using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jacquard.App {

// The mix, drawn behind the score.
//
// Two things, and both of them are the synth rather than the sequence: the finished
// output as a trace across the middle of the screen, and the voice pool as a row of
// slots along the bottom. What the sequence is doing is already on the plane — the
// playheads say which step each runner is on — and what it is doing is not the same
// question as what came out. A gate that did not fire, a note that lost its voice to a
// louder one, a limiter closing on a kick: none of that is visible on the plane and all
// of it is visible here.
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
        // Nothing here is ever off screen, and the bounds are recomputed from vertices
        // that move every frame, so they are simply given something that always holds.
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e4f);
    }

    void LateUpdate()
    {
        var camera = Camera.main;
        if (_material == null || camera == null) return;

        var synth = _app.Synth;
        if (synth == null) return;

        var scope = synth.Scope;
        if (!scope.IsCreated) return;

        // What the camera can see, which is what everything below is measured in: an
        // orthographic size is the half height, and the aspect gives the rest.
        var halfHeight = camera.orthographicSize;
        var halfWidth = halfHeight * camera.aspect;

        // One display pixel in those terms, so that a line an eighth of a millimetre
        // wide stays an eighth of a millimetre wide on a screen with twice the dots.
        var pixel = halfHeight * 2.0f / Mathf.Max(Screen.height, 1);

        _vertices.Clear();
        _colors.Clear();
        _indices.Clear();

        BuildTrace(scope, synth.SampleRate, halfWidth, halfHeight, pixel);
        BuildSlots(scope, halfWidth, halfHeight, pixel);

        _mesh.Clear(true);
        _mesh.SetVertices(_vertices);
        _mesh.SetColors(_colors);
        _mesh.SetIndices(_indices, MeshTopology.Triangles, 0, false);
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e4f);

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

    // The output, across the middle, one column of the mesh per few samples.
    //
    // Where the window starts is not simply "as far back as it reaches". A trace hung
    // off the write cursor slides by whatever the buffer size happens to be every
    // frame, so a held note comes out as a smear travelling sideways; hung off the last
    // rising zero crossing before that point, the same note stands still and what moves
    // is only what actually changed. Which is a scope's trigger, and it is worth the
    // dozen lines here for the same reason it is on the front of an oscilloscope.
    void BuildTrace(in FmSynthScope scope, int sampleRate, float halfWidth,
                    float halfHeight, float pixel)
    {
        var span = Mathf.Clamp((int)(Window * sampleRate), 64, scope.Length / 2);
        var start = Trigger(scope, span);

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
                var sample = scope.At(i);
                if (Mathf.Abs(sample) > Mathf.Abs(value)) value = sample;
            }

            var x = Mathf.Lerp(-halfWidth, halfWidth, column / (columns - 1.0f));
            var point = new Vector2(x, Mathf.Clamp(value, -1.0f, 1.0f) * height);

            if (column > 0)
                Ribbon(previous, point, thickness, Fade(TraceColor, column, columns));

            previous = point;
        }
    }

    // Where to start reading, which is the most recent rising crossing of zero before
    // the window would otherwise have begun. Nothing found inside a window's worth of
    // history means there is nothing periodic to hold still — silence, or a wash — and
    // the untriggered start is as good an answer as any.
    static int Trigger(in FmSynthScope scope, int span)
    {
        var start = scope.Head - 1 - span;

        for (var back = 0; back < span; back++)
        {
            var at = start - back;
            if (scope.At(at - 1) <= 0.0f && scope.At(at) > 0.0f) return at;
        }

        return start;
    }

    // The pool
    //
    // One slot per voice, in the order the pool holds them, along the bottom edge. The
    // order means nothing musically — a note takes whatever slot is free — and that is
    // precisely what makes the row worth drawing: it fills up as a chord is struck and
    // empties as the tails run out, so how close the pool is to being full is something
    // that can be seen rather than read off the voice count in the status line.
    //
    // The height is the square root of the level, which is the one place here that
    // anything is bent. A peak amplitude spends most of its life in the bottom quarter
    // of its range, so a bar drawn straight off one is a bar that flickers just above
    // the floor and says nothing about how loud the voice is.
    void BuildSlots(in FmSynthScope scope, float halfWidth, float halfHeight,
                    float pixel)
    {
        var slots = scope.Slots;
        if (slots == 0) return;

        if (_levels == null || _levels.Length != slots) _levels = new float[slots];

        var width = halfWidth * 2.0f / slots;
        var bar = width * SlotWidth;
        var tallest = halfHeight * SlotHeight;
        var floor = -halfHeight;

        // A level falls no faster than this, so that a note shorter than a frame is
        // still seen and a decay is watched rather than blinked at.
        var fall = Time.deltaTime / SlotFall;

        for (var slot = 0; slot < slots; slot++)
        {
            var level = Mathf.Clamp01(scope.Level(slot));
            _levels[slot] = Mathf.Max(level, _levels[slot] - fall);

            var height = Mathf.Sqrt(_levels[slot]) * tallest;
            if (height < pixel) continue;

            var centre = -halfWidth + (slot + 0.5f) * width;

            Quad(new Vector2(centre - bar * 0.5f, floor),
                 new Vector2(centre + bar * 0.5f, floor + height), SlotColor);
        }
    }

    // Geometry

    // A segment of the trace, as a box between two points. Thickness is vertical rather
    // than perpendicular to the segment, which on a trace of a few hundred columns is a
    // difference of well under a pixel except where the signal is nearly vertical — and
    // there the column either side of it is covering the same ground.
    void Ribbon(Vector2 from, Vector2 to, float thickness, Color color)
    {
        var half = thickness * 0.5f;
        var index = _vertices.Count;

        _vertices.Add(new Vector3(from.x, from.y - half, Depth));
        _vertices.Add(new Vector3(from.x, from.y + half, Depth));
        _vertices.Add(new Vector3(to.x, to.y + half, Depth));
        _vertices.Add(new Vector3(to.x, to.y - half, Depth));

        for (var i = 0; i < 4; i++) _colors.Add(color);

        Triangles(index);
    }

    void Quad(Vector2 low, Vector2 high, Color color)
    {
        var index = _vertices.Count;

        _vertices.Add(new Vector3(low.x, low.y, Depth));
        _vertices.Add(new Vector3(low.x, high.y, Depth));
        _vertices.Add(new Vector3(high.x, high.y, Depth));
        _vertices.Add(new Vector3(high.x, low.y, Depth));

        for (var i = 0; i < 4; i++) _colors.Add(color);

        Triangles(index);
    }

    void Triangles(int index)
    {
        _indices.Add(index);
        _indices.Add(index + 1);
        _indices.Add(index + 2);
        _indices.Add(index);
        _indices.Add(index + 2);
        _indices.Add(index + 3);
    }

    // Colour

    // The trace goes out at both ends rather than stopping at the edge of the screen,
    // since what is at the edge is an arbitrary moment of the sound and not the start
    // or the end of anything.
    static Color Fade(Color color, int column, int columns)
    {
        var position = column / (columns - 1.0f);
        var edge = Mathf.Min(position, 1.0f - position) / FadeWidth;
        return Shaded(color, Mathf.Min(edge, 1.0f));
    }

    // The panel paints in sRGB whatever the project's colour space, and this does not:
    // a vertex colour reaches the shader as the number it was given. So a colour taken
    // from Style is converted here rather than being picked again by eye, which is what
    // keeps one palette for both.
    static Color Shaded(Color color, float alpha)
    {
        var shade = QualitySettings.activeColorSpace == ColorSpace.Linear
                    ? color.linear : color;

        return new Color(shade.r, shade.g, shade.b, color.a * alpha);
    }

    // Private members

    JacquardApp _app;
    Material _material;
    Mesh _mesh;
    float[] _levels;

    readonly List<Vector3> _vertices = new();
    readonly List<Color> _colors = new();
    readonly List<int> _indices = new();

    // Far enough in front of the camera to be inside its near plane, and nothing else
    // is drawn at all, so any depth would do.
    const float Depth = 1.0f;

    const int MaxColumns = 512;

    // A column every three pixels, so the ceiling is only reached on a very wide screen.
    const float TraceHeight = 0.42f;  // Of the half height, at full scale
    const float FadeWidth = 0.12f;    // Of the width, at either end

    const float SlotWidth = 0.4f;   // Of a slot's share of the width
    const float SlotHeight = 0.16f; // Of the half height, at full level
    const float SlotFall = 0.4f;    // Seconds from full to nothing

    // Both alphas look far too small to be visible and are not, because the blend
    // happens in linear light where the background is 0.009 and this colour is 0.81.
    // They are what they are because they were measured rather than picked: the faintest
    // thing the plane draws is its lattice, which comes out at a luminance of 80 in a
    // screenshot, and a tenth of an alpha here lands the trace at 86 with the slots
    // under it at 73. Anything approaching what these numbers look like — the 0.16 they
    // started at read 102 — is a background that argues with the score in front of it.
    static readonly Color TraceColor = Style.Fade(Style.NoteLine, 0.10f);
    static readonly Color SlotColor = Style.Fade(Style.NoteLine, 0.07f);
}

} // namespace Jacquard.App
