using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// The score plane.
//
// Three layers: a painted one underneath for the lattice, the rails, the chains
// and the jump links, the tile cells in the middle, and a painted overlay on top
// for the cursor and the playheads. The painted layers are cheap to keep in step
// because nothing about them is stateful — they are redrawn from the score.
//
// All pointer and key input lands here rather than on the cells, because placing a
// tile on an empty cell needs a coordinate anyway.

public sealed class ScoreView : VisualElement
{
    // Public state

    // The score on the plane. Assigning a different one says that what comes next is a
    // score arriving and not the one here being edited, which is the difference Reframe
    // has to know about: there is nothing to hold still across a score being replaced.
    public Score Score
    {
        get => _score;
        set { _arrived |= value != _score; _score = value; }
    }

    public Sequencer Sequencer { get; set; }

    // Whether the plane is being held still, which it is while a score is waiting to
    // come in at the turn of the piece: the switch is measured on a lane the runners
    // are playing, so an edit that moved one would move the line under it.
    //
    // Only what edits is held. A press that would have carried a tile is let through
    // instead of stopped, so it reaches the scroll area and pans, and the plane can be
    // read and moved about while it waits. Keys go on being raised too — the transport
    // is on one of them, and what would edit is refused by the editor rather than here.
    public bool Locked
    {
        get => _locked;

        set
        {
            if (_locked == value) return;

            _locked = value;

            // Nothing stays in hand across this, for the reason an edit ends a drag:
            // what a drag is holding is a reading of a score that is about to be
            // somebody else's.
            if (value) EndDrag();

            style.opacity = value ? Style.DimmedOpacity : 1.0f;
        }
    }

    public GridPoint Cursor { get; private set; } = new GridPoint(1, 1);

    public event Action CursorMoved;
    public event Action<KeyDownEvent> KeyPressed;

    // Raised with the rect the cursor now occupies, so that a container can bring
    // it into view.
    public event Action<Rect> RevealRequested;

    // Raised with how far the score has just been moved across the plane, in pixels,
    // so that a container can take up the same distance and leave the picture where it
    // stood. The plane grew on its left by this much and everything on it went right by
    // it; a viewport that did not follow would look as though the score had jumped.
    public event Action<Vector2> Reframed;

    // Raised on a double click, once the cursor has been put on the cell that was
    // hit, so that what it means is a question about the cell the cursor is now on.
    public event Action DoubleClicked;

    // Raised when something carried by hand is let go. The view resolves nothing
    // itself: it says what was picked up and which cell it was dropped on.
    public event Action<CellRef, GridPoint> TilesDropped;
    public event Action<Lane, GridPoint> LaneDropped;

    public ScoreView()
    {
        style.position = Position.Relative;
        style.flexShrink = 0;
        focusable = true;

        _lower = AddLayer(DrawLower);
        _tiles = new VisualElement { pickingMode = PickingMode.Ignore };
        _tiles.StretchToParentSize();
        Add(_tiles);
        _upper = AddLayer(DrawUpper);

        // What is in hand rides above everything, including the cursor: it is the
        // one thing on the plane that is not where the score says it is. It is also the
        // one layer that is moved on every frame of a drag, so it is told so — the header
        // of ScrollArea has what the hint spares a subtree that travels.
        _ghosts = new VisualElement { pickingMode = PickingMode.Ignore };
        _ghosts.StretchToParentSize();
        _ghosts.usageHints = UsageHints.GroupTransform;
        _ghosts.style.opacity = 0.85f;
        Add(_ghosts);

        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerUpEvent>(OnPointerUp);
        RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
        RegisterCallback<KeyDownEvent>(OnKeyDown);
    }

    // Refreshing

