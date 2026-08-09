using UnityEngine.UIElements;

namespace Jacquard.App {

// What one parameter lock takes hold of.
//
// The same list the Sound panel shows, in the same order and over the same ranges,
// because it is the same set: every field of the patch is a lock target. Reading a
// lock against the timbre it colours only works if the two are laid out alike.
//
// A row starts released and greyed, and reads out what the channel does without it.
// Moving its bar is what takes hold of that parameter — there is no separate step
// for arming one, since a value nobody set is not a lock — and clicking its name
// lets go again. Whatever is left grey is untouched by this tile, so a lock holding
// nothing at all does nothing at all, which is what a freshly placed one is.
//
// It stands up under the Tile panel while the cursor is on a PABS or PREL tile and
// nowhere else, the rule every panel here follows. That is also why it can share a
// slot with the Sound panel: a CHAN tile is not a lock, so the two are never up
// together.

sealed class LockPanel
{
    public VisualElement Root { get; }

    public LockPanel(ScoreEditor editor)
    {
        _editor = editor;

        // No close button, for the reason the Sound panel has none: the cursor
        // decides whether this is up, and the next keypress would undo a button.
        Root = Controls.Panel("Lock", null);

        _body = new VisualElement();
        Root.Add(_body);

        Refresh();
    }

    // Called when the cursor moves and when the score changes.
    public void Refresh()
    {
        var tile = _editor.Selected as ParamTile;

        Root.style.display = tile != null ? DisplayStyle.Flex : DisplayStyle.None;

        if (tile == null) return;

        // The channel is part of what is built, and a lock can change channels
        // without moving: renumbering the CHAN above it does that, and so does
        // dragging the lane under a different jump.
        if (tile != _tile || Channel != _channel)
        {
            (_tile, _channel) = (tile, Channel);
            Build();
            return;
        }

        // The rows are rebuilt only for another tile, the way the Tile panel does
        // it: a row that pulled itself out from under the drag driving it would end
        // that drag. Everything a row shows can change without the row changing.
        foreach (var row in _body.Query<LockRow>().Build()) row.Sync();
    }

    // Private members

    readonly ScoreEditor _editor;
    readonly VisualElement _body;

    ParamTile _tile;
    int _channel;

    void Build()
    {
        _body.Clear();

        // Which channel this lock colours. A lock always takes the whole channel,
        // and this is the only place the number appears for one: the tile itself
        // does not carry it, and a branch lane borrows it from the jump that
        // reaches it.
        _body.Add(Controls.Caption("Channel " + _channel));
        _body.Add(Controls.Divider());

        for (var target = 0; target < ParamTargets.Count; target++)
            _body.Add(new LockRow(this, target));
    }

    int Channel => _editor.Score.ChannelOf(_editor.SelectedLane);

    // What the row shows while nothing holds it: where the channel already stands
    // for an absolute lock, and no shift at all for a relative one. Either way it is
    // what the parameter does if this tile is left alone, which is also where a drag
    // that takes hold of it starts from.
    float Released(int target)
      => _tile is AbsoluteParamTile
         ? ParamTargets.Get(_editor.Project.Patches[_channel], target) : 0.0f;

    // A parameter, held or not
    //
    // An element of its own rather than a row plus a list of closures, for the
    // reason ValueBar.SyncAll gives: the tree already knows what is on screen.

