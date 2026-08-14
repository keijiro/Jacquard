using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// What is set about the application rather than about the piece.
//
// Everything else on this screen belongs to what is being made: the score, the sounds,
// the mix, what is set across the whole of it. All of that is written into the file and
// comes back with it. What is here is the opposite — it belongs to this copy of the app
// on this machine, it outlasts one project being closed and another being opened, and it
// would mean nothing to anybody the file is handed to. So it is kept where a project
// cannot reach it, in PlayerPrefs, and set on a panel of its own rather than on Global,
// which is the project's own everything-at-once.
//
// One setting so far, and it was worth a panel at one for the reason Global was: a
// setting with nowhere to hang otherwise takes a switch on the transport row, and the
// row cannot grow a switch per setting. The visualizer had one, and it was the only
// switch up there that raised nothing — a question about the app standing among the
// panels the project is made on. This is where that question goes, and where the next
// one of its kind goes without the row noticing.
//
// Under it is the one button here that sets nothing: the folder the scores are written
// to, handed to whatever the desktop opens folders with. It belongs on this panel for
// the same reason the rest does — where a file lands is a fact about the machine and
// not about the piece — and it is on desktop builds only, since a hand holding a
// tablet has no window to be shown one in.
//
// It comes up in the middle of the screen, the way Global does and for the same reason:
// nothing here is read against the plane, so there is no edge it wants to be near, and
// the middle is what says a panel is not part of the arrangement around the score.

sealed class ConfigPanel
{
    public VisualElement Root { get; }

    // apply is what the setting actually does, since nothing here owns the thing it is
    // about: this panel holds what was chosen and hands it over. refocus is the keyboard
    // going back to the plane, which every panel with a button on it has to give back.
    //
    // store is only read for where it keeps its files, and only on the platforms that
    // can show a folder; it is taken whatever the platform, since a constructor that
    // changes shape with the build target is a call site that has to know about
    // platforms too.
    public ConfigPanel(ProjectStore store, Action<bool> apply, Action refocus)
    {
        (_apply, _refocus) = (apply, refocus);

        Root = Controls.Panel("Config");

        // The shape a tile's Play switch has: the state is written on the button as well
        // as shown by its fill, so it reads at a glance and turns over in one press
        // rather than in a chooser's two.
        _visualizer = Controls.Push("", ToggleVisualizer, 44);

        var row = Controls.Row();
        row.Add(Controls.Caption("Visualizer"));
        row.Add(_visualizer);
        Root.Add(row);

#if UNITY_EDITOR || UNITY_STANDALONE
        // At the foot, since it is not one of the settings above it and does not want to
        // be read as the end of that list: what it does is leave the app for a moment.
        //
        // Both halves of the condition are meant. A standalone player is a machine with
        // a file manager on it; the editor is one as well whatever it is currently
        // building for, and a row that vanished from the editor the moment the target
        // was set to iOS would be a control nobody could try. Everywhere else — the
        // phone, the tablet, the browser — there is no folder to be shown and the row
        // is not built at all, rather than built and dimmed: a dimmed control says
        // *not now*, and this one is *not here*.
        var foot = Controls.Foot();
        foot.Add(Controls.Push("Open score folder",
                               () => { OpenFolder(store); _refocus(); }, 108));
        Root.Add(foot);
#endif

        // Read once, here, and handed straight on. The panel is where the setting is
        // kept, so it is also what puts the app into the state it was left in: the
        // caller has nothing to remember, and there is no second place the default is
        // written down to disagree with this one.
        _visualizerOn = PlayerPrefs.GetInt(VisualizerKey, 0) != 0;

        Sync();
        _apply(_visualizerOn);
    }

    // Private members

    readonly Action<bool> _apply;
    readonly Action _refocus;
    readonly Button _visualizer;

    bool _visualizerOn;

    // Off unless it was turned on, which is what everything raised from the transport
    // row starts as: the plane is what the screen is for, and a thing that starts on is
    // a decision nobody made. What is remembered here is somebody deciding otherwise.
    const string VisualizerKey = "Jacquard.Visualizer";

    void ToggleVisualizer()
    {
        _visualizerOn = !_visualizerOn;

        PlayerPrefs.SetInt(VisualizerKey, _visualizerOn ? 1 : 0);
        // Written through rather than left for the quit. A tablet app is not quit, it is
        // put away and then killed off screen, so a setting that only reaches disk on a
        // clean exit is a setting lost by the one ending that always happens.
        PlayerPrefs.Save();

        Sync();
        _apply(_visualizerOn);
        _refocus();
    }

    void Sync()
    {
        _visualizer.text = _visualizerOn ? "On" : "Off";
        Controls.SetActive(_visualizer, _visualizerOn);
    }

#if UNITY_EDITOR || UNITY_STANDALONE
    // Handed to the desktop as a URL, which is all Application.OpenURL takes and is
    // enough on every platform this row is built for: a file URL naming a directory is
    // what a file manager is asked to show.
    //
    // Built through System.Uri rather than by writing "file://" in front of the path.
    // persistentDataPath on macOS runs through "Application Support", and a raw space
    // in a URL is where the handler stops reading; the Uri escapes it, and it is also
    // what turns a Windows path into the three slashes and forward slashes that spelling
    // wants.
    //
    // Made first, since it does not exist until the first save and there is nothing to
    // open until it does. A player who has saved nothing yet gets an empty folder, which
    // is the true answer to where the scores are.
    static void OpenFolder(ProjectStore store)
    {
        var directory = store.Directory;

        try
        {
            System.IO.Directory.CreateDirectory(directory);
            Application.OpenURL(new Uri(directory).AbsoluteUri);
        }
        catch (Exception error)
        {
            // The same road every other file failure here takes. Nothing on screen says
            // so, because the panel has nowhere to say it and the console is where the
            // rest of what the file controls have to say already goes.
            Debug.LogException(error);
        }
    }
#endif
}

} // namespace Jacquard.App
