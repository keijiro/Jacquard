using System;
using System.Globalization;
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
// It was worth a panel at one setting for the reason Global was: a setting with nowhere
// to hang otherwise takes a switch on the transport row, and the row cannot grow a
// switch per setting. The visualizer had one, and it was the only switch up there that
// raised nothing — a question about the app standing among the panels the project is
// made on. This is where that question went, and the auditioning is the next one of its
// kind arriving as a row rather than as a switch the transport had to find room for.
//
// The output volume is the one that tested the rule from the other side. It was built on
// Global first, where the rest of the mix is set and where it plainly seems to belong,
// and it was wrong there for exactly the reason the line above draws: a mix is left at a
// level, but what a hand comes to a volume for is the room it is in, and a room does not
// travel with the file. So it came here, and what settles that kind of question is not
// which panel a setting looks like it belongs on but whether it would mean anything to
// somebody the file is handed to.
//
// The two are not the same shape underneath, and the difference is worth reading. The
// visualizer is pushed: nothing here can reach a MonoBehaviour's enabled flag, so the
// panel keeps the answer and hands it to a callback. The auditioning is pulled: it is
// read at the moment a note would sound and nowhere else, so Audition keeps it and this
// panel only throws the switch. Which way round a setting goes is decided by whether
// anything has to be told when it moves. The volume is pulled as well and by the loop
// that was already asking: JacquardApp reads it once a frame with the rest of the mix
// settings and sends it on when it has moved, so the bar writes a number and nothing
// here knows what happens next.
//
// Stage Mode is both, and in being both it shows that the question is asked once per
// reader rather than once per setting. What it holds off is read at the instant a bar is
// double clicked and nowhere else, which is a pull and is StageMode's; the two buttons it
// takes off the transport row have to be gone as the switch moves rather than the next
// time anything builds a row, which is a push and is JacquardUI's. So it is kept where
// Audition is kept and hands the press on the way the visualizer does, and there is still
// only one copy of it. What this panel does not do is put the app into that state at
// launch, which is the one thing it does for the visualizer: the state is not the panel's
// to hold, so JacquardUI reads it for itself and there is no order for the two of them to
// get wrong. See StageMode.
//
// Under all of it is the one button here that sets nothing, and what it does is leave
// the app for a moment: it is the folder the scores are written to, handed to whatever
// the desktop opens folders with. It belongs on this panel for the reason the settings
// above it do — where a file lands is a fact about this machine and not about the piece
// — and it is a desktop's alone, since only a desktop has a window a folder can be
// shown in.
//
// The user guide used to stand over it, on the same argument and rightly: where the
// thing that explains the app is kept is not a fact about the piece either. What that
// argument never settled was whether anybody would look here for it, and the answer was
// no — so the guide is an icon at the right end of the transport row now and this is
// the only button left. JacquardUI.BuildTransportRow argues where it went, and
// JacquardUI.GuideUrl carries what used to be written here about the page it opens.
//
// It comes up in the middle of the screen, the way Global does and for the same reason:
// nothing here is read against the plane, so there is no edge it wants to be near, and
// the middle is what says a panel is not part of the arrangement around the score.

sealed class SystemPanel
{
    public VisualElement Root { get; }

