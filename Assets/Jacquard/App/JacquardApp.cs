using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

using CoreProject = Jacquard.Project;

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

    // The grid is drawn in whole pixels — 34x36 cells, 1px chains, half-pixel
    // aligned icons — so the panel is left at constant pixel size and scaled by a
    // whole number instead of by DPI. Two is right for a retina display, one for
    // everything else; a fractional scale would smear every hairline.
    [field:SerializeField, Range(1, 3)]
    public int PixelScale { get; set; } = 2;

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
        if (ui.panelSettings != null) ui.panelSettings.scale = PixelScale;

        var document = ui.rootVisualElement;
        _ui = new JacquardUI(document.Q("root") ?? document, this);
    }

    void Update()
    {
        // Hand over every note that falls inside the lookahead window.
        _pending.Clear();
        Sequencer.Schedule(Synth.CurrentSample, LookaheadSamples,
                           Synth.SampleRate, _pending);

        foreach (var note in _pending) Synth.Schedule(note);

        Status = Synth.GetStatus();
        _ui.Update();
    }

    void OnDestroy() => Synth?.Dispose();

    // Private members

    JacquardUI _ui;
    readonly List<FmNoteEvent> _pending = new();

    long LookaheadSamples => (long)(Lookahead * Synth.SampleRate);
}

} // namespace Jacquard.App
