using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

using CoreProject = Jacquard.Project;

// The Screen the Device Simulator stands in for, which is the one that knows the
// density of the device being previewed. The same class as UnityEngine.Screen
// everywhere else.
using DeviceScreen = UnityEngine.Device.Screen;

namespace Jacquard.App {

// Ties the score, the runners, the synth and the UI together.
//
// Timing comes from the audio clock, so this does not have to run at any particular
// rate: every step is handed over with the exact sample it is to start on, and a
// frame hitch delays the handover rather than the note.

[RequireComponent(typeof(UIDocument))]
public sealed class JacquardApp : MonoBehaviour
{
    // Public properties

    [field:SerializeField, Range(1, 64)]
    public int MaxVoices { get; set; } = 24;

    // How far ahead of the audio clock the sequencer runs. It only has to cover the
    // gap between two updates.
    [field:SerializeField, Range(0.02f, 0.5f)]
    public float Lookahead { get; set; } = 0.12f;

    // How far ahead of the audio clock a note is actually handed to the synth, which
    // ever since the live effects has been a shorter window than the one above.
    //
    // What a live effect reaches is what has not been handed over, so the two windows
    // being the same one is what would make a press take a step to be heard: at 129bpm
    // a sixteenth is 116ms against a lookahead of 120. So the sequencer still runs the
    // full window ahead and LiveFx parks what it produces, and a note leaves the
    // queue only once it is nearly due. Nothing moves but the moment of the handover
    // — the sample a note starts on was decided by the runner and is never touched —
    // so the sequence is as exact as it was and a press is heard on the next note.
    //
    // What it costs is the margin against a frame that takes too long, which was the
    // whole window and is now this. A note handed over late is not played late: the
    // pool triggers it against the clock, so what a hitch takes is the head of a note
    // rather than its place in the bar. Underneath this the driver's own floor still
    // applies, which on the Web is most of a tenth of a second and leaves the margin
    // there where it was.
    //
    // It is the floor of that window rather than the window itself: what is used is
    // two frames, and this is what two frames may not fall below. See HandoverSeconds,
    // which is where the whole of that argument is.
    [field:SerializeField, Range(0.005f, 0.1f)]
    public float LiveLead { get; set; } = 0.03f;

    // The score a fresh install is given, as a file rather than as code. What it holds
    // is a real piece of work — eight patches and a handful of lanes — and the one
    // thing that is certain about it is that it will be replaced again. Written out by
    // the app's own Save and copied in, so replacing it is a copy rather than a
    // transcription, and the format is exercised by the same reader every load uses.
    //
    // A double extension because Unity imports a TextAsset by extension and .jacquard
    // is not one it knows; the alternative is a ScriptedImporter for one file.
    //
    // It is not what the app opens on any more, and it is read exactly once in the life
    // of an install: the first launch writes it into the first slot of the score folder
    // and every launch after that opens whatever is in that folder. So this is the
    // contents of a slot rather than a startup score, and what it is called says so.
    //
    // Nothing here is a fallback for a missing asset beyond the initial score: leaving
    // the field unassigned is how the app is started with nothing made in it, which is
    // the only other thing this used to be able to do.
    [field:SerializeField]
    public TextAsset SampleScore { get; set; }

    // The wordmark at the left of the transport row. A bitmap and not a Painter2D
    // drawing like every other mark in here: the type is a pixel font already, so
    // what would be drawn is the same grid of squares the texture holds, and it is
    // cut from the same source the logo and the app icon are.
    //
    // Unassigned is a row that simply starts at Play, which is what every build
    // before this one looked like.
    [field:SerializeField]
    public Texture2D Logo { get; set; }

    // What every word on screen is set in, from the transport row down to the note
    // names in the cells. It is put on the root and inherited from there rather than
    // named by each control, since a control that chose its own face would be a place
    // for this to go wrong one element at a time.
    //
    // Unassigned is the theme's own face, which is what this ran on until now, so a
    // scene built without it still comes up.
    [field:SerializeField]
    public Font Font { get; set; }

