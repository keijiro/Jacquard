using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// Assembles the screen: one row of chrome above a scrolling score plane, with a
// column of panels floating over each of its top corners.
//
// The row carries what belongs to the project as a whole and nothing else. Anything
// that applies to a cell is on the panel that follows the cursor, which is where the
// cell already is, and the plane keeps the screen that a palette and a paragraph of
// keys used to take. The one switch on the row is the one panel a cell cannot ask
// for, since the send effects belong to the project rather than to anything written
// on the plane.
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
        _view.DoubleClicked += _editor.PlaceNote;
        _view.TilesDropped += _editor.DropTiles;
        _view.LaneDropped += _editor.DropLane;

        _inspector = new InspectorPanel(_editor);

        // These two share the slot under the inspector. Only one of them ever
        // answers to the tile under the cursor, so neither has to know about the
        // other.
        _sound = new SoundPanel(_editor);
        _lock = new LockPanel(_editor);

        body.Add(PanelColumn(false, _inspector.Root, _sound.Root, _lock.Root));

        // The effects are the project's, not a cell's, so they get a column of their
        // own on the other edge rather than a slot in the cursor's.
        _send = new SendPanel(_editor, () => ShowSend(false));
        body.Add(PanelColumn(true, _send.Root));
        ShowSend(false);

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
        _tempo.style.width = Controls.Width(78);
        row.Add(_tempo);

        row.Add(Separator());

        // The one panel with a switch, because it is the one panel no cell can ask
        // for. It sits with the tempo rather than with the file controls: the delay is
        // locked to the tempo, so the two things that decide how the sequence moves in
        // time are next to each other.
        //
        // "Send FX" and not "Send", which would name the half of the arrangement that
        // is not here: what a channel sends is set on the Sound panel, and this is
        // what it is sent to.
        _sendButton = Controls.Push("Send FX",
                                    () => { ShowSend(!_sendShown); Refocus(); }, 62);
        row.Add(_sendButton);

        row.Add(Separator());

        _slots = _app.Store.Slots();

        var chooser = Controls.Chooser("File", _slots,
                                       () => Mathf.Max(0, _slots.IndexOf(_app.Store.Name)),
                                       index => _app.Store.Name = _slots[index]);
        chooser.style.width = Controls.Width(190);
        chooser.style.marginBottom = 0;
        row.Add(chooser);

        row.Add(Controls.Push("Save", () => { _app.Save(); Refocus(); }, 46));
        row.Add(Controls.Push("Load", () => { _app.Load(); Refocus(); }, 46));

        row.Add(Separator());

        _status = Controls.Value("");
        row.Add(_status);

        return row;
    }

    // A column of panels down one edge. They stack in the order they are given rather
    // than each holding a corner of its own: on the right, the Tile panel is always up
    // so it keeps the top, and whichever of Sound and Lock the cursor calls for falls
    // in under it. A panel that is down is display: none, which takes it out of the
    // column rather than leaving its gap behind.
    //
    // Under each other, not beside, because what is beside them is the score. A second
    // column of the cursor's panels would cost a panel's width of plane down the whole
    // height of the screen for the sake of one that is up only some of the time and is
    // never as tall as the screen; a column that grows downwards costs only what it is
    // using.
    //
    // The left edge is a second column all the same, and this is what it is for: the
    // Send FX panel is nothing to do with the cursor, so standing it under the Tile
    // panel would put a project setting in the queue behind whatever cell is selected,
    // and it would move down the screen every time the Tile panel grew a line.
    // Opposite corners also mean that reaching for an effect never covers what the
    // cursor is saying about the note the effect is for.
    //
    // A column is transparent to the pointer and only as tall as what is on it, so the
    // plane stays reachable everywhere a panel is not actually drawn.
    static VisualElement PanelColumn(bool onLeft, params VisualElement[] panels)
    {
        var column = new VisualElement();
        column.style.position = Position.Absolute;
        column.style.top = Controls.PanelGap;
        column.pickingMode = PickingMode.Ignore;

        if (onLeft)
            column.style.left = Controls.PanelGap;
        else
            column.style.right = Controls.PanelGap;

        foreach (var panel in panels) column.Add(panel);

        return column;
    }

    static VisualElement Bar()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.flexShrink = 0;
        row.style.height = Controls.ToolbarHeight;
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
        // Short of the controls either side of it, so it reads as a break in the row
        // rather than as an edge of one.
        line.style.height = Controls.RowHeight - 2;
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
        // A renumbered CHAN tile changes which sound the cursor is standing over,
        // and moving a lane can change which channel a lock colours.
        _sound.Refresh();
        _lock.Refresh();
        // Not in OnCursorMoved, since nothing on the Send FX panel answers to a cell.
        // This is for the one change that does reach it: a load, which arrives with
        // effect settings of its own.
        _send.Refresh();
    }

    // Every panel on the right shows whatever the cursor is on: the inspector the
    // tile, and beside it either the timbre of a channel or the hold a lock has on one.
    void OnCursorMoved()
    {
        _inspector.Refresh();
        _sound.Refresh();
        _lock.Refresh();
    }

    // The button and the panel's own close are the two halves of one switch, so both
    // come through here rather than each setting what it can reach.
    void ShowSend(bool shown)
    {
        _sendShown = shown;
        _send.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        Controls.SetActive(_sendButton, shown);
    }

    void OnKey(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Space || evt.character == ' ')
        {
            if (evt.keyCode == KeyCode.Space) _app.TogglePlay();
            evt.StopPropagation();
            return;
        }

        if (_editor.HandleKey(evt)) evt.StopPropagation();
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
    readonly LockPanel _lock;
    readonly SendPanel _send;
    readonly StringBuilder _text = new();

    Button _play;
    Button _sendButton;
    bool _sendShown;
    ValueBar _tempo;
    Label _status;
    List<string> _slots;

    // A tempo below a walking pace or above a drum machine's top speed is of no
    // interest, so the bar covers the useful span and typing covers the rest.
    static readonly ValueBar.Range TempoRange =
      new ValueBar.Range(20.0f, 300.0f, snap: 1.0f, digits: 0, unit: "bpm");
}

} // namespace Jacquard.App
