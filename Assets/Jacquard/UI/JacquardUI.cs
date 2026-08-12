using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// Assembles the screen: one row of chrome above a scrolling score plane, with columns
// of panels floating over its top corners and one panel along the bottom edge.
//
// The row carries what belongs to the project as a whole and nothing else. Anything
// that applies to a cell is on the panel that follows the cursor, which is where the
// cell already is, and the plane keeps the screen that a palette and a paragraph of
// keys used to take. The two switches on the row are the two panels a cell cannot ask
// for: the send effects, which belong to the project rather than to anything written on
// the plane, and the live effects, which belong to nothing at all — they are held rather
// than set, and what they colour is gone as soon as the hand is off.
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

        // The effects are the project's, not a cell's, so they get a column of their
        // own rather than a slot in the cursor's. One panel each: a heading inside a
        // panel was doing the work a panel does.
        _reverb = new SendPanel(_editor, SendPanel.Effect.Reverb);
        _delay = new SendPanel(_editor, SendPanel.Effect.Delay);

        body.Add(PanelEdge(false, PanelColumn(_reverb.Root, _delay.Root),
                                  PanelColumn(_inspector.Root, _sound.Root,
                                              _lock.Root)));
        ShowSend(false);

        // Neither column, because this one is not read: it is played. The columns are
        // where the eye goes and the bottom edge is where the hands already are.
        _live = new LivePanel(app.Live, () => _app.Synth.CurrentSample, Refocus);
        body.Add(PanelDock(_live.Root));
        ShowLive(false);

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

        // A switch, because no cell can ask for what it raises. It sits with the tempo
        // rather than with the file controls: the delay is locked to the tempo, so the
        // two things that decide how the sequence moves in time are next to each other.
        //
        // "Send FX" and not "Send", which would name the half of the arrangement that
        // is not here: what a channel sends is set on the Sound panel, and this is
        // what it is sent to.
        _sendButton = Controls.Push("Send FX",
                                    () => { ShowSend(!_sendShown); Refocus(); }, 62);
        row.Add(_sendButton);

        // Beside it, since the two are the same kind of thing: a panel no cell can ask
        // for, raised by the only switch it has. What separates them is that the send
        // effects are a setting of the project and these are not a setting at all, so
        // this one raises the buttons and the buttons are the whole of the effect.
        //
        // The same width as Send FX, which is the same word long. Two buttons standing
        // next to each other saying as much as each other should measure the same.
        _liveButton = Controls.Push("Live FX",
                                    () => { ShowLive(!_liveShown); Refocus(); }, 62);
        row.Add(_liveButton);

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

    // A column of panels. They stack in the order they are given rather than each
    // holding a corner of its own: in the cursor's column the Tile panel is always up
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
    // A column is transparent to the pointer and only as tall as what is on it, so the
    // plane stays reachable everywhere a panel is not actually drawn.
    static VisualElement PanelColumn(params VisualElement[] panels)
    {
        var column = new VisualElement();
        column.style.flexShrink = 0;
        // The gap to whatever stands to the right of it, by the same rule the rest of
        // this file follows. The last column on an edge gives its own back below.
        column.style.marginRight = Controls.PanelGap;
        column.pickingMode = PickingMode.Ignore;

        foreach (var panel in panels) column.Add(panel);

        return column;
    }

    // The columns down one edge of the screen, in the order they stand across it,
    // pinned to that top corner and no wider than what is on them.
    //
    // Two of them on the right, because a panel that answers to the cursor and one that
    // does not cannot share a column: standing the send effects under the Tile panel
    // would put a project setting in the queue behind whatever cell is selected, and
    // move it down the screen every time the Tile panel grew a line. So they get a
    // column, and the column goes beside the cursor's rather than in the far corner.
    //
    // Beside, because the two are read together. What a channel sends is a row of its
    // Sound panel and what it is sent to is here, so the amount and the effect it feeds
    // are a glance apart instead of a screen apart — and the left edge, which the send
    // effects used to hold, is the one place a column can stand without ever being
    // covered by the cursor's, which is what the channels want.
    //
    // What it costs is the plane under it, and only while it is up: a column of panels
    // that is down is a column of nothing, and this one is down until the Send FX
    // button raises it.
    static VisualElement PanelEdge(bool onLeft, params VisualElement[] columns)
    {
        var edge = new VisualElement();
        edge.style.position = Position.Absolute;
        edge.style.top = Controls.PanelGap;
        edge.style.flexDirection = FlexDirection.Row;
        edge.style.alignItems = Align.FlexStart;
        edge.pickingMode = PickingMode.Ignore;

        if (onLeft)
            edge.style.left = Controls.PanelGap;
        else
            edge.style.right = Controls.PanelGap;

        foreach (var column in columns) edge.Add(column);

        // The gap the last column carries has nothing but the screen's edge or the
        // plane on the other side of it, and the inset above has already paid for one.
        columns[columns.Length - 1].style.marginRight = 0;

        return edge;
    }

    // One panel along the bottom edge, centred, for the one panel that is played
    // rather than read.
    //
    // A column would put it under one hand and out of reach of the other, and the
    // corner it took would be a corner of the plane covered by something up only while
    // it is being used. Across the bottom it is the width of its own contents and no
    // more, it sits where both thumbs already are on a tablet held in two hands, and
    // the score it is played over stays where it was.
    //
    // The panel's own bottom margin is what holds it off the edge, which is the same
    // gap the columns are inset by and the same rule everything else here follows: a
    // gap belongs to the thing above and to the left of it.
    static VisualElement PanelDock(VisualElement panel)
    {
        var dock = new VisualElement();
        dock.style.position = Position.Absolute;
        dock.style.left = 0;
        dock.style.right = 0;
        dock.style.bottom = 0;
        dock.style.alignItems = Align.Center;
        dock.pickingMode = PickingMode.Ignore;
        dock.Add(panel);
        return dock;
    }

    static VisualElement Bar()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.flexShrink = 0;
        row.style.height = Controls.ToolbarHeight;
        row.style.paddingLeft = Controls.Inset;
        row.style.paddingRight = Controls.Inset;
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
        // Wider air than the gap between two buttons, since this is what tells one
        // group of them from the next — and short on the left by the gap the control
        // before it has already left, the way a rule down a panel is short on top.
        line.style.marginLeft = SeparatorAir - Controls.Gap;
        line.style.marginRight = SeparatorAir;
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
        // Not in OnCursorMoved, since nothing on the send panels answers to a cell.
        // This is for the one change that does reach them: a load, which arrives with
        // effect settings of its own.
        _reverb.Refresh();
        _delay.Refresh();
    }

    // Every panel on the right shows whatever the cursor is on: the inspector the
    // tile, and beside it either the timbre of a channel or the hold a lock has on one.
    void OnCursorMoved()
    {
        _inspector.Refresh();
        _sound.Refresh();
        _lock.Refresh();
    }

    // The one thing that raises and lowers the send effects. It is the transport button
    // and nothing else, so the button's own look and what it shows are set in the same
    // place and cannot come apart — and the two panels move together, since they are
    // one setting of the project in two boxes rather than two things to arrange.
    void ShowSend(bool shown)
    {
        _sendShown = shown;

        var display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        _reverb.Root.style.display = display;
        _delay.Root.style.display = display;

        Controls.SetActive(_sendButton, shown);
    }

    // The same switch for the one panel that holds nothing. Lowering it does not lift
    // whatever is held on it, because a button cannot be held once it is not on screen:
    // losing the panel loses the pointer capture, and losing the capture is already
    // what ends an effect.
    void ShowLive(bool shown)
    {
        _liveShown = shown;

        _live.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        Controls.SetActive(_liveButton, shown);
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
    readonly SendPanel _reverb;
    readonly SendPanel _delay;
    readonly LivePanel _live;
    readonly StringBuilder _text = new();

    Button _play;
    Button _sendButton;
    bool _sendShown;
    Button _liveButton;
    bool _liveShown;
    ValueBar _tempo;
    Label _status;
    List<string> _slots;

    const float SeparatorAir = 8.0f;

    // A tempo below a walking pace or above a drum machine's top speed is of no
    // interest, so the bar covers the useful span and typing covers the rest.
    static readonly ValueBar.Range TempoRange =
      new ValueBar.Range(20.0f, 300.0f, snap: 1.0f, digits: 0, unit: "bpm");
}

} // namespace Jacquard.App
