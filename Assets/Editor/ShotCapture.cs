using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.Editor {

// The screenshots a store asks for, at a size no device here is.
//
// Google Play takes phone, 7-inch and 10-inch screenshots in separate slots and all
// three at 16:9 or 9:16, which the iOS captures are not -- 2688x1242 is 2.16:1 and
// 2732x2048 is 4:3. One size satisfies all three slots at once: 2560x1440 clears the
// 10-inch floor of 1080 per side and the phone ceiling of 3840 together.
//
// Which is why this is not a screenshot. A picture of a window is the size of the
// window, and the Game View is whatever size the editor happens to be -- so asking the
// CLI for one at 2560x1440 returns the Game View's own frame rescaled, laid out for
// whatever dpi that window had. What the store slot wants is a frame laid out at a size
// on purpose, so the interface is pointed at a texture of exactly that size and read
// back off it. See PanelPlate.
//
// The transport is left running while the shutter is open, and that is the whole reason
// this is not PlaneCapture with a different size. The visualizer is a mesh on the
// camera rather than anything on the panel, so it only reaches a texture if the camera
// is pointed at the same one -- PlaneCapture switches it off, which is right for a
// plate of the plane and wrong for a picture of the app, since the trace behind the
// score is a thing the app does that no still of a stopped transport shows. The iOS
// captures were taken the same way and show it.
//
// So a shot is not reproducible, and cannot be: the trace is a different curve every
// frame and the playheads are wherever the piece has got to. What is reproducible is
// the frame around them -- the score, the panels, the size and the scale.
//
// It runs in play mode, off a menu item, and does not put back what it took over. It
// loads sample4 over whatever score was open, leaves the transport stopped and every
// panel down, and writes nothing to disk but the pictures. Nothing here is saved, so
// the cost of that is re-loading the score that was being worked on.
static class ShotCapture
{
    // The piece the shots are of, which is PlaneCapture's for PlaneCapture's reason:
    // sample4 fills more of the plane than the others and every one of the eight
    // channels is written on in it.
    const string ScorePath = "Assets/Jacquard/Scores/sample4.jacquard.txt";

    const string ShotDir = "Branding/shots";

    // The one size that satisfies all three of Play's screenshot slots, argued above.
    const int Width = 2560, Height = 1440;

    // Which makes the frame 1280x720 units of interface. Whole-numbered for the reason
    // PlaneCapture's is -- the grid is drawn in whole pixels and a fractional scale
    // puts hairlines between them -- and 2 rather than 3 because 2 is the scale the
    // panel settings' own fallback dpi names, so a shot is laid out the way the retina
    // tablet that asset was written for lays out. A phone resolves higher and shows
    // less of the plane, which would be a second set of pictures rather than a
    // different number here.
    const int Scale = 2;

    // What a raised switch does to its own ground, which is how this reads back whether
    // a panel is already up rather than tracking presses it did not make. Style is
    // internal to the runtime assembly, so the value is not reachable from here and the
    // test is a threshold instead: the active ground is Style.NoteLine, which is light,
    // and every other state of a switch is dark.
    const float ActiveGround = 0.5f;

    // Two cells of channel 2's lane, which is the lane the shots are composed around
    // because it is the one nearest the top of the score that is written on all the way
    // across. A lane's given position is its first step, so the channel's own start
    // tile is the column before it.
    static readonly GridPoint Note = new GridPoint(18, 11);
    static readonly GridPoint Channel = new GridPoint(17, 11);

    // The set, mirroring the iOS captures. Each names the switches it wants raised and
    // the cell it wants the cursor on, since the cursor is what raises the inspector
    // down the right-hand side -- a shot with the cursor on an empty cell is a shot
    // whose inspector is the six buttons for putting a tile there, which says nothing
    // about a score and is the least interesting panel in the app.
    //
    // One group of switches per shot rather than the two the iPad captures pair up. A
    // 4:3 frame is 1024 units tall and this one is 720, so a panel down each side and a
    // third across the bottom leaves the score in a gap between them; on the iPad the
    // same three had room to stand clear of each other. What was two shots there is
    // three here, and the last is the plane on its own, which is the picture the app is
    // actually for.
    static readonly (string Name, string[] Panels, GridPoint Cursor)[] Shots =
    {
        ("01-score", new string[0], Note),
        // The channel start tile raises the longest panel in the app -- the whole FM
        // voice, a row at a time -- which is the only shot here that says the thing on
        // the plane is a synth and not a picture of one.
        ("02-channel", new string[0], Channel),
        ("03-channels-live-fx", new[] { "Channels", "Live FX" }, Note),
        ("04-send-fx", new[] { "Send FX" }, Note),
        ("05-global", new[] { "Global" }, Note),
    };

    // Every switch on the transport row that raises a panel. Listed rather than
    // discovered, because what makes this the list is that these five are the panels --
    // the buttons either side of them step the score list and are no part of a shot.
    static readonly string[] Switches =
      { "Channels", "Send FX", "Live FX", "Global", "System" };

    [MenuItem("Jacquard/Capture Store Shots")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Capture Store Shots",
                                        "Enter play mode first: what is captured is the "
                                        + "running app.", "OK");
            return;
        }

        var app = Object.FindAnyObjectByType<App.JacquardApp>();

        if (app == null)
        {
            EditorUtility.DisplayDialog("Capture Store Shots",
                                        "No JacquardApp in the scene.", "OK");
            return;
        }

        if (Camera.main == null)
        {
            EditorUtility.DisplayDialog("Capture Store Shots",
                                        "No main camera: the visualizer draws on it, and "
                                        + "without it a shot is a picture of the panel "
                                        + "over nothing.", "OK");
            return;
        }

        // On the app itself, since what this waits for is frames going by and the
        // editor's own update loop is not where they are counted.
        app.StartCoroutine(Capture(app));
    }

    static IEnumerator Capture(App.JacquardApp app)
    {
        var document = app.GetComponent<UIDocument>();
        var settings = document.panelSettings;
        var view = app.View;
        var camera = Camera.main;

        var buttons = Buttons(document.rootVisualElement);

        // The transport switch, held as a reference rather than looked up twice: the
        // word on it is the state, so the name it answers to changes the moment it is
        // pressed.
        buttons.TryGetValue("Play", out var transport);
        if (transport == null) buttons.TryGetValue("Stop", out transport);

        // The score the shots are of, put on the plane the way a load puts one there.
        //
        // The name on the transport row's score list will not follow it, and cannot be
        // made to from here. The list is the score folder read out, and the chooser
        // only re-reads it when JacquardUI tells it to, which is an internal call on an
        // internal class -- so the row goes on naming the slot the app was last left in
        // while the plane holds this. It names a file nobody outside this repository
        // has, so a store screenshot is none the worse for it, and the alternative is
        // reflection against the app's own UI for a word in a box.
        var project = ProjectFormat.Read(File.ReadAllText(ScorePath));
        app.Editor.Adopt(project);

        // Started before anything else and stopped at the end, rather than around each
        // shot. The switch reads "Play" when the transport is stopped and "Stop" when
        // it is running, which is the whole of the state this needs -- there is nothing
        // public here to ask whether the sequencer is going.
        if (transport != null && transport.text == "Play") Press(transport);

        // Long enough for there to be a mix to draw. What the trace shows is the last
        // thirtieth of a second of the output, and the scope holds nothing at all until
        // buffers have come back from a device that was started with the transport --
        // which takes longer than the shutter does. Taken without this, the first two
        // shots came back with a flat ground and the rest had the trace on them, which
        // is a set of pictures of two different apps.
        yield return new WaitForSeconds(1.0f);

        Directory.CreateDirectory(ShotDir);

        var written = new List<string>();

        foreach (var (name, panels, cursor) in Shots)
        {
            foreach (var switchName in Switches)
                Want(buttons, switchName, System.Array.IndexOf(panels, switchName) >= 0);

            view.SetCursor(cursor);

            Texture2D frame = null;

            yield return PanelPlate.Take(settings, Width, Height, Scale, camera,
                                         taken => frame = taken);

            var path = $"{ShotDir}/{name}.png";
            File.WriteAllBytes(path, frame.EncodeToPNG());
            Object.Destroy(frame);
            written.Add(path);
        }

        foreach (var switchName in Switches) Want(buttons, switchName, false);
        if (transport != null && transport.text == "Stop") Press(transport);

        Debug.Log($"wrote {written.Count} shots at {Width}x{Height} ({Scale}x):\n"
                  + string.Join("\n", written));
    }

    // Every button in the tree, by the word on it. All 73 of them are here whether or
    // not the panel each belongs to is up, so the switches this wants are found by
    // walking for them rather than by asking a panel that may be down.
    //
    // Breadth first, and the order is the point rather than an implementation detail:
    // the transport row is a child of the body and every panel's contents are deeper
    // than that, so the first button to answer to a word is the switch and not
    // something inside what the switch raises.
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

    // Raises or lowers one panel by pressing its switch, and only if it is not already
    // where it is wanted -- a switch is a toggle, so a press is not a state.
    static void Want(Dictionary<string, Button> buttons, string name, bool up)
    {
        if (!buttons.TryGetValue(name, out var button)) return;
        if (button.resolvedStyle.backgroundColor.r > ActiveGround == up) return;
        Press(button);
    }

    // Pressing a button from outside the assembly that built it.
    //
    // There is no public way to do this. No event type here has a public parameterless
    // GetPooled -- ClickEvent, NavigationSubmitEvent, PointerDownEvent and
    // PointerUpEvent were each checked and none does -- so VisualElement.SendEvent,
    // which is public, has nothing public to be handed. Clickable.SimulateSingleClick
    // is not public either, and is delayed, which a frame-counting coroutine cannot
    // wait on.
    //
    // What is left is Clickable.Invoke, which is non-public and synchronous. It reads
    // its argument only to decide whether the press came from a pointer, so null is a
    // valid thing to hand it and means it did not.
    //
    // Pressing the switch and not reaching for what it raises: every Show* on the UI
    // also calls Controls.SetActive, so a panel raised behind its switch's back would
    // photograph as a state the app is never in.
    static void Press(Button button)
      => Invoke.Invoke(button.clickable, new object[] { null });

    static readonly MethodInfo Invoke =
      typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance |
                                            BindingFlags.NonPublic);
}

} // namespace Jacquard.Editor
