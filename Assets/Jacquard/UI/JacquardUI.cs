using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// Assembles the screen: two rows of chrome above a scrolling score plane, with the
// tile and sound windows floating over it.
//
// prototype.md leaves the application level UI to be designed here, so it is kept
// to what a prototype has to prove: that every kind of tile can be put down, tuned
// and heard, that a score survives a save and a load, and that the plane can be
// navigated when it grows past the screen.

sealed class JacquardUI
{
    public JacquardUI(VisualElement root, JacquardApp app)
    {
        _app = app;
        _editor = app.Editor;

        root.style.flexGrow = 1;
        root.style.backgroundColor = Style.Background;

        root.Add(BuildTransportRow());
        root.Add(BuildPaletteRow());

        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.style.position = Position.Relative;
        body.style.overflow = Overflow.Hidden;
        root.Add(body);

        _scroll = new ScrollArea { WheelSpeed = 2.0f };
        _scroll.style.position = Position.Absolute;
        _scroll.style.left = 0;
        _scroll.style.top = 0;
        _scroll.style.right = 0;
        _scroll.style.bottom = 0;
        body.Add(_scroll);

        _view = app.View;
        _scroll.Add(_view);

        _view.KeyPressed += OnKey;
        _view.CursorMoved += OnCursorMoved;
        _view.RevealRequested += Reveal;

        _inspector = new InspectorPanel(_editor);
        body.Add(_inspector.Root);

        _sound = new SoundPanel(_editor);
        body.Add(_sound.Root);

        _hint = Controls.Hint(HintText);
        _hint.style.position = Position.Absolute;
        _hint.style.left = 12;
        _hint.style.bottom = 10;
        _hint.style.width = 420;
        _hint.pickingMode = PickingMode.Ignore;
        body.Add(_hint);

        _editor.Changed += OnChanged;

        _view.Rebuild();
        _view.Focus();
    }

    // Called every frame from the app.
    public void Update()
    {
        _view.RefreshPlayheads();

        Controls.SetActive(_play, _app.Sequencer.IsPlaying);
        _play.text = _app.Sequencer.IsPlaying ? "Stop" : "Play";
        // A loaded project brings a tempo of its own, which the bar has to follow.
        _tempo.Sync();
        _octave.text = _editor.Octave.ToString();
        Controls.SetActive(_soundButton, _sound.IsOpen);

        _status.text = Status();
    }

    // Construction

    VisualElement BuildTransportRow()
    {
        var row = Bar();

        _play = Controls.Push("Play", _app.TogglePlay, 54);
        row.Add(_play);

        // The tempo, on a bar rather than between a pair of nudges: a project is set
        // to a tempo once, and what is wanted then is to type the number, not to walk
        // to it a beat at a time.
        _tempo = Controls.Bar(TempoRange, () => _editor.Project.Tempo,
                              value => _editor.Project.Tempo = value);
        _tempo.style.width = 78;
        row.Add(_tempo);

        row.Add(Separator());

        _soundButton = Controls.Push("Sound", () => { _sound.Toggle(); Refocus(); }, 54);
        row.Add(_soundButton);

        row.Add(Separator());

        _slots = _app.Store.Slots();

        var chooser = Controls.Chooser("File", _slots,
                                       () => Mathf.Max(0, _slots.IndexOf(_app.Store.Name)),
                                       index => _app.Store.Name = _slots[index]);
        chooser.style.width = 190;
        chooser.style.marginBottom = 0;
        row.Add(chooser);

        row.Add(Controls.Push("Save", () => { _app.Save(); Refocus(); }, 46));
        row.Add(Controls.Push("Load", () => { _app.Load(); Refocus(); }, 46));

        row.Add(Separator());

        _status = Controls.Value("");
        row.Add(_status);

        return row;
    }

    VisualElement BuildPaletteRow()
    {
        var row = Bar();

        row.Add(Controls.Caption("Palette"));

        foreach (var kind in new[] { "PABS", "PREL", "GCYC", "GPRB", "JUMP" })
        {
            var name = kind;
            row.Add(Controls.Push(name, () => { _editor.Put(name); Refocus(); }, 46));
        }

        row.Add(Controls.Push("Delete", () => { _editor.Delete(); Refocus(); }, 54));

        row.Add(Separator());

        row.Add(Controls.Push("New lane", () => { _editor.NewChannelLane(); Refocus(); }, 66));

        row.Add(Separator());

        row.Add(Controls.Caption("Octave"));
        row.Add(Controls.Push("-", () => { _editor.SetOctave(_editor.Octave - 1);
                                           Refocus(); }, 22));
        _octave = Controls.Value("");
        _octave.style.width = 18;
        _octave.style.flexGrow = 0;
        _octave.style.unityTextAlign = TextAnchor.MiddleCenter;
        row.Add(_octave);
        row.Add(Controls.Push("+", () => { _editor.SetOctave(_editor.Octave + 1);
                                           Refocus(); }, 22));

        return row;
    }

