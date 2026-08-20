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
// Under it is the one button here that sets nothing: the folder the scores are written
// to, handed to whatever the desktop opens folders with. It belongs on this panel for
// the same reason the rest does — where a file lands is a fact about the machine and
// not about the piece — and it is on desktop builds only, since a hand holding a
// tablet has no window to be shown one in.
//
// It comes up in the middle of the screen, the way Global does and for the same reason:
// nothing here is read against the plane, so there is no edge it wants to be near, and
// the middle is what says a panel is not part of the arrangement around the score.

sealed class SystemPanel
{
    public VisualElement Root { get; }

    // apply is what the visualizer setting actually does, since nothing here owns the
    // thing it is about: this panel holds what was chosen and hands it over. It is the
    // one setting that needs it — the others are read where they are used. refocus is the
    // keyboard going back to the plane, which every panel with a button on it has to give
    // back.
    //
    // store is only read for where it keeps its files, and only on the platforms that
    // can show a folder; it is taken whatever the platform, since a constructor that
    // changes shape with the build target is a call site that has to know about
    // platforms too.
    public SystemPanel(ProjectStore store, Action<bool> apply, Action refocus)
    {
        (_apply, _refocus) = (apply, refocus);

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

    // Nothing to apply and nothing to remember: Audition writes itself through and is
    // read wherever a note would sound, so the press is the whole of what happens here.
    void ToggleAudition()
    {
        Audition.On = !Audition.On;

        Sync();
        _refocus();
    }

    void Sync()
    {
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