    // Called whenever the score has been edited, which is not the human-paced event it
    // sounds like: a panel bar being scrubbed commits on every pointer-move frame, so
    // this runs at the frame rate for as long as a hand is on one.
    //
    // Cells are moved rather than made again. The elements are the whole of the cost —
    // an inline style write allocates the first time that property is set on an element
    // and nothing afterwards, so a note cell, which is an element plus a column, a row
    // and two or three labels, is about 6 KB the once and free to stand on a different
    // cell later. Measured against the sample score's 94 cells: building them, 1.0 ms
    // and 560 KB; writing left and top on the 94 already standing, 31 µs and nothing at
    // all. On an iPad a lane move was a single frame of 25-31 ms against a 2.5 ms
    // median, half of it script and half of it the repaint that the fresh elements
    // dirtied — a dropped vsync for an edit that moved no cell's picture at all.
    //
    // So the walk below hands the element already standing in each slot to the tile the
    // score now has there, and stands up a new one only past the end of what is on the
    // plane. TileElement.Apply redraws only where the picture has actually changed,
    // which is one cell for a note edited and none at all for a lane moved.
    public void Rebuild()
    {
        // Nothing can stay in hand across an edit: what a drag is holding is a
        // reading of the score, and this is the score having changed. A light on a
        // cell goes out for the same reason — a reframe can move what it is on.
        EndDrag();
        EndFlash();

        // Before the resize, which reads the positions this may have moved, and before
        // the cells, which are placed from where it leaves the score.
        Reframe();

        Resize();

        // A channel start says whether its lane runs, and the master lane always runs
        // whatever its switch says, so the cell is drawn from what will happen rather than
        // from what is written on it. Read once: finding the master sorts the lanes.
        //
        // What the cell does not say is whether the lane is running yet — a lane switched
        // on waits for the turn of the piece. The playhead says that, so between them a
        // solid cell with no playhead is a lane about to come in and one with a playhead is
        // a lane playing, and there is no third look to draw.
        var master = Score.MasterLane;

        // The cells in the order the score gives them up, counted as they go by. No
        // list of built elements is kept alongside: the tree already knows what is on
        // the plane, which is the argument ValueBar.SyncAll makes for the same thing.
        //
        // An index is enough to pair a slot with a cell because the lanes keep their
        // order across the edits this is for. MoveLane writes a lane's coordinates and
        // nothing else, ChannelLanes and MasterLane sort a projection rather than the
        // list, and AddLane appends — so a lane moved, a note edited, a step or a stack
        // changed all leave every earlier slot on the tile it had. A lane deleted out of
        // the middle and a score arriving do shift the walk, and those two redraw about
        // as much as this used to; the arriving score is a dimmed plane anyway.
        var count = 0;

        foreach (var lane in Score.Lanes)
        {
            Place(ref count, lane.Head, lane.HeadPoint,
                  lane.Channel is { Enabled: false } && lane != master);
            Place(ref count, Score.Terminator, lane.TermPoint);

            for (var i = 0; i < lane.Steps.Count; i++)
            {
                var step = lane.Steps[i];
                for (var d = 0; d < step.Depth; d++)
                    Place(ref count, step.Tiles[d], lane.CellPoint(i, d));
            }
        }

        // Whatever the score no longer fills, taken off the back.
        while (_tiles.childCount > count) _tiles.RemoveAt(_tiles.childCount - 1);

        _lower.MarkDirtyRepaint();
        _upper.MarkDirtyRepaint();
    }

    // Puts the next cell of the walk on the element standing in that slot, or on a new
    // one if the walk has gone past the end of the plane.
    void Place(ref int index, Tile tile, GridPoint point, bool off = false)
    {
        if (index < _tiles.childCount)
            ((TileElement)_tiles[index]).Apply(tile, point, off);
        else
            _tiles.Add(new TileElement(tile, point, off));

        index++;
    }

    // Checks the audible position of every runner and repaints the overlay when it
    // has moved. Cheap enough to call every frame.
    public void RefreshPlayheads()
    {
        _playheads.Clear();

        if (Sequencer != null && Sequencer.IsPlaying)
            foreach (var runner in Sequencer.Runners)
                if (runner.PlayingLane != null && runner.PlayingStep >= 0)
                    _playheads.Add((runner.PlayingLane, runner.PlayingStep));

        if (Same(_playheads, _paintedPlayheads)) return;

        _paintedPlayheads.Clear();
        _paintedPlayheads.AddRange(_playheads);
        _upper.MarkDirtyRepaint();
    }

    // Cursor

    public void SetCursor(GridPoint point)
    {
        point = new GridPoint(Mathf.Clamp(point.X, 0, _columns - 1),
                              Mathf.Clamp(point.Y, 0, _rows - 1));

        if (point == Cursor) return;

        Cursor = point;
        _upper.MarkDirtyRepaint();
        CursorMoved?.Invoke();

        var rect = Style.CellRect(Cursor);
        RevealRequested?.Invoke(new Rect(rect.x - Style.Padding, rect.y - Style.Padding,
                                         rect.width + Style.Padding * 2,
                                         rect.height + Style.Padding * 2));
    }