    // How big a unit of this interface comes out is not settled here any more. It
    // belongs to the panel settings asset, which holds a constant physical size
    // against a reference of 132 dots to the inch — so a unit is a hundred and
    // thirty-second of an inch on whatever the app is running on, and a control sized
    // for a fingertip stays that size.
    //
    // It used to be a whole number written over that asset from here. The reasoning
    // was that the grid is drawn in whole pixels — 34x36 cells, 1px chains, half-pixel
    // aligned icons — and a fractional scale smears every hairline; two was right for
    // the retina screens this had met, and there was nothing in it to say what was
    // right for one it had not. A density answers that without being told. What the
    // whole number bought was crispness, and what it cost was being wrong the first
    // time the assumption under it did not hold, which for a touch target is the more
    // expensive of the two.

    // What the display is asked for. Sixty is what a hand dragging the plane needs
    // and what every screen this runs on can hold; a tablet that offers more can be
    // told to, but nothing here is drawn often enough to want it.
    [field:SerializeField, Range(30, 120)]
    public int FrameRate { get; set; } = 60;

    // Which set of control metrics the chrome is built from. Auto is what ships; the
    // other two are for seeing the tablet's layout on the machine it is written on,
    // which is the only way to judge it without a build.
    [field:SerializeField]
    public PointerKind Pointer { get; set; } = PointerKind.Auto;

    // How much larger than life the plane is drawn while the promotional reel runs —
    // see PromoDirector. The reel is filmed rather than used, so the size that matters
    // is the one a tile comes out on a video and not the one a finger wants.
    [field:SerializeField, Range(1.0f, 4.0f)]
    public float PromoScale { get; set; } = 2.0f;

    // How long a tile takes to move from one picture to the next. It is spent at the
    // end of a lap and lands on the beat, so anything much past half a lap begins
    // before the lap it belongs to is over.
    [field:SerializeField, Range(0.05f, 1.0f)]
    public float PromoTransition { get; set; } = 0.25f;

    // Runtime state

    public CoreProject Project { get; private set; }
    public Sequencer Sequencer { get; private set; }
    public LiveFx Live { get; private set; }

    // The background drawing, or nothing in a scene without one. It is a component
    // rather than something this owns, so what the UI switches is its enabled flag.
    public Visualizer Visualizer { get; private set; }
    public FmSynth Synth { get; private set; }
    public ScoreEditor Editor { get; private set; }
    public ScoreView View { get; private set; }
    public ProjectStore Store { get; private set; }
    public FmSynthStatus Status { get; private set; }

    // The promotional reel, which is not part of the app: it takes it apart and puts it
    // back. See PromoDirector.
    public PromoDirector Promo { get; private set; }

    // The viewport the plane is looked at through, which the reel pins.
    public ScrollArea Scroll => _ui?.Scroll;

    // Whatever the last file operation had to say.
    public string Message { get; private set; }

    // Transport

    public void TogglePlay()
    {
        if (Sequencer.IsPlaying)
        {
            Sequencer.Stop();
            Live.Stop();
        }
        else
        {
            // Read once, so that the grid a live effect counts its sixteenths from
            // is the sample the first step lands on and not one beside it.
            var now = Synth.CurrentSample;
            Sequencer.Play(now, LookaheadSamples);
            Live.Start(now + LookaheadSamples);
        }

        View.RefreshPlayheads();
    }

    // Starts the transport from the top and answers the sample the first step lands
    // on, which is the line every lap of the reel is counted from.
    //
    // The same two calls the play branch above makes, with the sample handed back: the
    // reel has to know where its own clock starts, and reading it afterwards would be
    // reading a clock that has since moved.
    public long RestartTransport()
    {
        Sequencer.Stop();
        Live.Stop();

        var now = Synth.CurrentSample;

        Sequencer.Play(now, LookaheadSamples);
        Live.Start(now + LookaheadSamples);

        View.RefreshPlayheads();

        return now + LookaheadSamples;
    }

    // The promotional reel

    // Takes the app apart for the reel: the chrome comes off, the plane loses the
    // cursor and is drawn larger than life, and the score is replaced outright.
    //
    // The score goes in without waiting for a lap line. A seam is what two pieces of
    // music want between them and this is a piece beginning, so the transport is
    // restarted over the top of it by the caller.
    public void EnterPromo(CoreProject project)
    {
        _ui.SetChrome(false);
        View.ShowCursor = false;

        SetPanelScale(PromoScale);

        AdoptOutright(project);
    }

    // And puts it back, with the score that was being edited when the reel went up.
    public void LeavePromo(CoreProject project)
    {
        Sequencer.Stop();
        Live.Stop();

        SetPanelScale(0.0f);

        _ui.SetChrome(true);
        View.ShowCursor = true;

        AdoptOutright(project);
        View.RefreshPlayheads();

        // After the score is back, since this aims at where it has come to rest: the
        // plane has spent the reel pinned to a lane that is no longer on it.
        _ui.ShowScore();
    }

