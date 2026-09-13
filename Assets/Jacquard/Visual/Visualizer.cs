using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jacquard.App {

// The mix, drawn behind the score.
//
// Two lines at most, and both of them the synth rather than the sequence: the finished
// output as a trace across the middle of the screen, and over it — when there is one to
// draw — the share of that output one channel is answerable for. What the sequence is
// doing is already on the plane — the playheads say which step each runner is on — and
// what it is doing is not the same question as what came out. A gate that did not fire,
// a limiter closing on a kick: neither of those is visible on the plane and both of them
// are visible here.
//
// Nothing here knows what a channel is, and that is deliberate rather than incidental.
// The scope carries a second ring and a flag saying whether anything is being kept in
// it, so the question asked here is "is the synth tapping something", never "which lane
// is selected" — the one that decides that is JacquardApp, which is also where every
// other reading of the score is turned into something the audio side can hold. So the
// second trace is a nameless second line and this file stays a drawing of the synth.
//
// It is drawn rather than laid out, which is why it is not on the UI panel with
// everything else. A trace is a few hundred columns rebuilt every frame, and UI Toolkit
// would want an element or a Painter2D call per column standing in a layout that has
// nothing to lay out; a mesh handed to the renderer is the shape this actually is. The
// panel is transparent over it and the camera clears to the colour the panel used to
// paint, so the two stack without either knowing about the other.
//
// Faint on purpose. The score is what the screen is for and this is behind it, so the
// trace is a wash the eye can ignore while reading a cell and can still see out of the
// corner of it while playing. The palette is the UI's own — there is no colour here
// that Style does not already hold.
//
// It is down until it is asked for, on the component's own enabled flag, which is what
// the switch on the transport row moves. Faint or not, this is the only thing on screen
// that moves when nothing is being edited, and a background that is always on is a
// background nobody chose.
//
// The geometry is built from nothing every frame and that is the right shape for it:
// the picture genuinely differs every frame, so there is no rebuild here paying for a
// picture that is the same picture. At the 512 columns every device this ships to
// reaches, a frame is 511 ribbon quads — 2044 vertices and 3066 indices — and twice that
// while a second trace is up, which is the whole of what the second one costs on this
// side. What has been taken out of it is the part that was *not* new every frame: a
// colour space conversion that ran once a column for a result in which only the alpha
// moved, an index buffer that spelled the same three thousand numbers out again, and
// the trigger's habit of fetching every sample twice.

[RequireComponent(typeof(JacquardApp))]
public sealed class Visualizer : MonoBehaviour
{
    // The one thing that has to be a reference rather than a lookup: a shader nothing
    // in a scene points at is a shader that is not in the build.
    [field:SerializeField]
    public Shader Shader { get; set; }

    // How much of the mix the trace shows, in seconds. A frame's worth of audio is
    // about a fiftieth of a second, so anything much longer than this is a trace that
    // slides sideways faster than the eye follows.
    [field:SerializeField, Range(0.005f, 0.05f)]
    public float Window { get; set; } = 0.03f;

    // MonoBehaviour implementation

    // Awake and not Start, because this component ships disabled: what raises it is a
    // switch on the transport row, and Start never runs on something that has not been
    // enabled yet. Awake does, so the material and the mesh are built once whether or
    // not anyone ever asks to see the mix.
    void Awake()
    {
        _app = GetComponent<JacquardApp>();

        if (Shader != null)
        {
            _material = new Material(Shader);
            _material.hideFlags = HideFlags.HideAndDontSave;
        }

        _mesh = new Mesh { name = "Visualizer" };
        _mesh.MarkDynamic();
        // Nothing here is ever off screen, so the bounds are simply given something that
        // always holds — and given it once, because nothing in the rebuild disturbs them.
        // That was worth checking rather than assuming, since it reads like the kind of
        // thing a Clear would reset: Mesh.Clear keeps whatever bounds it was holding,
        // and the only thing that ever overwrote them was SetVertices recomputing them,
        // which LateUpdate now tells it not to do. This is the value RenderMesh culls
        // against on every frame of the app's life.
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e4f);

        // The trace's colour, converted the one time it can be. Alpha is all that
        // varies down the trace, so Shaded was being asked the same question once a
        // column for an answer that cannot change while the app runs. Here rather than
        // as another static readonly beside the palette, because static initialisers
        // run in declaration order and a converted colour declared above the colour it
        // converts comes out black without saying so.
        _traceColor = Shaded(TraceColor);
        _channelColor = Shaded(ChannelColor);
    }

    void LateUpdate()
    {
        var camera = Camera.main;
        if (_material == null || camera == null) return;

        var synth = _app.Synth;
        if (synth == null) return;

        var scope = synth.Scope;
        if (!scope.IsCreated) return;

        // The scope travels by value from here down rather than by `in`, which looks
        // like the wrong way round and is not. None of At, Head or Length is a readonly
        // member, so `in` obliges the compiler to copy the whole struct — both of its
        // NativeArrays, each carrying a safety handle in the editor — before every one
        // of the several thousand reads a frame, where by value it is one copy per call.
        // The pipeline hands the scope over by value already, so this is a copy of a
        // copy and there is nothing here for `in` to protect.

        // What the camera can see, which is what everything below is measured in: an
        // orthographic size is the half height, and the aspect gives the rest.
        var halfHeight = camera.orthographicSize;
        var halfWidth = halfHeight * camera.aspect;

        // One display pixel in those terms, so that a line an eighth of a millimetre
        // wide stays an eighth of a millimetre wide on a screen with twice the dots.
        var pixel = halfHeight * 2.0f / Mathf.Max(Screen.height, 1);

        _vertices.Clear();
        _colors.Clear();

        // Where the window starts is not simply "as far back as it reaches". A trace
        // hung off the write cursor slides by whatever the buffer size happens to be
        // every frame, so a held note comes out as a smear travelling sideways; hung
        // off the last rising zero crossing before that point, the same note stands
        // still and what moves is only what actually changed. Which is a scope's
        // trigger, and it is worth the dozen lines it takes for the same reason it is
        // on the front of an oscilloscope.
        //
        // Taken here rather than inside a trace because both traces take it, and it
        // reads the mix whichever of them is being drawn: it is the shared origin of
        // time for the two lines, and a channel triggering on itself would slide the
        // mix about underneath it every time the selection moved.
        var span = Mathf.Clamp((int)(Window * synth.SampleRate), 64, scope.Length / 2);
        var start = Trigger(scope, span);

        BuildTrace(scope, false, start, span, _traceColor, halfWidth, halfHeight, pixel);

        // Over the mix rather than under it, which is the whole of the layering: one
        // mesh, alpha blended with no depth test, so the quads added last are the ones
        // on top. And only when there is something in the tap — an untapped ring is
        // silence, and silence draws a straight bright line across the middle that says
        // nothing and cannot be ignored.
        if (scope.Watch != 0)
            BuildTrace(scope, true, start, span, _channelColor,
                       halfWidth, halfHeight, pixel);

        // The trace adds vertices four at a time, so the index buffer follows from the
        // vertex count and nothing else.
        var quads = _vertices.Count / 4;
        GrowIndices(quads);

        _mesh.Clear(true);
        // No bounds recalculation over two thousand vertices, which is the one thing
        // here that ever wrote the bounds — so with it off, the value Awake gave them
        // is still standing when RenderMesh reads it below.
        _mesh.SetVertices(_vertices, 0, _vertices.Count,
                          MeshUpdateFlags.DontRecalculateBounds);
        _mesh.SetColors(_colors);
        _mesh.SetIndices(_indices, 0, quads * 6, MeshTopology.Triangles, 0, false);

        var parameters = new RenderParams(_material)
          { worldBounds = _mesh.bounds,
            layer = gameObject.layer,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false };

        Graphics.RenderMesh(parameters, _mesh, 0, Matrix4x4.identity);
    }

    void OnDestroy()
    {
        if (_material != null) Destroy(_material);
        if (_mesh != null) Destroy(_mesh);
    }

    // The trace

    // One ring of the scope, across the middle, one column of the mesh per few samples.
    // Which ring is the whole of what `tapped` says: the window, the origin and the
    // geometry are the same either way, and the second line differs from the first only
    // in where its samples come from and what colour they are drawn in.
    void BuildTrace(FmSynthScope scope, bool tapped, int start, int span, Color color,
                    float halfWidth, float halfHeight, float pixel)
    {
        var columns = Mathf.Clamp(Mathf.RoundToInt(halfWidth * 2.0f / pixel / 3.0f),
                                  64, MaxColumns);

        var thickness = pixel * 1.5f;
        var height = halfHeight * TraceHeight;

        var previous = Vector2.zero;

        for (var column = 0; column < columns; column++)
        {
            // The loudest sample of the few this column stands for, sign and all. An
            // average would show a quiet trace of something that is not quiet, since
            // what a waveform does between two columns is cancel.
            var from = start + (int)((long)column * span / columns);
            var to = start + (int)((long)(column + 1) * span / columns);
            var value = 0.0f;

            for (var i = from; i < to || i == from; i++)
            {
                var sample = tapped ? scope.TapAt(i) : scope.At(i);
                if (Mathf.Abs(sample) > Mathf.Abs(value)) value = sample;
            }

            var x = Mathf.Lerp(-halfWidth, halfWidth, column / (columns - 1.0f));
            var point = new Vector2(x, Mathf.Clamp(value, -1.0f, 1.0f) * height);

            if (column > 0)
                Ribbon(previous, point, thickness, Fade(color, column, columns));

            previous = point;
        }
    }

    // Where to start reading, which is the most recent rising crossing of zero before
    // the window would otherwise have begun. Nothing found inside a window's worth of
    // history means there is nothing periodic to hold still — silence, or a wash — and
    // the untriggered start is as good an answer as any.
    //
    // The walk carries the upper sample of the pair rather than fetching it again: each
    // step's lower sample is the next step's upper one, so a scan that read twice per
    // step now reads once. Nothing at all on a sounding mix, where the test passes
    // inside a period or two — but in silence the test never passes and the walk runs
    // the whole span, which is a window's worth of ring reads saved in the state the app
    // rests in. Under the unsynchronised read FmSynthScope's header argues for, reading
    // a sample once where it used to be read twice can only make a pair more consistent.
    static int Trigger(FmSynthScope scope, int span)
    {
        var start = scope.Head - 1 - span;
        var above = scope.At(start);

        for (var back = 0; back < span; back++)
        {
            var at = start - back;
            var below = scope.At(at - 1);
            if (below <= 0.0f && above > 0.0f) return at;
            above = below;
        }

        return start;
    }

    // Geometry
    //
    // This adds exactly four vertices in the order the index pattern expects, and that
    // is the whole of the contract between it and GrowIndices below.

    // A segment of the trace, as a box between two points. Thickness is vertical rather
    // than perpendicular to the segment, which on a trace of a few hundred columns is a
    // difference of well under a pixel except where the signal is nearly vertical — and
    // there the column either side of it is covering the same ground.
    void Ribbon(Vector2 from, Vector2 to, float thickness, Color color)
    {
        var half = thickness * 0.5f;

        _vertices.Add(new Vector3(from.x, from.y - half, Depth));
        _vertices.Add(new Vector3(from.x, from.y + half, Depth));
        _vertices.Add(new Vector3(to.x, to.y + half, Depth));
        _vertices.Add(new Vector3(to.x, to.y - half, Depth));

        for (var i = 0; i < 4; i++) _colors.Add(color);
    }

    // The indices, which are a fact about the shape of the mesh and not about what is
    // in it: quad k is vertices 4k..4k+3 wound (0,1,2) and (0,2,3), whatever the quad
    // turns out to be a picture of. So the list is extended to reach the widest frame
    // the app has drawn so far and never written again, and each frame submits the
    // prefix of it that its own quad count asks for. What that replaces is three
    // thousand Add calls a frame spelling out numbers the list already held.
    void GrowIndices(int quads)
    {
        for (var quad = _indices.Count / 6; quad < quads; quad++)
        {
            var index = quad * 4;

            _indices.Add(index);
            _indices.Add(index + 1);
            _indices.Add(index + 2);
            _indices.Add(index);
            _indices.Add(index + 2);
            _indices.Add(index + 3);
        }
    }

    // Colour

    // The trace goes out at both ends rather than stopping at the edge of the screen,
    // since what is at the edge is an arbitrary moment of the sound and not the start
    // or the end of anything. The colour arriving here is already converted, so this is
    // the multiply it always was underneath.
    static Color Fade(Color color, int column, int columns)
    {
        var position = column / (columns - 1.0f);
        var edge = Mathf.Min(position, 1.0f - position) / FadeWidth;
        return new Color(color.r, color.g, color.b,
                         color.a * Mathf.Min(edge, 1.0f));
    }

    // The panel paints in sRGB whatever the project's colour space, and this does not:
    // a vertex colour reaches the shader as the number it was given. So a colour taken
    // from Style is converted here rather than being picked again by eye, which is what
    // keeps one palette for both. Called once, from Awake: the colour space cannot
    // change under a running app, and reading QualitySettings and taking three pows for
    // it once a column was answering a settled question five hundred times a frame.
    static Color Shaded(Color color)
    {
        var shade = QualitySettings.activeColorSpace == ColorSpace.Linear
                    ? color.linear : color;

        return new Color(shade.r, shade.g, shade.b, color.a);
    }

    // Private members

    JacquardApp _app;
    Material _material;
    Mesh _mesh;
    Color _traceColor;
    Color _channelColor;

    readonly List<Vector3> _vertices = new();
    readonly List<Color> _colors = new();
    readonly List<int> _indices = new();

    // Far enough in front of the camera to be inside its near plane, and nothing else
    // is drawn at all, so any depth would do.
    const float Depth = 1.0f;

    // A column every three physical pixels, up to a ceiling that is not the headroom it
    // reads as: the count reduces to clamp(Screen.width / 3, 64, 512), so it is met at
    // 1536 across and the iPad is 2360. 512 is the operative number on every device this
    // ships to, not a limit nobody reaches.
    const int MaxColumns = 512;

    const float TraceHeight = 0.42f;  // Of the half height, at full scale
    const float FadeWidth = 0.12f;    // Of the width, at either end

    // The alpha looks far too small to be visible and is not, because the blend happens
    // in linear light where the background is 0.009 and this colour is 0.81. It is what
    // it is because it was measured rather than picked: the faintest thing the plane
    // draws is its lattice, which comes out at a luminance of 80 in a screenshot, and a
    // tenth of an alpha here lands the trace at 86. Anything approaching what that
    // number looks like — the 0.16 it started at read 102 — is a background that argues
    // with the score in front of it.
    static readonly Color TraceColor = Style.Fade(Style.NoteLine, 0.10f);

    // The watched channel, over the top of that. It is the one colour here that has two
    // sides to answer to: under the mix's own brightness the second line disappears into
    // the first, and far over it the wash stops being a wash — and this one is on screen
    // exactly while a lane is selected, which is when the eye is on the plane rather
    // than on the background.
    //
    // Placed against the same blend the figure above was measured from, which the two
    // readings recorded there are enough to check. In linear light the result is
    // a·0.81 + (1-a)·0.009; encoded back to sRGB that puts 0.10 at 84 against the 86
    // read off the screenshot, and the 0.16 that was tried and rejected at 104 against
    // 102 — good to a couple of counts across the range in question. This alpha comes
    // out at 119, which is 33 clear of the mix where the mix is 6 clear of the lattice.
    //
    // 0.16 was rejected for a trace that is always on, and this one is not: it is the
    // answer to a lane having been chosen, and it goes when the choice does. That is the
    // whole of the argument for being over it, and the arithmetic cannot finish it —
    // whether 119 still reads as background with a cell being edited in front of it is a
    // screenshot rather than a calculation.
    static readonly Color ChannelColor = Style.Fade(Style.NoteLine, 0.22f);
}

} // namespace Jacquard.App