    public void MoveCursor(int dx, int dy)
      => SetCursor(Cursor.Offset(dx, dy));

    // Geometry

    // How much empty ground the plane keeps past the score, so that there is always
    // somewhere to put a new lane. The same margin on all four sides: to the right and
    // below it falls out of the plane's size, and to the left and above out of Reframe.
    //
    // A row above is not optional. A jump link into the topmost lane is painted through
    // the row over its head, and FindFreeRow keeps one clear so that an unrelated stack
    // does not read as chained to it, so PadRows below one would draw off the plane.
    const int PadColumns = 10;
    const int PadRows = 8;

    // Keeps the free ground to the left and above that the plane's size keeps to the
    // right and below, by moving the score rather than by letting a coordinate go
    // negative. The plane starts at cell (0,0) and nothing in the model addresses a
    // cell before it; what a score can do instead is sit further in.
    //
    // One sided on purpose. Surplus margin is left where it is, so the only thing that
    // ever moves a score is a lane carried or created into the margin — dragging one
    // back to the right, or deleting the leftmost lane, would otherwise haul the whole
    // score after it and rewrite every coordinate in the file for no gain.
    void Reframe()
    {
        // Whether this is a score being moved or a score arriving. Read here rather
        // than acted on at the assignment, since it is this rebuild that the answer is
        // about, and read whether or not anything moves.
        var arrived = _arrived;
        _arrived = false;

        // With no lanes there is no leftmost one to measure, and the score would
        // otherwise be handed the same delta on every rebuild for ever.
        if (Score.Lanes.Count == 0) return;

        var dx = Mathf.Max(0, PadColumns - Score.MinX);
        var dy = Mathf.Max(0, PadRows - Score.MinY);
        if (dx == 0 && dy == 0) return;

        Score.Translate(dx, dy);

        // A score that has just arrived is normalised and nothing else. How far it had
        // to travel is how far the file it came out of happened to sit from the corner,
        // which is a fact about the file and not about anything on the screen — moving
        // the cursor and the viewport by it would carry the plane off by an arbitrary
        // distance and could leave the incoming score off the edge of it. What holds the
        // two scores together is that both end up at the same corner, so the one coming
        // in appears exactly where the one going out was, which at the turn of a piece
        // is the whole point.
        if (arrived) return;

        // Otherwise the score under the cursor has moved, and the cursor travels with
        // it: it stays on the cell it was on and there is nothing for the panels to hear
        // about. Not through SetCursor, which would say the cursor had moved and ask to
        // be brought into view — and being brought into view is the one thing that must
        // not happen here, since the scroll offset is about to be moved by exactly this
        // much to hold the picture still. Its bounds are seen to at the end of Resize
        // instead, which is where the plane's size is known.
        Cursor = Cursor.Offset(dx, dy);

        Reframed?.Invoke(new Vector2(dx * Style.StrideX, dy * Style.StrideY));
    }

    // The plane is kept a good deal larger than the score so that there is always
    // empty ground to put a new lane on.
    void Resize()
    {
        _columns = Mathf.Max(48, Score.Width + PadColumns);
        _rows = Mathf.Max(28, Score.Height + PadRows);

        var size = Style.PlaneSize(_columns, _rows);
        style.width = size.x;
        style.height = size.y;

        // Whatever the plane has become, the cursor is on it. A reframe moves the
        // cursor by what it moved the score, which the plane does not always match
        // because of the floors above; and a score losing its bottom lane shrinks the
        // plane out from under a cursor standing down there. Silently, since the cell
        // it lands on is read by the panels that this rebuild is refreshing anyway.
        Cursor = new GridPoint(Mathf.Clamp(Cursor.X, 0, _columns - 1),
                               Mathf.Clamp(Cursor.Y, 0, _rows - 1));
    }

    // Painting

    void DrawLower(MeshGenerationContext context)
    {
        if (Score == null) return;

        var painter = context.painter2D;

        DrawLattice(context);
        DrawRails(context);
        DrawChains(painter);
        DrawLinks(painter);
        DrawMarkers(painter);
    }