    // A project put in with nothing reconciled and nobody waited for, which is what a
    // score arriving from outside the app's own editing is.
    void AdoptOutright(CoreProject project)
    {
        Project = project;
        Sequencer.Project = project;
        Editor.Adopt(project);
    }

    // Two keys, read off the keyboard rather than off the plane: putting the chrome
    // away moves the focus about, and the one key that puts it back has to work whatever
    // the focus is. Anything being typed into keeps its letters.
    void ReadTheReelKeys()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || _ui.Typing) return;

        if (keyboard.pKey.wasPressedThisFrame) Promo.Toggle();
        else if (keyboard.escapeKey.wasPressedThisFrame) Promo.Stop();
    }

    // Draws the interface at a fixed multiple of a screen pixel instead of at the size
    // a hand wants, and gives it back. Zero is the panel the app came up with.
    //
    // A copy rather than the asset, because the asset is on disk and the editor would
    // keep whatever was written into it.
    void SetPanelScale(float scale)
    {
        var ui = GetComponent<UIDocument>();

        if (scale > 0.0f)
        {
            _beforePromo ??= ui.panelSettings;

            _promoPanel = Instantiate(_beforePromo);
            _promoPanel.scaleMode = PanelScaleMode.ConstantPixelSize;
            _promoPanel.scale = scale;

            ui.panelSettings = _promoPanel;
            return;
        }

        if (_beforePromo == null) return;

        ui.panelSettings = _beforePromo;
        _beforePromo = null;

        if (_promoPanel != null) Destroy(_promoPanel);
        _promoPanel = null;
    }

    // Files

    public void Save() => Message = Store.Save(Project);

    // Opens a score without stopping for it.
    //
    // The file is read now and the score comes in at the turn of the piece: the
    // sequencer holds it until the master lane comes round and then plays straight on
    // into it, so the two scores run together as if they had been written that way. The
    // transport is not touched and neither is LiveFx — its queue is holding the last of
    // the outgoing score at that moment, and emptying it is exactly the hole this is
    // here to avoid.
    //
    // Reading the file here rather than at the seam is also what keeps the seam cheap:
    // what happens at the lap line is a swap of two references and a rebuild of the
    // plane, with the parsing already done a bar ago.
    //
    // A request cannot be taken back, so a second press is not a second request. What
    // is left to a hand that has changed its mind is Stop, which ends the run the
    // switch was waiting on and lets the score in at once.
    public void Load()
    {
        if (Editor.Locked) return;

        var project = Store.Load(out var message);
        Message = message;

        if (project == null) return;

        // Before the request, since a score arriving while the transport is stopped
        // lands inside this call and gives the lock straight back.
        Editor.Locked = true;

        Sequencer.SwitchTo(project);

        // The file controls have nowhere to say anything but the console, so a load
        // that has not landed says so rather than reading as one that has.
        if (Sequencer.IsSwitchPending) Message = message + ", in at the turn of the piece";
    }

    // The sequencer has changed hands. It did so ahead of the clock, by however much of
    // the lookahead was left, so the screen is not moved yet: what is drawn is what is
    // heard, and what is heard for a little longer is the score that is going.
    void OnSwitched(long sample)
    {
        _adoptAt = sample;
        FollowTheSwitch();
    }

    // Puts the score that is now playing on the plane, once it is the score that is
    // sounding. The wait is a fraction of a second and the plane is dim for the whole
    // of it, so the moment the music turns over is the moment the plane comes back.
    void FollowTheSwitch()
    {
        if (_adoptAt is not long at || Synth.CurrentSample < at) return;

        _adoptAt = null;

        Project = Sequencer.Project;
        Editor.Adopt(Project);
        Editor.Locked = false;
    }

    // The score the app comes up in, which is a file on disk like every other score
    // this app has ever saved.
    //
    // Three things in order, and each is only interesting when the one before it has
    // nothing to say. The folder is filled if it is empty, which happens once in the
    // life of an install. The name is whatever the app was last left in, which the
    // store checks against the folder rather than trusting. And then it is loaded, the
    // same way pressing Load loads one — the seam a running score is switched at is not
    // wanted here, since there is nothing yet to play across.
    //
    // A file that will not read is not worth stopping for: there is a whole app behind
    // it that works without one, so it comes back as an initial score with something to
    // say, which is what the status line is for.
    CoreProject OpeningScore()
    {
        Store.Seed(ReadSampleScore);
        Store.Name = Store.Opening();

        var project = Store.Load(out var message);
        Message = _sampleProblem ?? message;

        return project ?? CoreProject.CreateInitial();
    }

    // The sample, as a score, for the one launch that has a folder to fill. Nothing
    // here is worth stopping for either — a slot the sample cannot be put in gets the
    // initial score like the eight beside it — but it is worth saying, since a sample
    // asset left behind by a format bump is a thing to be told about once.
    CoreProject ReadSampleScore()
    {
        if (SampleScore == null) return null;

        try
        {
            return ProjectFormat.Read(SampleScore.text);
        }
        catch (System.Exception error)
        {
            Debug.LogException(error);
            _sampleProblem = "could not read " + SampleScore.name + ": " + error.Message;
            return null;
        }
    }

    // Coming back to the front. What was in the score folder is no longer known: the
    // app has been away, and away is exactly where a file manager, a sync client or a
    // desktop's own Finder window does its work — the System panel hands somebody that
    // folder on purpose. So the chooser is built again from what is there now.
    //
    // Both callbacks, because which of the two arrives is the platform's business: iOS
    // sends the pause, a desktop sends the focus, and either way this is cheap and
    // reaches the same place. Nothing happens before the UI exists, since focus is
    // delivered to a component that has not been started yet.
    void ReadTheFolderAgain()
    {
        if (_ui == null) return;

        // A folder emptied while the app was away is filled again rather than left
        // empty, by the same rule that filled it in the first place: the chooser has to
        // have something on it.
        Store.Seed(ReadSampleScore);

        _ui.RefreshSlots();
    }

    // Going away, on a platform that stops the app for it.
    //
    // The transport stops, and it stops here rather than being left to come back on its
    // own. Nothing about a run survives the gap: the app is not called while it is away,
    // so no note is scheduled for however long it lasts, and the audio system it comes
    // back to is not the one it left — the clock has moved somewhere else and the path
    // down to the mix has to be measured again. A sequencer that thinks it is still
    // playing across all that is a sequencer that comes back with a fistful of notes
    // whose moment went by while the screen was off, and plays them all at once with
    // their fronts cut off.
    //
    // So the piece ends at the edge, and Play starts it again from the top. That is a
    // real cost — it is not where the hand left it — and it is the price of the piece
    // coming back sounding like itself rather than like the wreck of itself.
    //
    // The pause callback and not the focus one. iOS sends the pause, which is the
    // platform that stops; a desktop sends focus, and a desktop losing focus is a
    // window behind another window with the music still playing, which is right.
    void GoQuietForTheBackground()
    {
        if (!Sequencer.IsPlaying) return;

        Sequencer.Stop();
        Live.Stop();
        View.RefreshPlayheads();
    }

    // MonoBehaviour implementation

    void Start()
    {
        // iOS hands out thirty frames a second unless it is asked for more, and vsync
        // is not what governs there. The plane is panned by dragging it, so the score
        // is under a fingertip the whole time a hand moves: at thirty it visibly
        // trails the finger, which is the one thing a direct manipulation cannot do.
        // Set on every platform, since where a desktop's vsync already rules this is
        // simply ignored.
        Application.targetFrameRate = FrameRate;

        // Before anything is built, since every control reads its size as it is made.
        Controls.LayOutFor(Pointer);

        // The store before the score, since the score is now one of its files.
        Store = new ProjectStore();
        Project = OpeningScore();

        // Before the synth and not after it. What this may do is reinitialize the audio
        // system, and the moment for that is while nothing has been allocated against
        // the figure it is replacing: the pipeline sizes its mix buffer from the format
        // it is handed and reads the buffer length once for the dropout detector, both
        // of which happen in the constructor on the next line. In the ordinary case —
        // the stored number being the one Unity booted with — it does nothing at all.
        DspBuffer.Apply();

        Synth = new FmSynth(MaxVoices);
        Sequencer = new Sequencer { Project = Project };
        Live = new LiveFx();

        View = new ScoreView { Score = Project.Score, Sequencer = Sequencer };

        Editor = new ScoreEditor
          { Project = Project, Sequencer = Sequencer, Synth = Synth, View = View };

        // After the editor and the view, since following a switch means pointing both
        // of them at what the sequencer has taken up.
        Sequencer.Switched += OnSwitched;

        Visualizer = GetComponent<Visualizer>();

        // The UXML holds nothing but a full-height root to build into. Adding to the
        // document root instead would put the chrome below that element rather than
        // inside it, and the two would then divide the screen between them.
        var ui = GetComponent<UIDocument>();

#if UNITY_EDITOR
        StandInForTheDevice(ui);
#elif UNITY_WEBGL
        FollowTheBrowser(ui);
#endif

        var document = ui.rootVisualElement;
        _ui = new JacquardUI(document.Q("root") ?? document, this);

        // Last, since what it takes apart is everything above it.
        Promo = new PromoDirector(this);
    }

    void Update()
    {
        // First, because this is what moves the audio clock on a driver that has no
        // audio thread of its own, and everything below reads that clock.
        Synth.Pump();

        // Next, because a score that has taken over is the score the rest of this frame
        // is about: the plane, the panels and the mix all read the project this puts in.
        FollowTheSwitch();

        // Before the scheduler and after the clock: what the reel does at a lap line is
        // rewrite the score the next line below is about to read, and the whole of its
        // timing is that it does so in the frame before that line rather than after it.
        ReadTheReelKeys();
        Promo.Tick();

        // Run the sequence as far ahead as it has always run, but park what comes out
        // rather than handing it straight over: a live effect reaches what has not
        // been handed over yet, so the handover waits until a note is nearly due.
        var now = Synth.CurrentSample;

        _pending.Clear();
        Sequencer.Schedule(now, LookaheadSamples, Synth.SampleRate, _pending);
        Live.Enqueue(_pending);

        _released.Clear();
        Live.HandOver(now + LiveLeadSamples, Project.Tempo, Synth.SampleRate,
                       _released);

        foreach (var note in _released) Synth.Schedule(note);

        // Hand over the mix settings whenever they are not what was handed over last.
        // One comparison covers every way they can change — a bar on the Send panel, the
        // Global one or the System one, the tempo the delay is locked to, a project
        // loaded over the top of this one — so none of those has to know that anything
        // downstream cares. The volume is the one term that is not the project's, and it
        // rides here because what the audio thread is owed is the settings as they now
        // stand, whoever they belong to.
        var fx = MixFxRuntime.FromSettings(Project.Fx, Project.Limiter,
                                           OutputVolume.Decibels, Project.Tempo,
                                           Synth.SampleRate);

        if (!fx.Equals(_fx))
        {
            Synth.SetFx(fx);
            _fx = fx;
        }

        Status = Synth.GetStatus();
        _ui.Update();

#if UNITY_WEBGL && !UNITY_EDITOR
        // Last, because a page's ratio is not settled the way a device's density is:
        // it moves under a running app.
        FollowTheZoom();
#endif
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused) ReadTheFolderAgain();
    }

    void OnApplicationPause(bool paused)
    {
        // A screen locked while the app is still coming up delivers this to a component
        // that has not had a Start yet, and everything below is built in that Start.
        if (Synth == null) return;

        if (paused) { GoQuietForTheBackground(); return; }

        // Before the folder, and long before a hand can reach Play: the measurement
        // takes a tenth of a second of frames and the transport is stopped for all of
        // it, so by the time anybody asks for a sound the lead is this machine's again.
        Synth.Recalibrate();

        ReadTheFolderAgain();
    }

    void OnDestroy()
    {
        Synth?.Dispose();
        if (_promoPanel != null) Destroy(_promoPanel);
#if UNITY_EDITOR || UNITY_WEBGL
        if (_panelCopy != null) Destroy(_panelCopy);
#endif
    }