    // apply is what the visualizer setting actually does, since nothing here owns the
    // thing it is about: this panel holds what was chosen and hands it over. stage is the
    // same road for the mode below, which owns its own state but not the row it empties.
    // Those two are the settings that need telling — the others are read where they are
    // used. refocus is the keyboard going back to the plane, which every panel with a
    // button on it has to give back.
    //
    // store is only read for where it keeps its files, and only on the platforms that
    // can show a folder; it is taken whatever the platform, since a constructor that
    // changes shape with the build target is a call site that has to know about
    // platforms too.
    public SystemPanel(ProjectStore store, Action<bool> apply, Action<bool> stage,
                       Action refocus)
    {
        (_apply, _stage, _refocus) = (apply, stage, refocus);

        Root = Controls.Panel("System");

        // The shape a tile's Play switch has: the state is written on the button as well
        // as shown by its fill, so it reads at a glance and turns over in one press
        // rather than in a chooser's two.
        _visualizer = Controls.Push("", ToggleVisualizer, 44);

        var row = Controls.Row();
        row.Add(Controls.Caption("Visualizer"));
        row.Add(_visualizer);
        Root.Add(row);

        // The second question of this kind, and it arrives as a row rather than as
        // another switch on the transport, which is what this panel exists for.
        //
        // It hands nothing to _apply, because nothing has to be told when it moves:
        // where it is read is at the instant an edit would sound a note, so the switch
        // and the reader meet in Audition and not here. The state is therefore Audition's
        // and not the panel's — there is no second field of it to fall out of step.
        _audition = Controls.Push("", ToggleAudition, 44);

        var audition = Controls.Row();
        audition.Add(Controls.Caption("Audition"));
        audition.Add(_audition);
        Root.Add(audition);

        // How loud the whole thing leaves, which is here rather than on Global for what
        // this panel is: a mix is left at a level, but what a hand comes to this bar for
        // is the room it is in — a pair of headphones at midnight, a speaker across a
        // desk, the phone it was carried out on. None of that travels with the file, and
        // a volume that did would arrive on somebody else's machine as an instruction
        // about their room. So the piece stays at full scale wherever it is opened and
        // this says how loud that is played here. See OutputVolume.
        //
        // Above the buffer size and below the two switches, which is the order the three
        // are reached for: this one is come back to, and the one under it is set once on
        // a machine and left. It is a bar rather than a pair of arrows for the reason the
        // one under it is — a level is an amount and not a list.
        Root.Add(Controls.Bar("Volume", VolumeRange,
                              () => OutputVolume.Decibels,
                              value => OutputVolume.Decibels = value,
                              // A drag crosses every value on the way and each one would
                              // be a write to disk. The hand coming off commits it, the
                              // same bargain the buffer size makes.
                              OutputVolume.Flush));

        // Under it because that is the order the three are met in: what the screen is
        // doing, how loud what comes out of it is, and what the machine underneath can
        // keep up with.
        //
        // A bar rather than the arrows a choice out of a list gets, because this is not
        // a list: it is one number with a low end and a high end, and which end it is
        // near is the whole of what a hand setting it wants to know — a short buffer is
        // an instrument that answers immediately and drops out on a busy frame, a long
        // one is the other bargain, and everything between them is a position on that
        // trade rather than a name.
        if (DspBuffer.Supported)
        {
            Root.Add(Controls.Bar("Buffer size", BufferRange,
                                  () => DspBuffer.Requested,
                                  value => { DspBuffer.Requested = Mathf.RoundToInt(value);
                                             SyncRestart(); },
                                  // A drag crosses every stop on the way, and each one
                                  // would be a write to disk. The hand coming off is
                                  // what commits it; a typed number settles at once.
                                  DspBuffer.Flush));

            // What the bar cannot say for itself: the audio system takes its figure at
            // boot, so a number chosen here is a number the next launch will use. It
            // stands under the row it is about and only while it is true — a line that
            // is always there is a line nobody reads by the second session.
            _restart = Controls.Caption("Applies at the next launch.");
            _restart.style.width = StyleKeyword.Auto;
            _restart.style.height = StyleKeyword.Auto;
            _restart.style.whiteSpace = WhiteSpace.Normal;
            _restart.style.marginBottom = Controls.Gap;
            Root.Add(_restart);

            SyncRestart();
        }

        // Last of the settings, under the four that are about the app and not about what
        // is being done with it. It was first, on the argument that a mode saying whether
        // the app is being worked on at all stands over the ones saying how it behaves
        // while it is; what is better about it here is that the four above are a list and
        // this is not a member of it. Read down, the panel now says what the screen draws,
        // how loud it leaves, what the machine underneath can keep up with — and then, set
        // apart at the foot of them, whether any of that is being played to a room.
        //
        // It also stops being the row a hand meets first on the way to the volume, which
        // is the row on this panel most often come back to. A switch that turns the app
        // over is worth reaching past the settings for rather than through them.
        //
        // What it costs is one thing worth writing down: the restart note above it comes
        // and goes, so this row sits a line lower whenever the buffer size has been moved
        // and not yet taken up. That is a switch that shifts under a hand which changed
        // the buffer a moment ago, and it is accepted because the note is up only until
        // the next launch and the two are never reached for together.
        //
        // The same shape as the two switches above, and deliberately not a louder one. A
        // mode that announced itself with a colour of its own would be one more thing to
        // learn about switches that are all read the same way, and what says this one is
        // on is not the button anyway: it is the gap where Save was. See StageMode.
        _stageMode = Controls.Push("", ToggleStage, 44);

        var stageRow = Controls.Row();
        stageRow.Add(Controls.Caption("Stage Mode"));
        stageRow.Add(_stageMode);
        Root.Add(stageRow);

#if UNITY_EDITOR || UNITY_STANDALONE
        // Both halves of the condition are meant. A standalone player is a machine with
        // a file manager on it; the editor is one as well whatever it is currently
        // building for, and a row that vanished from the editor the moment the target
        // was set to iOS would be a control nobody could try. Everywhere else — the
        // phone, the tablet, the browser — there is no folder to be shown and the row
        // is not built at all, rather than built and dimmed: a dimmed control says
        // *not now*, and this one is *not here*.
        //
        // At the foot, since it is not one of the settings above it and does not want
        // to be read as the end of that list: what it does is leave the app for a
        // moment. It was a plain row while the guide stood over it carrying the air
        // that says so; with the guide gone it is the first thing down here and takes
        // that air itself. On a build where this is not compiled in, the last setting
        // is the last thing on the panel and there is no foot to be missing.
        var foot = Controls.Foot();
        foot.Add(Controls.Push("Open score folder",
                               () => { OpenFolder(store); _refocus(); }, 108));
        Root.Add(foot);
#endif

        // The one line on this panel that is not a setting, and the only thing on the
        // screen that says which copy of the app is running. Nothing dresses it as a
        // control, because there is nothing to do to it: no caption column, no readout
        // beside one, no box — a line of text at the foot and that is all.
        //
        // Which is why it is written the way it is. It was a caption and a value first,
        // the shape every row above it has, and the shape was the mistake: a name in the
        // caption column with a figure in the bright text a value is written in reads as
        // a control whose control is missing, and the bright text is the app saying
        // something matters. So the whole line is in the caption grey — the name and the
        // figure together, since a version number is not a value anybody set — and that
        // grey is most of what says *this is here if you need it*, which is exactly what
        // a version is.
        //
        // It starts at the left edge, where every name on this panel starts. Tried at the
        // right it read as a mark set into the corner — a thing placed there, which is one
        // more decision the eye has to take in on a panel whose whole left edge is already
        // a column of names running down it. At the left it is the last of those names and
        // the column simply ends, and what keeps it from being read as another setting is
        // the grey and the air over it rather than a position of its own.
        //
        // It belongs on this panel for the reason the settings above it do — a version is
        // a fact about this copy on this machine and would mean nothing to anybody the
        // file is handed to — and it is at the foot on every platform, under the row that
        // is a desktop's alone. Last because of what it is for: it is looked at when
        // something is being said *about* the app rather than done with it, which is the
        // one errand nobody opens this panel to run.
        //
        // The figure is asked of the player rather than written here. Application.version
        // is the bundle version the build was stamped with, so the number on screen is the
        // number that was shipped and there is no second copy of it in the source to be
        // raised a release late.
        var version = Controls.Text("Version " + Application.version,
                                    Controls.FontSize, Style.Label);
        version.style.unityTextAlign = TextAnchor.MiddleLeft;
        version.style.marginTop = Controls.GroupGap;
        // The gap the panel's short bottom inset is counting on, which every row above
        // carries for itself and this line is not a row to carry.
        version.style.marginBottom = Controls.Gap;
        Root.Add(version);

        // Read once, here, and handed straight on. The panel is where the setting is
        // kept, so it is also what puts the app into the state it was left in: the
        // caller has nothing to remember, and there is no second place the default is
        // written down to disagree with this one.
        _visualizerOn = PlayerPrefs.GetInt(VisualizerKey, 1) != 0;

        Sync();
        _apply(_visualizerOn);
    }