    // The lattice shows through only where nothing else is: a step that holds
    // nothing gets a pass-through marker instead of a dot.
    void DrawLattice(MeshGenerationContext context)
    {
        _rects.Clear();

        // Square dots rather than round ones: a square is the two triangles that
        // FillRects writes anyway, and at this size nothing else was ever visible.
        for (var y = 0; y < _rows; y++)
            for (var x = 0; x < _columns; x++)
            {
                var point = new GridPoint(x, y);
                if (Score.At(point).Kind != CellKind.Empty) continue;

                var center = Style.CellCenter(point);
                _rects.Add(new Rect(center.x - Style.LatticeDot / 2,
                                    center.y - Style.LatticeDot / 2,
                                    Style.LatticeDot, Style.LatticeDot));
            }

        FillRects(context, _rects, Style.Dot);
    }

    // Each lane's own time axis, from its head to its terminator.
    void DrawRails(MeshGenerationContext context)
    {
        _rects.Clear();

        foreach (var lane in Score.Lanes)
        {
            var from = Style.CellCenter(lane.HeadPoint).x;
            var to = Style.CellCenter(lane.TermPoint).x;
            var y = Mathf.Floor(Style.CellCenter(lane.HeadPoint).y) - Style.RailDot / 2;

            for (var x = from; x < to; x += Style.RailStep)
                _rects.Add(new Rect(x, y, Style.RailDot, Style.RailDot));
        }

        FillRects(context, _rects, Style.Fade(Style.NoteLine, Style.RailOpacity));
    }

    // The vertical chain, which reads as one block from the top down. Only cells of
    // the same stack are joined: the mockup joins anything that happens to be
    // directly above, which makes two unrelated lanes look connected when they end
    // up adjacent, and knowing the lane is enough to avoid it.
    void DrawChains(Painter2D painter)
    {
        painter.strokeColor = Style.NoteLine;
        painter.lineWidth = 1.0f;
        painter.BeginPath();

        foreach (var lane in Score.Lanes)
            for (var i = 0; i < lane.Steps.Count; i++)
                for (var d = 1; d < lane.Steps[i].Depth; d++)
                {
                    var origin = Style.CellOrigin(lane.CellPoint(i, d));
                    var x = origin.x + Mathf.Floor(Style.CellWidth / 2) + 0.5f;
                    painter.MoveTo(new Vector2(x, origin.y - Style.Gap - 1));
                    painter.LineTo(new Vector2(x, origin.y + 1));
                }

        painter.Stroke();
    }

    // A jump leaves its cell downwards, crosses the clear row above the lane it
    // hands over to, and drops into that lane's head.
    void DrawLinks(Painter2D painter)
    {
        painter.strokeColor = Style.Link;
        painter.lineWidth = 1.0f;
        painter.lineJoin = LineJoin.Round;
        painter.lineCap = LineCap.Round;

        foreach (var lane in Score.Lanes)
        {
            if (lane.JumpSource == null) continue;

            var source = Score.Locate(lane.JumpSource);
            if (!source.HasValue) continue;

            var a = Style.CellCenter(source.Value);
            var b = Style.CellCenter(lane.HeadPoint);
            var midY = Style.CellCenter(lane.HeadPoint.Offset(0, -1)).y + Style.LinkOffset;

            _path.Clear();
            _path.Add(new Vector2(a.x + Style.LinkOffset, a.y));
            _path.Add(new Vector2(a.x + Style.LinkOffset, midY));
            _path.Add(new Vector2(b.x + Style.LinkOffset, midY));
            _path.Add(new Vector2(b.x + Style.LinkOffset, b.y));

            painter.BeginPath();
            RoundedPath(painter, _path, Style.LinkRadius);
            painter.Stroke();
        }
    }

    // Empty steps get a marker, so the beats the sequence walks straight through
    // are visible as such.
    void DrawMarkers(Painter2D painter)
    {
        painter.fillColor = Style.Marker;
        painter.BeginPath();

        foreach (var lane in Score.Lanes)
            for (var i = 0; i < lane.Steps.Count; i++)
            {
                if (!lane.Steps[i].IsEmpty) continue;

                var o = Style.CellOrigin(lane.CellPoint(i, 0)) +
                        new Vector2(Mathf.Floor((Style.CellWidth - 7) / 2),
                                    Mathf.Floor((Style.CellHeight - 9) / 2));

                painter.MoveTo(o);
                painter.LineTo(o + new Vector2(7, 4.5f));
                painter.LineTo(o + new Vector2(0, 9));
                painter.ClosePath();
            }

        painter.Fill(FillRule.NonZero);
    }