#if UNITY_EDITOR

    // Editor preview

    // Makes the editor resolve the scale the device would, which it does not do by
    // itself.
    //
    // This is UUM-136603, a regression in 6.3 that is fixed in 6000.6.0a5 and closed
    // as won't-fix on the 6.3, 6.4 and 6.5 streams — this project is on the 6.5 stream, so
    // it is on the wrong side of that. The simulator shims Screen properly: with an
    // iPhone 13 Pro Max selected Screen.dpi reads 458, the safe area is the phone's
    // and the target is 2778x1284. What does not arrive is the panel's own density.
    // Read off PanelSettings by reflection with that phone on screen, the figure it
    // was resolving against was 303 — the DPI of the Mac's display, which the panel
    // takes from whichever monitor the view is on and which has nothing to do with the
    // device being previewed. A physical size held against the wrong screen: the
    // preview came out at 1210x559 units where the phone gives 802, every control two
    // thirds of the size it will really be.
    //
    // What is done about it is to stop asking the panel to resolve a density at all
    // here. The same sum is done with the DPI the simulator does shim, and handed over
    // as a constant pixel size, which is the one mode that takes the number instead of
    // looking one up. That is also why this is a fix and not a patch: the bug report
    // records it as not reproducible under constant pixel size, so converting is
    // stepping off the broken path rather than correcting a value it produced. The
    // workaround that stays in physical size and folds a ratio into the scale has to
    // be re-applied on a timer, because what it is correcting flips on its own.
    //
    // Outside the simulator Screen.dpi is the display's own, so a plain Game View
    // resolves exactly what it resolved before.
    //
    // On a copy of the settings, not the asset. A PanelSettings written to in play
    // mode is an asset written to on disk, and an asset carrying a value the app puts
    // there is how its scale came to disagree with the one actually in force, twice
    // over. The asset stays the only thing a player reads, and says what it means.
    //
    // Read once, so changing the simulated device means entering play mode again. The
    // control metrics are settled at build time too, so there was never going to be a
    // way to swap devices without it.
    //
    // All of this can go when the editor this is built with has the fix.
    void StandInForTheDevice(UIDocument ui)
    {
        var settings = ui.panelSettings;
        if (settings == null ||
            settings.scaleMode != PanelScaleMode.ConstantPhysicalSize) return;

        var dpi = DeviceScreen.dpi > 0.0f ? DeviceScreen.dpi : settings.fallbackDpi;

        _panelCopy = Instantiate(settings);
        _panelCopy.scaleMode = PanelScaleMode.ConstantPixelSize;
        _panelCopy.scale = dpi / settings.referenceDpi;
        ui.panelSettings = _panelCopy;
    }

