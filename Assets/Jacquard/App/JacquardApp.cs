using System.Collections.Generic;
using UnityEngine;
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

    // How far ahead of the audio clock notes are handed to the synth. It only has
    // to cover the gap between two updates.
    [field:SerializeField, Range(0.02f, 0.5f)]
    public float Lookahead { get; set; } = 0.12f;

    [field:SerializeField]
    public bool LoadSampleScore { get; set; } = true;

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

    // Runtime state

    public CoreProject Project { get; private set; }
    public Sequencer Sequencer { get; private set; }
    public FmSynth Synth { get; private set; }
    public ScoreEditor Editor { get; private set; }
    public ScoreView View { get; private set; }
    public ProjectStore Store { get; private set; }
    public FmSynthStatus Status { get; private set; }

    // Whatever the last file operation had to say.
    public string Message { get; private set; }

    // Transport

    public void TogglePlay()
    {
        if (Sequencer.IsPlaying)
            Sequencer.Stop();
        else
            Sequencer.Play(Synth.CurrentSample, LookaheadSamples);

        View.RefreshPlayheads();
    }

    // Files

    public void Save() => Message = Store.Save(Project);

    public void Load()
    {
        var project = Store.Load(out var message);
        Message = message;

        if (project == null) return;

        Sequencer.Stop();

        Project = project;
        Sequencer.Project = project;
        Editor.Project = project;
        View.Score = project.Score;

        Editor.Commit();
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

        Project = LoadSampleScore ? CoreProject.CreateSample() : CoreProject.CreateEmpty();

        Synth = new FmSynth(MaxVoices);
        Sequencer = new Sequencer { Project = Project };

        View = new ScoreView { Score = Project.Score, Sequencer = Sequencer };

        Editor = new ScoreEditor
          { Project = Project, Sequencer = Sequencer, Synth = Synth, View = View };

        Store = new ProjectStore();
        Message = Store.Listing();

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
    }

    void Update()
    {
        // First, because this is what moves the audio clock on a driver that has no
        // audio thread of its own, and everything below reads that clock.
        Synth.Pump();

        // Hand over every note that falls inside the lookahead window.
        _pending.Clear();
        Sequencer.Schedule(Synth.CurrentSample, LookaheadSamples,
                           Synth.SampleRate, _pending);

        foreach (var note in _pending) Synth.Schedule(note);

        // Hand over the effect settings whenever they are not what was handed over
        // last. One comparison covers every way they can change — a bar on the Send
        // panel, the tempo the delay is locked to, a project loaded over the top of
        // this one — so none of those has to know that anything downstream cares.
        var fx = SendFxRuntime.FromSettings(Project.Fx, Project.Tempo, Synth.SampleRate);

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

    void OnDestroy()
    {
        Synth?.Dispose();
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
    // as won't-fix on the 6.3, 6.4 and 6.5 streams — this project is on 6000.5.6f1, so
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

#if UNITY_EDITOR || UNITY_WEBGL
    PanelSettings _panelCopy;
#endif

    readonly List<FmNoteEvent> _pending = new();

    // The last effect settings the synth was given, which is what makes sending them
    // again a comparison rather than a notification from everything that can move one.
    SendFxRuntime _fx;

    // The window, plus however far past the clock the driver's earliest schedulable
    // note lies. Under the pipeline that is nothing and this is just the window.
    long LookaheadSamples
      => (long)(Lookahead * Synth.SampleRate) + Synth.MinimumLead;
}

} // namespace Jacquard.App