    void DrawUpper(MeshGenerationContext context)
    {
        var painter = context.painter2D;

        // A bar in the gutter to the left of the step being heard. It sits outside
        // the cells, so it never fights with what they contain.
        painter.fillColor = Style.Playhead;
        painter.BeginPath();

        foreach (var (lane, step) in _paintedPlayheads)
        {
            if (step < 0 || step >= lane.Steps.Count) continue;

            var origin = Style.CellOrigin(lane.CellPoint(step, 0));
            var depth = Mathf.Max(1, lane.Steps[step].Depth);
            var height = depth * Style.StrideY - Style.Gap;

            Rect(painter, new Rect(origin.x - Style.Gap + 1, origin.y, 3, height));
        }

        painter.Fill(FillRule.NonZero);

        // The cursor is drawn just outside the cell it is on, so that the cell
        // itself stays readable underneath.
        painter.strokeColor = Style.Cursor;
        painter.lineWidth = 1.0f;
        painter.BeginPath();

        var rect = Style.CellRect(Cursor);
        RoundedRect(painter, new Rect(rect.x - 2.5f, rect.y - 2.5f,
                                      rect.width + 5, rect.height + 5),
                    Style.Radius + 2);
        painter.Stroke();

        DrawDropCells(painter);
        DrawFlashCells(painter);
    }

    // The cells a drag would land on, filled faintly and outlined. There is no
    // refused marker to go with it: a drop that cannot happen simply has nowhere
    // lit up for it, which is the same thing said without a second colour.
    void DrawDropCells(Painter2D painter)
    {
        if (_dropCells.Count == 0) return;

        painter.fillColor = Style.Fade(Style.Cursor, 0.14f);
        painter.BeginPath();
        foreach (var point in _dropCells) RoundedRect(painter, Style.CellRect(point),
                                                      Style.Radius);
        painter.Fill(FillRule.NonZero);

        painter.strokeColor = Style.Fade(Style.Cursor, 0.7f);
        painter.lineWidth = 1.0f;
        painter.BeginPath();
        foreach (var point in _dropCells) RoundedRect(painter, Style.CellRect(point),
                                                      Style.Radius);
        painter.Stroke();
    }

    // The cells a copy has just been taken from, filled for as long as it takes to
    // notice. Drawn like the drop cells because it is the same statement — these
    // cells and not those — said about something that has already happened.
    void DrawFlashCells(Painter2D painter)
    {
        if (_flashCells.Count == 0) return;

        painter.fillColor = Style.Fade(Style.Cursor, 0.3f);
        painter.BeginPath();
        foreach (var point in _flashCells) RoundedRect(painter, Style.CellRect(point),
                                                       Style.Radius);
        painter.Fill(FillRule.NonZero);
    }

    // Lights the given cells and takes it back a moment later.
    //
    // One scheduled item, rearmed rather than replaced: a second flash while the
    // first is still up would otherwise leave two clocks running and the older one
    // would put the newer light out early.
    public void Flash(IEnumerable<GridPoint> cells)
    {
        _flashCells.Clear();
        _flashCells.AddRange(cells);

        _flashTimer ??= schedule.Execute(EndFlash);
        _flashTimer.ExecuteLater(FlashMilliseconds);

        _upper.MarkDirtyRepaint();
    }

    void EndFlash()
    {
        if (_flashCells.Count == 0) return;

        _flashCells.Clear();
        _upper.MarkDirtyRepaint();
    }

    // Input