#endif

#if UNITY_WEBGL && !UNITY_EDITOR

    // The browser

    // Sizes the interface by the browser's own unit, because a browser will not say how
    // dense its screen is.
    //
    // Web has no DPI to ask for. There is no binding for one anywhere in the platform's
    // JavaScript, and what `Screen.dpi` answers is 96 — the density a CSS pixel is
    // nominally defined against — multiplied by the device pixel ratio the runtime
    // applied to the canvas. Measured in Chrome at ratios of one, two and three:
    // 96, 192 and 288.
    //
    // Held against a reference of 132 that resolves to 0.727, 1.455 and 2.182 pixels to
    // the unit — and since the drawing buffer is the same ratio larger than the page, the
    // ratio cancels and **a unit is 0.727 CSS pixels on every display there is**. The
    // interface does not come out a hundred-and-thirty-secondth of an inch anywhere; it
    // comes out at whatever three quarters of the browser's own unit happens to measure,
    // which on a Mac in More Space is 0.122mm against the 0.192mm the iPad gets, and
    // on an iPad in Safari is 0.140mm — a 30pt touch row landing at 21.8pt, under the
    // 20pt one the desktop profile would have given it.
    //
    // So the physical size is given up here rather than corrected, exactly as it is for
    // the simulator above, and the panel is handed the ratio as a constant pixel size:
    // **one unit, one CSS pixel.** That is not a fudge factor. A CSS pixel is the
    // browser's device-independent unit, and on iOS it is precisely one iOS point — an
    // iPad's is a hundred-and-thirty-secondth of an inch, which is this project's
    // reference DPI exactly. The chrome therefore comes out the size the native build
    // gives it on the one platform where both exist, which is the arithmetic the touch
    // metrics rest on, and everywhere else it is the size the page said. Browser zoom
    // then works on the interface as well as on the score, since zooming is a change to
    // the ratio.
    //
    // Dividing 96 back out is how the ratio is read from C#, and it is the ratio that
    // matters rather than `window.devicePixelRatio`: what `Screen.dpi` carries is the one
    // the runtime actually applied, so a page that turns off canvas matching or pins the
    // ratio is followed rather than contradicted.
    //
    // On a copy of the settings, for the reason the editor path gives: the asset is what
    // the app is configured by and never a scratch pad. Nothing on the disk of a player
    // is at stake, only the one rule.
    void FollowTheBrowser(UIDocument ui)
    {
        var settings = ui.panelSettings;
        if (settings == null ||
            settings.scaleMode != PanelScaleMode.ConstantPhysicalSize) return;

        _panelCopy = Instantiate(settings);
        _panelCopy.scaleMode = PanelScaleMode.ConstantPixelSize;
        ui.panelSettings = _panelCopy;

        FollowTheZoom();
    }

    // A device's density is settled before the app starts and a page's ratio is not: it
    // changes when the page is zoomed and when a window is dragged to another display.
    // So it is read every frame and written only when it has moved, which is a float
    // comparison against the value this last put there.
    void FollowTheZoom()
    {
        if (_panelCopy == null) return;

        var scale = Screen.dpi / CssDpi;

        // Nothing sensible to do with a zero, and blanking the panel is worse than
        // keeping the last good ratio.
        if (scale > 0.0f && scale != _panelCopy.scale) _panelCopy.scale = scale;
    }

    // The density a CSS pixel is defined against, and the one number in here that is not
    // read from anywhere. It is the constant in the specification, not a property of a
    // screen, which is why it can be written down.
    const float CssDpi = 96.0f;