    // Private members

    readonly Action<bool> _apply;
    readonly Action<bool> _stage;
    readonly Action _refocus;
    readonly Button _stageMode;
    readonly Button _visualizer;
    readonly Button _audition;
    readonly Label _restart;

    bool _visualizerOn;

    // Down from full scale, and curved where the limiter's threshold is straight — the
    // difference between the two is what each is for rather than a difference of taste.
    // Every position on that one is a different sound and its far end is the most extreme
    // one, so a decibel there is worth the same travel wherever it is taken. This bar is
    // not that. What a hand comes to it for is a trim — a decibel or two, so the piece
    // sits against a room or a pair of headphones — and below about twenty down there is
    // nothing left to choose: -40 and -46 are both quiet, and neither is a setting
    // anybody arrives at on purpose. Straight, the part that is played was the top sixth
    // of the bar and six decibels of it were a tenth, which is what made it abrupt — the
    // fiftieth of the range a hand can move on the lift alone, which ValueBar.OnPointerUp
    // exists to give back, was a decibel and a quarter of trim on this one.
    //
    // So the exponent spends the travel where the choosing happens. Below one, where the
    // two curved bars in ParamRanges are above it — those give their bottom ends the room
    // because that is where their sounds are, and this one gives its top end the room for
    // the same reason. At 0.4 the first six decibels take a quarter of the bar instead of
    // a tenth, a pixel is worth a sixth of a decibel up there against half of one at
    // thirty down and a whole one at the foot of it, and the middle of the travel lands
    // at -14.5dB — which is about where a mixing desk puts the middle of a fader, arrived
    // at from the same argument.
    //
    // There is nothing above unity on it. What reaches the volume has been through the
    // soft clip, so it cannot be over full scale and cannot be made louder without asking
    // the device to square off what the clip was careful to round.
    //
    // The bottom is silence rather than a number, and the readout says so: a bar printing
    // -60.0 dB at the position where nothing is coming out would be telling the truth
    // about the setting and lying about the sound.
    static readonly ValueBar.Range VolumeRange =
      new ValueBar.Range(OutputVolume.MinVolume, 0.0f, curve: 0.4f, digits: 1,
                         unit: "dB",
                         display: v => v <= OutputVolume.MinVolume ? "off"
                                       : v.ToString("F1", CultureInfo.InvariantCulture)
                                         + " dB");