    void OnPointerDown(PointerDownEvent evt)
    {
        // The second press a touch drag sends lands on the cell the drag started on, a
        // frame and a pixel or two after the first, which is a double click by every test
        // below — so a finger moving a tile copied its stack, and a finger moving a CHAN
        // head started or stopped the lane. See Controls.PressAlreadyHeld.
        if (Controls.PressAlreadyHeld(this, evt)) return;

        // A modified drag pans the plane instead of editing it, and a held plane is
        // every drag: the press travels on to the scroll area either way.
        if (_locked || ScrollArea.IsPanModifierHeld(evt)) return;

        Focus();

        var point = Style.CellAt(evt.localPosition);
        SetCursor(point);

        // Double click detection of its own, rather than the event's clickCount, which
        // counts presses by the clock alone. A press here, one on the cell beside it and
        // one back here again arrives as a click count of three, and the plane would read
        // that as a double click on a cell nobody pressed twice running. What the gesture
        // means is this cell and then this cell, so the cell is half the test — and the
        // half that matters, since the copy it usually stands for is a question about a
        // position and never about a rhythm.
        //
        // The interval is forgotten once it has been spent, so a third press starts a new
        // pair rather than making a second double out of the same click. ValueBar hand
        // rolls this the same way and for the neighbouring reason: there a click that
        // scrubbed must not count as the first of two.
        var doubled = point == _lastPress &&
                      evt.timestamp - _lastPressTime < Controls.DoubleClickMilliseconds;

        (_lastPress, _lastPressTime) = (point, doubled ? 0L : evt.timestamp);

        if (doubled) DoubleClicked?.Invoke();

        // A cell that holds something can be carried: a tile on a lane to another
        // cell, a lane's own head to take the whole lane with it. The terminator is
        // neither, being where a lane ends rather than something standing on it.
        //
        // Free ground has nothing to carry, so the press is left to travel on to the
        // scroll area and pan the plane instead. The cursor has already moved, which
        // is what the press means if it turns out to be a click and not a drag.
        var cell = Score.At(point);
        if (cell.Kind != CellKind.Tile && cell.Kind != CellKind.Head) return;

        evt.StopPropagation();

        _grabbed = cell;
        _grabOrigin = evt.localPosition;
        this.CapturePointer(evt.pointerId);
    }

    // A press only becomes a drag once it has travelled far enough to mean one, so
    // that a click that wobbles by a pixel still reads as a click.
    void OnPointerMove(PointerMoveEvent evt)
    {
        if (_locked || _grabbed.Kind == CellKind.Empty) return;

        var delta = (Vector2)evt.localPosition - _grabOrigin;

        if (!_dragging)
        {
            if (delta.magnitude < DragThreshold) return;
            BeginDrag();
        }

        _ghosts.style.translate = new Translate(delta.x, delta.y);

        var point = Style.CellAt(evt.localPosition);
        if (point != _dropPoint) { _dropPoint = point; ResolveDrop(); }

        evt.StopPropagation();
    }

    // The capture, not the grab, is what says this press was ours: an edit from
    // the keys ends a drag where it stands, and the pointer still has to be let go
    // of afterwards.
    void OnPointerUp(PointerUpEvent evt)
    {
        if (!this.HasPointerCapture(evt.pointerId)) return;

        var (grabbed, point, dropped) = (_grabbed, _dropPoint, _dragging);

        EndDrag();
        this.ReleasePointer(evt.pointerId);
        evt.StopPropagation();

        if (!dropped || grabbed.Kind == CellKind.Empty) return;

        if (grabbed.Kind == CellKind.Head)
            LaneDropped?.Invoke(grabbed.Lane, point);
        else
            TilesDropped?.Invoke(grabbed, point);
    }

    void OnKeyDown(KeyDownEvent evt) => KeyPressed?.Invoke(evt);

    // Dragging

    void BeginDrag()
    {
        _dragging = true;
        _dropPoint = _grabbed.Kind == CellKind.Head ? _grabbed.Lane.HeadPoint
                     : _grabbed.Lane.CellPoint(_grabbed.Step, _grabbed.Depth);
        BuildGhosts(DragCount(_dropPoint));
        ResolveDrop();
    }

    void EndDrag()
    {
        if (_grabbed.Kind == CellKind.Empty) return;

        _grabbed = CellRef.Empty;
        _dragging = false;

        _ghosts.Clear();
        _ghosts.style.translate = new Translate(0.0f, 0.0f);

        foreach (var cell in _lifted) cell.style.opacity = 1.0f;
        _lifted.Clear();

        _dropCells.Clear();
        _upper.MarkDirtyRepaint();
    }

    // How many tiles a drop here would carry. Read from the target rather than
    // from what was grabbed, so that what is in hand visibly gathers its sub-stack
    // the moment the drag leaves the step it came from.
    int DragCount(GridPoint target)
    {
        if (_grabbed.Kind == CellKind.Head) return 0;

        var lane = Score.DropLane(target, out var step, out _);
        if (lane == _grabbed.Lane && step == _grabbed.Step) return 1;

        return _grabbed.Lane.Steps[_grabbed.Step].Tiles.Count - _grabbed.Depth;
    }