    static VisualElement Bar()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.flexShrink = 0;
        row.style.height = 32;
        row.style.paddingLeft = 10;
        row.style.paddingRight = 10;
        row.style.borderBottomWidth = 1;
        row.style.borderBottomColor = Style.PanelLine;
        return row;
    }

    static VisualElement Separator()
    {
        var line = new VisualElement();
        line.style.width = 1;
        line.style.height = 18;
        line.style.flexShrink = 0;
        line.style.backgroundColor = Style.PanelLine;
        line.style.marginLeft = 5;
        line.style.marginRight = 8;
        return line;
    }

    // Behaviour

    void OnChanged()
    {
        _view.Rebuild();
        _inspector.Refresh();
        // A renumbered CHAN tile changes which sound the cursor is standing over.
        _sound.Refresh();
        _octave.text = _editor.Octave.ToString();
    }

    // Both panels show whatever the cursor is on: the inspector the tile, the sound
    // window the timbre of the channel it belongs to.
    void OnCursorMoved()
    {
        _inspector.Refresh();
        _sound.Refresh();
    }

    void OnKey(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Space || evt.character == ' ')
        {
            if (evt.keyCode == KeyCode.Space) _app.TogglePlay();
            evt.StopPropagation();
            return;
        }

        if (_editor.HandleKey(evt))
        {
            _octave.text = _editor.Octave.ToString();
            evt.StopPropagation();
        }
    }

    // Keeps typing working after a button has been pressed: a click moves the focus
    // to the button, and the grid is where the keys are supposed to land.
    void Refocus() => _view.Focus();

    // Brings the cursor into view when it walks off the edge.
    void Reveal(Rect rect)
    {
        var size = _scroll.contentRect.size;
        if (size.x <= 0.0f || size.y <= 0.0f) return;

        var offset = _scroll.Offset;

        if (rect.xMin < offset.x) offset.x = rect.xMin;
        if (rect.xMax > offset.x + size.x) offset.x = rect.xMax - size.x;
        if (rect.yMin < offset.y) offset.y = rect.yMin;
        if (rect.yMax > offset.y + size.y) offset.y = rect.yMax - size.y;

        _scroll.Offset = offset;
    }

    string Status()
    {
        var status = _app.Status;

        _text.Clear();
        _text.Append("cursor ").Append(_view.Cursor);
        _text.Append("   voices ").Append(status.activeVoices)
             .Append('/').Append(_app.MaxVoices);

        if (_app.Sequencer.IsPlaying)
        {
            _text.Append("   runners ").Append(_app.Sequencer.Runners.Count);

            foreach (var runner in _app.Sequencer.Runners)
                _text.Append("  ch").Append(runner.Channel)
                     .Append(':').Append(runner.PlayingStep + 1)
                     .Append(" lap ").Append(runner.Pass + 1);
        }

        if (_app.Message != null) _text.Append("   ").Append(_app.Message);

        return _text.ToString();
    }

    // Private members

    readonly JacquardApp _app;
    readonly ScoreEditor _editor;
    readonly ScoreView _view;
    readonly ScrollArea _scroll;
    readonly InspectorPanel _inspector;
    readonly SoundPanel _sound;
    readonly Label _hint;
    readonly StringBuilder _text = new();

    Button _play;
    Button _soundButton;
    ValueBar _tempo;
    Label _status;
    Label _octave;
    List<string> _slots;

    // A tempo below a walking pace or above a drum machine's top speed is of no
    // interest, so the bar covers the useful span and typing covers the rest.
    static readonly ValueBar.Range TempoRange =
      new ValueBar.Range(20.0f, 300.0f, snap: 1.0f, digits: 0, unit: "bpm");

    const string HintText =
      "Click a cell to put the cursor there. A-G writes a note and steps right, " +
      "0-8 picks the octave, shift+arrows transpose, delete removes. A tile on a " +
      "lane's TERM cell adds a step. Command+drag or a two finger swipe pans the " +
      "plane. Space plays. Drag a value bar right or up to raise it, shift for " +
      "fine, double click to type an exact number.";
}

} // namespace Jacquard.App