#endif

    // Private members

    JacquardUI _ui;

    // Whatever the sample file had to say for itself, held until there is a status
    // line to say it on.
    string _sampleProblem;

    // The sample a score that has already taken over starts sounding on, and nothing
    // once the screen has caught up with it.
    long? _adoptAt;

#if UNITY_EDITOR || UNITY_WEBGL
    PanelSettings _panelCopy;
#endif

    // The panel the reel is drawn on, and the one it was drawn on before — see
    // SetPanelScale.
    PanelSettings _promoPanel, _beforePromo;

    readonly List<FmNoteEvent> _pending = new();
    readonly List<FmNoteEvent> _released = new();

    // The last mix settings the synth was given, which is what makes sending them
    // again a comparison rather than a notification from everything that can move one.
    MixFxRuntime _fx;

    // The window, plus however far past the clock the driver's earliest schedulable
    // note lies — which is measured on the machine and is not nothing anywhere.
    long LookaheadSamples
      => (long)(Lookahead * Synth.SampleRate) + Synth.MinimumLead;

    // The same floor under a much shorter window, since a note cannot be handed
    // over closer to the clock than the driver will take one, however late a live
    // effect is read.
    long LiveLeadSamples
      => (long)(HandoverSeconds * Synth.SampleRate) + Synth.MinimumLead;

    // How far ahead of a note this app actually hands it over, which is two frames
    // unless LiveLead asks for more.
    //
    // The handover happens once an Update and takes everything already inside the
    // window, so a note leaves at some point in the frame *before* the one it would
    // have missed: the lead it really gets is the window less however long that frame
    // ran. Cover the driver's own floor as well and the requirement comes out as one
    // line — **this window has to be longer than a frame** — which is a requirement in
    // frames and was written down in milliseconds.
    //
    // Thirty of them is 1.8 frames at sixty a second, and there is a device here that
    // does not always run at sixty: iOS drops the app to thirty when it gets warm and
    // to fifteen when it gets hot, which this project asks for
    // (adjustIOSFPSUsingThermalState). At thirty a frame is 33ms and the window is
    // shorter than the frame that has to fit inside it — every note would then be
    // handed over after its own start and lose its front, which on a patch with a
    // pitch sweep is heard as the sweep beginning part way down. The same fault the
    // background/foreground work was about, arriving by a different road. Measured at
    // fifteen with the window fixed at thirty milliseconds: up to 32.7ms off the front
    // of every note, with the lead trim stepping in a second later to cover it under
    // the wrong name.
    //
    // So the window is read off the frame rate rather than written down. Two frames,
    // smoothed, because one is the bare requirement and the second is the margin: a
    // single frame that runs long is not this — the sequencer answers that one by
    // taking the head off a note rather than moving it — and a rate that has genuinely
    // dropped shows in the average within about a second.
    //
    // Clamped at both ends. LiveLead is the floor, so a display running at 120 does
    // not shrink the reach of a live effect below what was chosen for it; Lookahead is
    // the ceiling, since handing a note over before the sequencer has produced it is
    // not a thing this can do, and at fifteen frames a second two of them would ask
    // for exactly that.
    float HandoverSeconds
      => Mathf.Clamp(2.0f * Time.smoothDeltaTime, LiveLead, Lookahead);
}

} // namespace Jacquard.App