    void ResolveDrop()
    {
        _dropCells.Clear();

        if (_grabbed.Kind == CellKind.Head)
        {
            var lane = _grabbed.Lane;
            if (Score.CanMoveLane(lane, _dropPoint))
            {
                var dx = _dropPoint.X - lane.HeadX;
                var dy = _dropPoint.Y - lane.Y;
                foreach (var cell in lane.OccupiedCells())
                    _dropCells.Add(cell.Offset(dx, dy));
            }
        }
        else
        {
            var count = DragCount(_dropPoint);
            if (count != _ghosts.childCount) BuildGhosts(count);

            var move = Score.PlanMove(_grabbed, _dropPoint);
            for (var i = 0; i < move.Count; i++)
                _dropCells.Add(move.Lane.CellPoint(move.Step, move.Depth + i));
        }

        _upper.MarkDirtyRepaint();
    }

    // Copies of what is being carried, which travel with the pointer while the
    // cells they came from stay faintly in place.
    void BuildGhosts(int count)
    {
        _ghosts.Clear();
        foreach (var cell in _lifted) cell.style.opacity = 1.0f;
        _lifted.Clear();

        if (_grabbed.Kind == CellKind.Head)
        {
            var lane = _grabbed.Lane;
            AddGhost(lane.Head, lane.HeadPoint);
            AddGhost(Score.Terminator, lane.TermPoint);

            for (var i = 0; i < lane.Steps.Count; i++)
                for (var d = 0; d < lane.Steps[i].Depth; d++)
                    AddGhost(lane.Steps[i].Tiles[d], lane.CellPoint(i, d));
        }
        else
        {
            var tiles = _grabbed.Lane.Steps[_grabbed.Step].Tiles;
            for (var i = 0; i < count; i++)
                AddGhost(tiles[_grabbed.Depth + i],
                         _grabbed.Lane.CellPoint(_grabbed.Step, _grabbed.Depth + i));
        }
    }

    void AddGhost(Tile tile, GridPoint point)
    {
        _ghosts.Add(new TileElement(tile, point));

        foreach (var child in _tiles.Children())
            if (child is TileElement cell && cell.Point == point)
            {
                cell.style.opacity = LiftedOpacity;
                _lifted.Add(cell);
                return;
            }
    }

    // Private members

    readonly VisualElement _tiles;
    readonly VisualElement _ghosts;
    readonly PaintLayer _lower;
    readonly PaintLayer _upper;

    readonly List<(Lane lane, int step)> _playheads = new();
    readonly List<(Lane lane, int step)> _paintedPlayheads = new();
    readonly List<Vector2> _path = new();

    // Reused across rebuilds, so that an edit repainting the plane allocates nothing.
    readonly List<Rect> _rects = new();

    int _columns = 48;
    int _rows = 28;

    Score _score;

    // Set when a different score is put on the plane and cleared by the rebuild that
    // takes it up, so that the reframe knows a score arriving from a score moving.
    bool _arrived;

    bool _locked;

    // The last press, for telling a double click from two single ones. Held as the cell
    // rather than as the pixel, since a hand that wobbles across a cell boundary between
    // two presses meant the same cell both times, and one that hits the same pixel of two
    // different cells cannot.
    GridPoint _lastPress = new GridPoint(-1, -1);
    long _lastPressTime;

    // What is in hand. An empty cell means nothing is: the kind of the grabbed
    // cell is also what kind of drag it is, a tile being carried to another cell
    // and a head carrying its lane.
    CellRef _grabbed = CellRef.Empty;
    Vector2 _grabOrigin;
    bool _dragging;

    GridPoint _dropPoint;
    readonly List<GridPoint> _dropCells = new();
    readonly List<TileElement> _lifted = new();

    readonly List<GridPoint> _flashCells = new();
    IVisualElementScheduledItem _flashTimer;

    const float DragThreshold = 4.0f;
    const float LiftedOpacity = 0.2f;
    const long FlashMilliseconds = 220;

    PaintLayer AddLayer(Action<MeshGenerationContext> draw)
    {
        var layer = new PaintLayer(draw);
        Add(layer);
        return layer;
    }

