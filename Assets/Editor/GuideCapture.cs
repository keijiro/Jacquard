using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.Editor {

// The user guide's pictures of the interface, which live in the jacquard-doc repository.
//
// Docs/Figures/README.md takes the tile figures by hand and argues for it: a figure is
// retaken when the tiles it illustrates change, which is rarely. The guide's pictures of
// the panels are the case that argument does not cover. A release moves rows inside three
// panels at once, and what has to be true of all of them together -- the same scale, the
// same margin, the same ground, the visualizer in the same state -- is what a hand is
// worst at keeping. So the set is taken off one shutter, and jacquard-doc's README
// describes what that shutter does rather than what a hand was asked to do.
//
// What it writes, and what each is a picture of:
//
//   screenshot.png   the whole app, transport running, for the head of the page
//   screen.png       the whole app with three of its parts named, for The Screen
//   tile-sound.png   the Tile panel over a Channel Start, for The Sound
//   channels.png     the Channels panel, for The Channels Panel
//   system.png       the System panel, for System Settings
//
// Everything is laid out at a scale of 2, so a UI pixel is two device pixels and the page
// asks for each picture at half its pixel width. The panels are cut to their own bounds
// plus a margin of ten, which is what leaves the lattice dots room around them -- the same
// margin the tile figures are cut at, since what tells a panel from the plane is the air
// around it.
//
// The visualizer is on for the two whole-screen pictures, where it is the app, and off for
// the three panel crops, where a flat line through the margin reads as an artefact rather
// than as the interface. It is switched off by taking the camera out from behind the
// shutter rather than by pressing the switch that turns it off: what the crops want is the
// trace gone, not the System panel photographed in a state it does not ship in.
//
// It runs in play mode, off a menu item, and does not put back what it took over -- it
// loads sample1 over whatever score was open and leaves the transport where the last
// picture wanted it. Nothing here is saved, so the cost of that is re-loading the score
// that was being worked on.
static class GuideCapture
{
    // The score every picture is of, which is the one the guide has always shown: six
    // channels, a jump and its target, and a lane short enough that the whole of it is
    // read at once. Reached through the store rather than out of Assets, so the name on
    // the transport row is the name of what is on the plane.
    const string ScoreName = "sample1";

    // Where the cursor sits for all five is not decided here: it is where a launch puts
    // it, which is the corner of the score and for this one the head of CH1's lane -- the
    // Channel Start tile. That is what raises the longest panel in the app, the lane's own
    // rows with the whole of a channel's sound under them, which is the panel the guide's
    // Sound section is a picture of. So the whole screen and the crop of that panel show
    // the same thing, and neither carries a cell reference a different score would move.

    const string OutDir = "Branding/guide";

    // Twice the size the interface is laid out at, for the reason PlaneCapture's plate is:
    // the grid is drawn in whole pixels, so a whole number keeps every hairline on one,
    // and a page that asks for each picture at half its pixel width gets the interface at
    // the size the interface is drawn at and sharp on a display with the pixels for it.
    const int Scale = 2;

    // The air a panel is cut with, which is Controls.Inset by eye rather than by
    // reference: Controls is internal to the runtime assembly. Ten is what the tile
    // figures are cut at as well.
    const float Margin = 10.0f;

    // Style.Background, spelled out for the same reason PlaneCapture spells it out.
    static readonly Color Background = new Color(0.086f, 0.086f, 0.086f);

    // Style.Label, which is the grey every caption on a panel is set in and the grey the
    // guide's own labels and their leaders are set in.
    static readonly Color LabelInk = new Color32(0x9a, 0x9a, 0x9a, 0xff);

    // The frame the three panel crops are laid out in. Wide enough for the plane to be
    // behind the panel rather than short of it, and tall enough that the longest panel is
    // laid out whole rather than scrolled -- a panel cut off at the foot of the screen is
    // a panel cut off in the picture.
    const int CropWidth = 1200, CropHeight = 1100;

    // The whole screen at very nearly the narrowest the transport row fits across. A
    // screen wider than this is a picture of the app shown smaller than the app, and the
    // guide has 832 pixels to put it in.
    const int ScreenWidth = 880, ScreenHeight = 640;

    // The head of the page, which is the one picture on the site that is not read against
    // a paragraph: it is the app at its own proportions, wide enough to hold a score worth
    // looking at.
    //
    // Tall enough to hold the longest panel in the app whole, which is what decides the
    // figure rather than a proportion: the Tile panel over a Channel Start reaches 734
    // units, and a picture of the app whose one panel runs off the bottom edge is a
    // picture of a panel that has more on it than the reader is being shown. The frame
    // above is free of that -- it is a crop of the panel and cannot cut it -- and the
    // named screen below is narrower than the panel is tall on purpose. This one is the
    // whole app and has to fit it.
    const int HeroWidth = 1108, HeroHeight = 768;

    // What a raised switch does to its own ground, which is how the state of a transport
    // switch is read back rather than tracked. ShotCapture argues this one.
    const float ActiveGround = 0.5f;

    // Every switch on the transport row that raises a panel.
    static readonly string[] Switches =
      { "Channels", "Send FX", "Live FX", "Global", "System" };

    [MenuItem("Jacquard/Capture Guide Pictures")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Capture Guide Pictures",
                                        "Enter play mode first: what is captured is the "
                                        + "running app.", "OK");
            return;
        }

        var app = Object.FindAnyObjectByType<App.JacquardApp>();

        if (app == null)
        {
            EditorUtility.DisplayDialog("Capture Guide Pictures",
                                        "No JacquardApp in the scene.", "OK");
            return;
        }

        if (Camera.main == null)
        {
            EditorUtility.DisplayDialog("Capture Guide Pictures",
                                        "No main camera: the visualizer draws on it, and "
                                        + "the two whole-screen pictures are pictures of "
                                        + "it.", "OK");
            return;
        }

        if (!AsShipped()) return;

        app.StartCoroutine(Capture(app));
    }

    // The settings the app remembers for this machine, which are in three of these
    // pictures and behind all five.
    //
    // Everything else the shutter decides for itself. These it cannot: they are read at
    // launch and held in PlayerPrefs, so what they are when the menu item is pressed is
    // whatever the last session of the app on this machine was left on -- and the page
    // says of three of them that the app starts on, starts on and starts off. A picture
    // taken with Audition off is a picture contradicting the row beside it, and Stage
    // Mode on takes Save and the guide icon off the transport row and the Swap group off
    // the Channels panel, which is three of the five pictures wrong at once.
    //
    // So this refuses rather than corrects. Writing the defaults here would put the app
    // in a state it would then have to be restarted to show, and would quietly throw away
    // settings somebody chose; what it names instead is exactly what to change, and the
    // capture is one menu press away once it has been changed.
    //
    // The keys and the defaults are spelled out rather than reached, since each belongs to
    // a static the runtime assembly keeps internal -- the same trade PlaneCapture makes
    // for Style's two colours. See StageMode, Audition, OutputVolume, DspBuffer and
    // SystemPanel.
    static bool AsShipped()
    {
        var off = new List<string>();

        Check(off, "Visualizer", "Jacquard.Visualizer", 1);
        Check(off, "Audition", "Jacquard.Audition", 1);
        Check(off, "Stage Mode", "Jacquard.StageMode", 0);

        if (PlayerPrefs.GetInt(BufferKey, BufferDefault) != BufferDefault)
            off.Add($"Buffer size is {PlayerPrefs.GetInt(BufferKey)}, not {BufferDefault}");

        if (!Mathf.Approximately(PlayerPrefs.GetFloat(VolumeKey, VolumeDefault),
                                 VolumeDefault))
            off.Add($"Volume is {PlayerPrefs.GetFloat(VolumeKey):0.0} dB, "
                    + $"not {VolumeDefault:0.0}");

        if (off.Count == 0) return true;

        EditorUtility.DisplayDialog(
          "Capture Guide Pictures",
          "The app's settings are not the ones it ships with, and three of these "
          + "pictures are pictures of them:\n\n" + string.Join("\n", off)
          + "\n\nSet them on the System panel, leave play mode and enter it again so "
          + "the app reads them, then run this.", "OK");

        return false;
    }

    static void Check(List<string> off, string name, string key, int shipped)
    {
        if (PlayerPrefs.GetInt(key, shipped) == shipped) return;

        off.Add($"{name} is {(shipped == 0 ? "on" : "off")}, and ships "
                + $"{(shipped == 0 ? "off" : "on")}");
    }

    const string BufferKey = "Jacquard.DspBuffer", VolumeKey = "Jacquard.OutputVolume";

    // DspBuffer.Default and OutputVolume.Default. The buffer's is the desktop figure,
    // which is the only one an editor taking these ever has.
    const int BufferDefault = 512;
    const float VolumeDefault = -1.0f;

    static IEnumerator Capture(App.JacquardApp app)
    {
        var document = app.GetComponent<UIDocument>();
        var settings = document.panelSettings;
        var root = document.rootVisualElement;
        var view = app.View;
        var camera = Camera.main;

        var buttons = Buttons(root);

        buttons.TryGetValue("Play", out var transport);
        if (transport == null) buttons.TryGetValue("Stop", out transport);

        // The score, loaded the way a press of Load loads one, so that the name on the
        // transport row is the name of what is on the plane. RefreshSlots is what rebuilds
        // the chooser around a name set from outside it, and it is internal -- the same
        // reflection ShotCapture reaches a Clickable with, and for the same reason: there
        // is no public way in and the alternative is a picture that names the wrong file.
        app.Store.Name = ScoreName;
        RefreshSlots(app);
        app.Load();

        foreach (var name in Switches) Want(buttons, name, false);

        // After the load rather than with it: a score arriving rebuilds the plane, and a
        // frame asked for before that rebuild is a frame the rebuild puts back.
        yield return null;
        yield return null;

        // The plane framed the way a launch frames it -- the score's own corner two cells
        // in from the edge, with the cursor on the head of its first lane. It is the app's
        // own answer to where to look, and the alternative is this file carrying an offset
        // in plane coordinates that a score written differently would put in the wrong
        // place. ShowScore is internal and only ever called at startup, which is the same
        // reflection RefreshSlots is reached by and for the same reason.
        ShowScore(app);

        // What the panel was on, for the whole of the run rather than for each picture.
        // Every shot below points the interface at a texture and reads the layout back off
        // it, and PanelPlate restores what it finds -- so what it finds has to be the
        // capture's own state and not the screen's, or the second half of every shot would
        // be measured against a layout the shutter never saw.
        var scaleMode = settings.scaleMode;
        var scale = settings.scale;
        var texture = settings.targetTexture;

        Directory.CreateDirectory(OutDir);

        var written = new List<string>();

        // The plane taken out from behind the three crops, which is what the margin of ten
        // is left for: the air around a panel is what tells it from the plane, and a tile
        // cut in half by the edge of the picture reads as an artefact rather than as the
        // app -- the same argument the visualizer is switched off under. Where a panel
        // comes up over is wherever the score happens to have been left, so leaving it
        // there would make the margin a different picture every time.
        //
        // Hidden rather than taken out of the tree, so that the panels are laid out
        // exactly where they are laid out on a screen with a score behind them. What is
        // left is the ground the plane paints itself on, which is the ground PanelPlate
        // clears to and the one the panel is read against anyway.
        var showing = view.style.visibility;
        view.style.visibility = Visibility.Hidden;

        // The transport is stopped through all three, so there is nothing for the
        // visualizer to draw even if it were behind the shutter.
        yield return Crop(settings, "tile-sound", "Channel Start Tile", root, written);

        Want(buttons, "Channels", true);
        yield return Crop(settings, "channels", "Channels", root, written);
        Want(buttons, "Channels", false);

        Want(buttons, "System", true);
        yield return Crop(settings, "system", "System", root, written);
        Want(buttons, "System", false);

        // Back on the plane, which is what the two whole-screen pictures are pictures of.
        view.style.visibility = showing;

        // The whole screen with its parts named, stopped: the visualizer is behind it and
        // draws the one flat line a silent output is, which is what the app looks like
        // with nothing playing and is the state the paragraph beside it describes.
        //
        // The names are placed against the frame the shutter is about to take rather than
        // against the screen, so the interface is laid out at that size first and left
        // there while they are written. The plane is framed again in that same layout,
        // and has to be: where a score can be scrolled to is a fact about how much of the
        // plane can be seen, so a frame asked for at one size is clamped at another.
        yield return Settle(settings, ScreenWidth, ScreenHeight);

        ShowScore(app);
        yield return null;

        var labels = Labels(root, view);

        yield return Shot(settings, camera, "screen", ScreenWidth, ScreenHeight, written);

        labels.RemoveFromHierarchy();

        // The head of the page, running: the trace behind the score is a thing the app
        // does that no still of a stopped transport shows.
        yield return Settle(settings, HeroWidth, HeroHeight);

        ShowScore(app);
        yield return null;

        if (transport != null && transport.text == "Play") Press(transport);

        // Long enough for there to be a mix to draw. The scope holds nothing at all until
        // buffers have come back from a device that was started with the transport, which
        // takes longer than the shutter does.
        yield return new WaitForSeconds(1.0f);

        yield return Shot(settings, camera, "screenshot", HeroWidth, HeroHeight, written);

        settings.scaleMode = scaleMode;
        settings.scale = scale;
        settings.targetTexture = texture;

        Release();

        Debug.Log($"wrote {written.Count} guide pictures at {Scale}x:\n"
                  + string.Join("\n", written));
    }

    // Lays the interface out at the size the next shutter will open on, and leaves it
    // there.
    //
    // Where a panel stands is an answer to how big the frame is -- the Tile panel is
    // against the right edge, and the plane is what is left over -- so a crop measured
    // against the editor's own window is a crop cut somewhere else entirely. What this
    // buys is one layout for the measuring and the taking both.
    static IEnumerator Settle(PanelSettings settings, int width, int height)
    {
        Release();

        _probe = new RenderTexture(width * Scale, height * Scale, 24,
                                   RenderTextureFormat.ARGB32,
                                   RenderTextureReadWrite.sRGB);

        settings.scaleMode = PanelScaleMode.ConstantPixelSize;
        settings.scale = Scale;
        settings.targetTexture = _probe;

        // One frame for the panel to take the texture and lay itself out against it, and
        // one for that layout to be resolved into the bounds this is about to be asked
        // for.
        yield return null;
        yield return null;
    }

    // The texture the interface is laid out against between shutters. It is never read
    // back from -- what a picture is read off is PanelPlate's own, taken a moment later --
    // so it exists only to be a frame of the right size for the layout to answer to.
    static RenderTexture _probe;

    static void Release()
    {
        if (_probe == null) return;

        _probe.Release();
        Object.DestroyImmediate(_probe);
        _probe = null;
    }

    // A panel cut to its own bounds plus the margin, off a frame with no camera behind it.
    //
    // The frame is laid out first and the panel is found in it afterwards, because where a
    // panel stands and how tall it is are the layout's answers and not this file's: a row
    // added to a panel moves its foot, and a crop that carried its own numbers would be a
    // crop that has to be corrected every time the app grows a control.
    static IEnumerator Crop(PanelSettings settings, string name, string title,
                            VisualElement root, List<string> written)
    {
        yield return Settle(settings, CropWidth, CropHeight);

        var panel = Panel(root, title);

        if (panel == null)
        {
            Debug.LogError($"no panel headed \"{title}\" -- {name} not written");
            yield break;
        }

        var bounds = panel.worldBound;

        Texture2D plate = null;

        yield return PanelPlate.Take(settings, CropWidth * Scale, CropHeight * Scale,
                                     Scale, Background, taken => plate = taken);

        var x = Mathf.RoundToInt((bounds.xMin - Margin) * Scale);
        var width = Mathf.RoundToInt((bounds.width + Margin * 2) * Scale);
        var height = Mathf.RoundToInt((bounds.height + Margin * 2) * Scale);

        // The plate reads back bottom up and the layout is top down, so the row a crop
        // starts at is counted from the other end.
        var top = Mathf.RoundToInt((bounds.yMin - Margin) * Scale);
        var y = plate.height - top - height;

        var crop = new Texture2D(width, height, TextureFormat.RGBA32, false);
        crop.SetPixels(plate.GetPixels(x, y, width, height));
        crop.Apply();

        var path = $"{OutDir}/{name}.png";
        File.WriteAllBytes(path, crop.EncodeToPNG());
        written.Add($"{path} ({width}x{height})");

        Object.Destroy(crop);
        Object.Destroy(plate);
    }

    // The whole screen at a size of its own, with the camera behind it so that what is
    // under the interface is the visualizer rather than a flat colour.
    static IEnumerator Shot(PanelSettings settings, Camera camera, string name,
                            int width, int height, List<string> written)
    {
        Texture2D frame = null;

        yield return PanelPlate.Take(settings, width * Scale, height * Scale, Scale,
                                     camera, taken => frame = taken);

        var path = $"{OutDir}/{name}.png";
        File.WriteAllBytes(path, frame.EncodeToPNG());
        written.Add($"{path} ({width * Scale}x{height * Scale})");

        Object.Destroy(frame);
    }

    // The three names on the whole-screen picture, and the leaders that point them at what
    // they name.
    //
    // They go on the screen as one more layer of the app's own interface and are captured
    // with everything else, rather than being drawn on the picture afterwards. So the ink
    // in a label is the ink the app would have used, and a label cannot drift from the
    // chrome beside it.
    //
    // Set at fourteen rather than at the size the app sets a caption, which is the one
    // place these depart from the interface and is deliberate: a label here is not one
    // more thing on the screen being described, it is the page speaking about the picture,
    // and it is read at the size the page's own captions are read at.
    static VisualElement Labels(VisualElement root, VisualElement view)
    {
        const float Size = 14.0f;

        var layer = new VisualElement { pickingMode = PickingMode.Ignore };
        layer.StretchToParentSize();
        root.Add(layer);

        var tile = Panel(root, "Channel Start Tile");

        if (tile == null)
        {
            Debug.LogError("no Tile panel up -- the whole screen is named without it");
            return layer;
        }

        var panel = tile.worldBound;

        // The screen, which is what these are placed against. Not the plane: a ScoreView
        // sizes itself to the score it is holding and lives inside something that scrolls,
        // so its width is how far the piece reaches and not how much of it can be seen.
        var screen = root.worldBound;

        // The transport row is not asked for by name: it is whatever holds Save, which is
        // a button no panel has.
        var row = Buttons(root)["Save"].worldBound;

        // Under the row it names, over the score's own left half, where the plane is
        // empty above the first lane.
        Name(layer, "Transport row", Size, screen.width * 0.39f, row.yMax + 30.0f,
             TextAnchor.UpperCenter);
        Leader(layer, screen.width * 0.39f, row.yMax + 8.0f,
               screen.width * 0.39f, row.yMax + 26.0f);

        // Left of the panel it names, on the plane rather than on the panel, since a label
        // over the thing it points at is a label in the way of it. Above the middle rather
        // than level with it: the trace the visualizer draws across a stopped output runs
        // through the middle, and a name with a rule through it is not a name.
        //
        // The three are placed in the bands this score leaves empty, and that is what
        // these fractions are -- nothing about them is a rule, and a score with its lanes
        // somewhere else would want them somewhere else. What is worth keeping is where
        // they point: one at the row above, one at the panel beside it, one at the ground
        // under both.
        Name(layer, "Tile panel", Size, panel.xMin - 60.0f, screen.height * 0.365f,
              TextAnchor.UpperRight);
        Leader(layer, panel.xMin - 52.0f, screen.height * 0.365f + Size * 0.62f,
               panel.xMin, screen.height * 0.365f + Size * 0.62f);

        // Under the lanes rather than at the foot of the picture, in the gap this score
        // leaves across the whole width of it: what a leader dropped from the bottom edge
        // pointed at was whichever tile happened to be over it, and what this one points
        // at is the ground between two lanes, which is the plane and nothing else.
        Name(layer, "Score plane", Size, screen.width * 0.47f, screen.height * 0.755f,
              TextAnchor.UpperCenter);
        Leader(layer, screen.width * 0.47f, screen.height * 0.755f - 22.0f,
               screen.width * 0.47f, screen.height * 0.755f - 4.0f);

        return layer;
    }

    // One label, placed by a point and the corner of itself that point is.
    static void Name(VisualElement layer, string text, float size, float x, float y,
                     TextAnchor anchor)
    {
        var label = new Label(text);
        label.style.fontSize = size;
        label.style.color = LabelInk;
        label.style.position = Position.Absolute;
        label.style.top = y;

        if (anchor == TextAnchor.UpperRight)
        {
            label.style.right = 0;
            label.style.left = 0;
            label.style.width = x;
            label.style.unityTextAlign = TextAnchor.UpperRight;
        }
        else
        {
            label.style.left = 0;
            label.style.width = x * 2.0f;
            label.style.unityTextAlign = TextAnchor.UpperCenter;
        }

        layer.Add(label);
    }

    // One leader, in axis-aligned segments the way the plane draws a jump link.
    static void Leader(VisualElement layer, float x0, float y0, float x1, float y1)
    {
        var line = new VisualElement { pickingMode = PickingMode.Ignore };
        line.style.position = Position.Absolute;
        line.style.left = Mathf.Min(x0, x1);
        line.style.top = Mathf.Min(y0, y1);
        line.style.width = Mathf.Max(Mathf.Abs(x1 - x0), 1.0f);
        line.style.height = Mathf.Max(Mathf.Abs(y1 - y0), 1.0f);
        line.style.backgroundColor = LabelInk;
        layer.Add(line);
    }

    // The panel under a header, found by the word on it. A header is a Label and every
    // switch that raises a panel is a Button, so the word cannot be answered to by the
    // switch; the panel is the row's parent, the row being what the header stands in.
    static VisualElement Panel(VisualElement root, string title)
    {
        var queue = new Queue<VisualElement>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var element = queue.Dequeue();

            if (element is Label label && label.text == title && label.parent != null)
                return label.parent.parent;

            for (var i = 0; i < element.childCount; i++) queue.Enqueue(element[i]);
        }

        return null;
    }

    // Every button in the tree by the word on it, breadth first, which is ShotCapture's
    // own walk and is here for its reason: the transport row is a child of the body and a
    // panel's contents are deeper, so the first button to answer to a word is the switch.
    static Dictionary<string, Button> Buttons(VisualElement root)
    {
        var found = new Dictionary<string, Button>();
        var queue = new Queue<VisualElement>();

        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var element = queue.Dequeue();

            if (element is Button button && !found.ContainsKey(button.text))
                found.Add(button.text, button);

            for (var i = 0; i < element.childCount; i++) queue.Enqueue(element[i]);
        }

        return found;
    }

    static void Want(Dictionary<string, Button> buttons, string name, bool up)
    {
        if (!buttons.TryGetValue(name, out var button)) return;
        if (button.resolvedStyle.backgroundColor.r > ActiveGround == up) return;
        Press(button);
    }

    static void Press(Button button)
      => Invoke.Invoke(button.clickable, new object[] { null });

    static readonly MethodInfo Invoke =
      typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance |
                                            BindingFlags.NonPublic);

    // The chooser on the transport row, rebuilt around a name set from outside it.
    static void RefreshSlots(App.JacquardApp app) => Reach(app, "RefreshSlots");

    // The plane framed on the score with the cursor on its corner, which is what a launch
    // does and what every picture here wants.
    static void ShowScore(App.JacquardApp app) => Reach(app, "ShowScore");

    // One of the UI's own calls, reached from outside the assembly that holds it.
    static void Reach(App.JacquardApp app, string name)
    {
        var field = typeof(App.JacquardApp)
          .GetField("_ui", BindingFlags.Instance | BindingFlags.NonPublic);

        var ui = field?.GetValue(app);

        ui?.GetType()
          .GetMethod(name, BindingFlags.Instance | BindingFlags.Public |
                           BindingFlags.NonPublic)
          ?.Invoke(ui, null);
    }

}

} // namespace Jacquard.Editor