    sealed class LockRow : VisualElement
    {
        public LockRow(LockPanel panel, int target)
        {
            (_panel, _target) = (panel, target);

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.flexShrink = 0;
            style.marginBottom = 3;

            _caption = Controls.Caption(ParamTargets.Name(target));

            // Every label this UI builds is transparent to the pointer, so that the
            // text on a cell does not eat the click meant for the cell and the
            // readout on a bar does not eat the drag meant for the bar. This one is
            // the exception: it is the control, not a label on one.
            _caption.pickingMode = PickingMode.Position;

            // And it is as tall as the bar beside it rather than as tall as its own
            // line of text, so that what can be clicked is the row the name sits on
            // and not a twelve pixel strip through the middle of it.
            _caption.style.height = Controls.RowHeight;

            _caption.RegisterCallback<PointerDownEvent>(OnCaptionDown);
            _caption.RegisterCallback<PointerUpEvent>(OnCaptionUp);
            _caption.RegisterCallback<PointerEnterEvent>(_ => SetHover(true));
            _caption.RegisterCallback<PointerLeaveEvent>(_ => SetHover(false));
            Add(_caption);

            // An absolute lock holds a value the target could hold itself, so its bar
            // is the target's own; a relative one holds a shift, and reads from the
            // middle. Neither depends on whether the row is held, so taking hold of a
            // parameter never rebuilds anything.
            var range = panel._tile is AbsoluteParamTile
              ? ParamRanges.Of(target) : ParamRanges.Relative(target);

            _bar = Controls.Bar(range, Get, Set);
            _bar.style.flexGrow = 1;
            Add(_bar);

            Sync();
        }

        // Pulls the row back in line with the tile, in both of the things it shows:
        // the number, and whether the lock is holding it.
        public void Sync()
        {
            _bar.Sync();
            UpdateAppearance();
        }

        // Private members

        readonly LockPanel _panel;
        readonly int _target;
        readonly Label _caption;
        readonly ValueBar _bar;

        bool _hover;

        ParamTile Tile => _panel._tile;

        bool Engaged => Tile.IsEngaged(_target);

        float Get() => Engaged ? Tile[_target] : _panel.Released(_target);

        // Setting a value is what takes hold of the parameter. Nothing else does,
        // which is what makes an untouched row mean untouched.
        void Set(float value)
        {
            Tile.Engage(_target, value);
            UpdateAppearance();
            _panel._editor.Commit();
        }

        void OnCaptionDown(PointerDownEvent e)
        {
            if (e.button != 0) return;

            // Letting go is the only thing the name does that the bar cannot. Taking
            // hold from it as well is worth having anyway: a parameter is sometimes
            // wanted exactly where it already is, and there is no drag that says so.
            if (Engaged) Tile.Release(_target); else Tile.Engage(_target, Get());

            Sync();
            _panel._editor.Commit();

            // The pointer is captured so that the release is seen here even if the
            // hand slid off the name in between, which is what makes the keyboard
            // handover below certain rather than merely likely.
            _caption.CapturePointer(e.pointerId);

            e.StopPropagation();
        }

        // The name is not focusable, so the press that reached it took the keyboard
        // away from whatever had it and gave it to nothing. Handing it back is what
        // every button on the toolbar does after being pressed, and for the same
        // reason: letting go of a parameter must not quietly be the end of typing
        // notes on the grid.
        //
        // It waits for the release because the focus controller settles the press
        // itself, after this element has seen it — focusing from the press handler
        // is simply undone. ValueBar returns the keyboard at the end of a drag for
        // the same reason.
        void OnCaptionUp(PointerUpEvent e)
        {
            if (!_caption.HasPointerCapture(e.pointerId)) return;

            _caption.ReleasePointer(e.pointerId);
            _panel._editor.View.Focus();

            e.StopPropagation();
        }

        void SetHover(bool on)
        {
            _hover = on;
            UpdateAppearance();
        }

        // A released row is dimmed whole, bar and all, the way the rails and a note's
        // length label are dimmed: it is the same content, further back. The name
        // lights under the pointer, since clicking it is the one thing here that a
        // greyed control would otherwise say is not available.
        void UpdateAppearance()
        {
            var engaged = Engaged;

            style.opacity = engaged ? 1.0f : ReleasedOpacity;
            _caption.style.color = engaged || _hover ? Style.NoteText : Style.Label;
        }

        const float ReleasedOpacity = 0.45f;
    }
}

} // namespace Jacquard.App