    static bool Same(List<(Lane, int)> a, List<(Lane, int)> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++) if (!a[i].Equals(b[i])) return false;
        return true;
    }

    // Painting primitives

    static void Rect(Painter2D painter, Rect rect)
    {
        painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
        painter.LineTo(new Vector2(rect.xMax, rect.yMin));
        painter.LineTo(new Vector2(rect.xMax, rect.yMax));
        painter.LineTo(new Vector2(rect.xMin, rect.yMax));
        painter.ClosePath();
    }

    // Axis-aligned rectangles written straight into the mesh, two triangles each.
    //
    // Painter2D cannot have these. A filled path costs more than linearly in the number
    // of subpaths it holds — the lattice's 1400 dots measured 58ms of tessellation on an
    // iPad, against 16ms for the whole frame around it — and above about three thousand
    // it stops drawing the far ones altogether. Written as vertices the cost is flat in
    // the count, and the dots come out crisper because nothing antialiases them.
    static void FillRects(MeshGenerationContext context, List<Rect> rects, Color color)
    {
        // An allocation holds 65535 vertices; batching well under it costs nothing and
        // means a plane that grows never has to be thought about again.
        const int BatchQuads = 16000;

        for (var start = 0; start < rects.Count; start += BatchQuads)
        {
            var count = Mathf.Min(BatchQuads, rects.Count - start);
            var mesh = context.Allocate(count * 4, count * 6);

            for (var i = 0; i < count; i++)
            {
                var r = rects[start + i];
                var v = (ushort)(i * 4);

                mesh.SetNextVertex(new Vertex
                  { position = new Vector3(r.xMin, r.yMax, Vertex.nearZ), tint = color });
                mesh.SetNextVertex(new Vertex
                  { position = new Vector3(r.xMin, r.yMin, Vertex.nearZ), tint = color });
                mesh.SetNextVertex(new Vertex
                  { position = new Vector3(r.xMax, r.yMin, Vertex.nearZ), tint = color });
                mesh.SetNextVertex(new Vertex
                  { position = new Vector3(r.xMax, r.yMax, Vertex.nearZ), tint = color });

                mesh.SetNextIndex(v);
                mesh.SetNextIndex((ushort)(v + 1));
                mesh.SetNextIndex((ushort)(v + 2));
                mesh.SetNextIndex((ushort)(v + 2));
                mesh.SetNextIndex((ushort)(v + 3));
                mesh.SetNextIndex(v);
            }
        }
    }

    static void RoundedRect(Painter2D painter, Rect rect, float radius)
    {
        var (x0, y0, x1, y1) = (rect.xMin, rect.yMin, rect.xMax, rect.yMax);

        painter.MoveTo(new Vector2(x0 + radius, y0));
        painter.LineTo(new Vector2(x1 - radius, y0));
        painter.ArcTo(new Vector2(x1, y0), new Vector2(x1, y0 + radius), radius);
        painter.LineTo(new Vector2(x1, y1 - radius));
        painter.ArcTo(new Vector2(x1, y1), new Vector2(x1 - radius, y1), radius);
        painter.LineTo(new Vector2(x0 + radius, y1));
        painter.ArcTo(new Vector2(x0, y1), new Vector2(x0, y1 - radius), radius);
        painter.LineTo(new Vector2(x0, y0 + radius));
        painter.ArcTo(new Vector2(x0, y0), new Vector2(x0 + radius, y0), radius);
        painter.ClosePath();
    }

    // A polyline with its corners rounded off, after the mockup's routing.
    static void RoundedPath(Painter2D painter, List<Vector2> points, float radius)
    {
        painter.MoveTo(points[0]);

        for (var i = 1; i < points.Count - 1; i++)
        {
            painter.LineTo(Toward(points[i], points[i - 1], radius));
            painter.QuadraticCurveTo(points[i], Toward(points[i], points[i + 1], radius));
        }

        painter.LineTo(points[points.Count - 1]);
    }

    static Vector2 Toward(Vector2 from, Vector2 to, float distance)
    {
        var delta = to - from;
        var length = Mathf.Max(delta.magnitude, 1e-5f);
        return from + delta * (Mathf.Min(distance, length / 2) / length);
    }
}

// A transparent layer that only paints. Kept separate from the cells so that the
// lattice and the cursor can be redrawn at their own rates.
sealed class PaintLayer : VisualElement
{
    public PaintLayer(Action<MeshGenerationContext> draw)
    {
        generateVisualContent += draw;
        pickingMode = PickingMode.Ignore;
        this.StretchToParentSize();
    }
}

} // namespace Jacquard.App