    // On unless it was turned off, which is the other way round from everything raised
    // from the transport row. Those are panels, and a panel that came up by itself would
    // be covering the plane the screen is for. This one is not in the way of anything —
    // it is behind the score and the eye can ignore it — and it is the only thing on
    // screen that says the synth is there before a note has been played. So it is what
    // the app looks like on the first launch, and what is remembered here is somebody
    // deciding they would rather it were not.
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

    // Nothing to apply and nothing to remember: Audition writes itself through and is
    // read wherever a note would sound, so the press is the whole of what happens here.
    void ToggleAudition()
    {
        Audition.On = !Audition.On;

        Sync();
        _refocus();
    }

    // Nothing to remember either — StageMode writes itself through — but something to
    // apply, since the buttons it takes away are on a row this panel cannot reach. The
    // state is read back out of StageMode rather than passed on from the line above it,
    // so that what is handed over is what was stored and not what this method believed
    // it stored.
    void ToggleStage()
    {
        StageMode.On = !StageMode.On;

        Sync();
        _stage(StageMode.On);
        _refocus();
    }

    void Sync()
    {
        _stageMode.text = StageMode.On ? "On" : "Off";
        Controls.SetActive(_stageMode, StageMode.On);

        _visualizer.text = _visualizerOn ? "On" : "Off";
        Controls.SetActive(_visualizer, _visualizerOn);

        _audition.text = Audition.On ? "On" : "Off";
        Controls.SetActive(_audition, Audition.On);
    }

    // The note under the buffer row, up exactly while the setting has moved since the
    // app started — which is what *applies at the next launch* means and all it means.
    // See DspBuffer.Applied for why it is not measured against the buffer in force.
    void SyncRestart()
      => _restart.style.display = DspBuffer.Requested == DspBuffer.Applied
                                  ? DisplayStyle.None : DisplayStyle.Flex;

    // Whole frames, in the steps DspBuffer settles on. No unit on the readout — the
    // caption has already said what the number is, and there is no shorter word for a
    // frame than the figure itself.
    static readonly ValueBar.Range BufferRange =
      new ValueBar.Range(DspBuffer.Min, DspBuffer.Max, snap: DspBuffer.Step, digits: 0);

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
