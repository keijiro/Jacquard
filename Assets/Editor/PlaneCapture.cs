using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.Editor {

// A picture of the plane with nothing else on the screen, for the store graphics.
//
// Docs/Figures/README.md takes its pictures by hand and argues for it: a figure is
// retaken when the tiles it illustrates change, which is rarely, and a generator for
// twelve crops is more to keep right than the twelve crops are. A promotional graphic
// is the case that argument does not cover. What it needs is not a crop of the screen
// but a plate larger than any screen this app runs on -- the whole plane at twice the
// size the interface is laid out at, so a store's own artwork can be cut out of it and
// come out sharp at whatever size that store asks for. There is no window that size to
// screenshot, so the panel is pointed at a render texture instead and the plate is read
// back off it.
//
// The same bargain the figures make is kept: what is captured is this app drawing its
// own score, so a plate cannot drift from the interface the way a drawing of it would.
// Everything below is either aimed at that or at getting the chrome out of the way.
//
// It runs in play mode, off a menu item, and puts everything back afterwards -- the
// score on the plane included, since the plate is cut from sample4 and whatever was
// being worked on is not.
static class PlaneCapture
{
    // The piece the plate is cut from. sample4 fills more of the plane than the others
    // and every one of the eight channels is written on in it.
    const string ScorePath = "Assets/Jacquard/Scores/sample4.jacquard.txt";

    const string PlatePath = "Branding/plane.png";
    const string GridPath = "Branding/plane.txt";

    // Twice the size the interface is laid out at, which is the scale the figures are
    // cut at and for the same reason: the grid is drawn in whole pixels, so a whole
    // number keeps every hairline on one. What a store graphic then does to the plate is
    // a reduction, and a reduction of a 2x plate is what makes the note names in the
    // cells survive it.
    const int Scale = 2;

    // Style is internal to the runtime assembly, so the two numbers this needs out of it
    // are spelled out here -- the same trade SceneBuilder makes for the background colour,
    // which is the first of them.
    static readonly Color Background = new Color(0.086f, 0.086f, 0.086f);

    // The cell pitch, which is what the sidecar hands to whatever crops the plate: a cell
    // is 30x32 with a 4px gutter, laid out from a margin of 18.
    const float Padding = 18.0f, StrideX = 34.0f, StrideY = 36.0f;
    const float CellWidth = 30.0f, CellHeight = 32.0f;

    [MenuItem("Jacquard/Capture Score Plane")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Capture Score Plane",
                                        "Enter play mode first: what is captured is the "
                                        + "running app's own plane.", "OK");
            return;
        }

        var app = Object.FindFirstObjectByType<App.JacquardApp>();

        if (app == null)
        {
            EditorUtility.DisplayDialog("Capture Score Plane",
                                        "No JacquardApp in the scene.", "OK");
            return;
        }

        // On the app itself, since what this waits for is frames going by and the editor's
        // own update loop is not where they are counted.
        app.StartCoroutine(Capture(app));
    }

    static IEnumerator Capture(App.JacquardApp app)
    {
        var document = app.GetComponent<UIDocument>();
        var settings = document.panelSettings;
        var view = app.View;

        // The score this is a plate of, put on the plane the way a load puts one there.
        // Nothing is told to the sequencer: it is not playing, and a plate has no
        // playheads on it.
        var project = ProjectFormat.Read(File.ReadAllText(ScorePath));
        app.Editor.Adopt(project);

        // The cursor's outline is part of the interface and no part of a score. (0,0) is
        // the far corner of the empty margin the plane keeps above and left of any score,
        // so it is off the plate rather than merely out of the way.
        view.SetCursor(new GridPoint(0, 0));

        // Everything that is not the plane, which is the transport row, whichever panels
        // happen to be raised, and the onboarding sheet on a first launch. Found by
        // walking up from the plane and putting every sibling on the way down, so this
        // knows nothing about what the chrome is made of or how much of it there is.
        //
        // The inline value is kept rather than cleared afterwards: most of these are
        // already display:none, having been put down by a switch, and a panel restored to
        // whatever it inherits would come back up.
        var hidden = new List<(VisualElement, StyleEnum<DisplayStyle>)>();

        for (var element = (VisualElement)view; element.parent != null;
             element = element.parent)
            foreach (var sibling in element.parent.Children())
                if (sibling != element)
                {
                    hidden.Add((sibling, sibling.style.display));
                    sibling.style.display = DisplayStyle.None;
                }

        // The plane's ground, which is the camera's on a screen and nobody's in a render
        // texture: the panel is transparent over the camera's clear, and a texture the
        // panel draws into has no camera behind it. Painted by the plane itself rather
        // than by the texture's clear, so the colour goes through the same conversion
        // every other colour in the interface does.
        var ground = view.style.backgroundColor;
        view.style.backgroundColor = Background;

        // The visualizer draws behind the interface and not into the panel, so it cannot
        // reach the plate -- but it is switched off anyway, so that a capture taken with
        // it on and one taken with it off are the same picture.
        var visualizer = app.Visualizer;
        var drawing = visualizer != null && visualizer.enabled;
        if (visualizer != null) visualizer.enabled = false;

        // The plane sizes itself from the score, so how big the plate is is not decided
        // here: it is read off the plane once the rebuild has been laid out. Three
        // frames, because one is not enough -- the score is on the plane by then and the
        // layout still holds the size the last one asked for, and a plate cut to that is
        // a plate with a band of another score's plane down two of its sides.
        yield return null;
        yield return null;
        yield return null;

        var width = Mathf.CeilToInt(view.layout.width) * Scale;
        var height = Mathf.CeilToInt(view.layout.height) * Scale;

        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
                                       RenderTextureReadWrite.sRGB);

        // Written over the asset and put back below rather than applied to a copy of it.
        // Handing the document a different PanelSettings takes its tree off one panel and
        // puts it on another, and what is being captured is a tree the app built by hand
        // and holds references into; the asset is restored to the values it came with, so
        // what is left behind is a file marked dirty and identical.
        var scaleMode = settings.scaleMode;
        var scale = settings.scale;
        var texture = settings.targetTexture;
        var clearing = settings.clearColor;
        var clearValue = settings.colorClearValue;

        // Constant pixel size for the length of the capture, because a plate is measured
        // in cells and not in inches: the panel is at constant physical size on a screen,
        // which is the right answer for a control under a fingertip and no answer at all
        // for a texture nobody is holding.
        settings.scaleMode = PanelScaleMode.ConstantPixelSize;
        settings.scale = Scale;
        settings.targetTexture = target;

        // The plane covers the whole of a panel this size, so the clear is not what the
        // ground is painted with -- it is there because a render texture starts out
        // holding whatever the driver left in it, and a plate with one uncovered pixel of
        // that is a plate with one pixel of somebody else's frame in it.
        //
        // Handed the linear value rather than the colour: a clear is written straight to
        // an sRGB target and takes none of the conversion the interface's own colours
        // take, so the colour itself comes out three times too light. Which only ever
        // showed on a plate cut too large, and that is the point -- the insurance has to
        // be the same grey as the thing it is standing in for.
        settings.clearColor = true;
        settings.colorClearValue = Background.linear;

        // Two frames: one for the panel to take the texture and lay itself out against
        // it, and one to draw. Then the end of that frame, since a runtime panel is
        // repainted after everything this coroutine is resumed by.
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        var plate = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var active = RenderTexture.active;

        RenderTexture.active = target;
        plate.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        plate.Apply();
        RenderTexture.active = active;

        File.WriteAllBytes(PlatePath, plate.EncodeToPNG());

        // Where the cells are on the plate, for whatever cuts a graphic out of it. The
        // pitch is the interface's and the scale is this file's, and neither is a number
        // an artwork script should be holding a second copy of.
        File.WriteAllText(GridPath,
                          $"scale {Scale}\npadding {Padding}\n"
                          + $"stridex {StrideX}\nstridey {StrideY}\n"
                          + $"cellwidth {CellWidth}\ncellheight {CellHeight}\n"
                          + $"columns {Mathf.RoundToInt((view.layout.width - Padding * 2 + StrideX - CellWidth) / StrideX)}\n"
                          + $"rows {Mathf.RoundToInt((view.layout.height - Padding * 2 + StrideY - CellHeight) / StrideY)}\n");

        settings.scaleMode = scaleMode;
        settings.scale = scale;
        settings.targetTexture = texture;
        settings.clearColor = clearing;
        settings.colorClearValue = clearValue;

        view.style.backgroundColor = ground;
        foreach (var (element, display) in hidden) element.style.display = display;
        if (visualizer != null) visualizer.enabled = drawing;

        Object.Destroy(plate);
        target.Release();
        Object.Destroy(target);

        Debug.Log($"wrote {PlatePath} ({width}x{height}px at {Scale}x)");
    }
}

} // namespace Jacquard.Editor
