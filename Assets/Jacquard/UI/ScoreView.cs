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

    public Score Score { get; set; }
    public Sequencer Sequencer { get; set; }

    public GridPoint Cursor { get; private set; } = new GridPoint(1, 1);

    public event Action CursorMoved;
    public event Action<KeyDownEvent> KeyPressed;

    // Raised with the rect the cursor now occupies, so that a container can bring
    // it into view.
    public event Action<Rect> RevealRequested;

    // Raised on a double click, once the cursor has been put on the cell that was
    // hit: a second click on a cell means the tile that cell usually gets.
    public event Action DoubleClicked;

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

        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<KeyDownEvent>(OnKeyDown);
    }

    // Refreshing

    // Called whenever the score has been edited. Cells are rebuilt outright: an
    // edit is a human-paced event and a score holds tens of tiles, so there is
    // nothing to gain from reconciling them one by one.
    public void Rebuild()
    {
        Resize();

        _tiles.Clear();

        foreach (var lane in Score.Lanes)
        {
            _tiles.Add(new TileElement(lane.Head, lane.HeadPoint));
            _tiles.Add(new TileElement(Score.Terminator, lane.TermPoint));

            for (var i = 0; i < lane.Steps.Count; i++)
            {
                var step = lane.Steps[i];
                for (var d = 0; d < step.Depth; d++)
                    _tiles.Add(new TileElement(step.Tiles[d], lane.CellPoint(i, d)));
            }
        }

        _lower.MarkDirtyRepaint();
        _upper.MarkDirtyRepaint();
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

    // The plane is kept a good deal larger than the score so that there is always
    // empty ground to put a new lane on.
    void Resize()
    {
        _columns = Mathf.Max(48, Score.Width + 10);
        _rows = Mathf.Max(28, Score.Height + 8);

        var size = Style.PlaneSize(_columns, _rows);
        style.width = size.x;
        style.height = size.y;
    }

    // Painting

    void DrawLower(MeshGenerationContext context)
    {
        if (Score == null) return;

        var painter = context.painter2D;

        DrawLattice(painter);
        DrawRails(painter);
        DrawChains(painter);
        DrawLinks(painter);
        DrawMarkers(painter);
    }

    // The lattice shows through only where nothing else is: a step that holds
    // nothing gets a pass-through marker instead of a dot.
    void DrawLattice(Painter2D painter)
    {
        painter.fillColor = Style.Dot;
        painter.BeginPath();

        // Square dots rather than round ones: a path holding hundreds of arc
        // subpaths only fills the first of them, and at this size the difference
        // is invisible anyway.
        for (var y = 0; y < _rows; y++)
            for (var x = 0; x < _columns; x++)
            {
                var point = new GridPoint(x, y);
                if (Score.At(point).Kind != CellKind.Empty) continue;

                var center = Style.CellCenter(point);
                Rect(painter, new Rect(center.x - Style.LatticeDot / 2,
                                       center.y - Style.LatticeDot / 2,
                                       Style.LatticeDot, Style.LatticeDot));
            }

        painter.Fill(FillRule.NonZero);
    }

    // Each lane's own time axis, from its head to its terminator.
    void DrawRails(Painter2D painter)
    {
        painter.fillColor = Style.Fade(Style.NoteLine, Style.RailOpacity);
        painter.BeginPath();

        foreach (var lane in Score.Lanes)
        {
            var from = Style.CellCenter(lane.HeadPoint).x;
            var to = Style.CellCenter(lane.TermPoint).x;
            var y = Mathf.Floor(Style.CellCenter(lane.HeadPoint).y) - Style.RailDot / 2;

            for (var x = from; x < to; x += Style.RailStep)
                Rect(painter, new Rect(x, y, Style.RailDot, Style.RailDot));
        }

        painter.Fill(FillRule.NonZero);
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
    }

    // Input

    void OnPointerDown(PointerDownEvent evt)
    {
        // A modified drag pans the plane instead of editing it.
        if (ScrollArea.IsPanModifierHeld(evt)) return;

        Focus();
        SetCursor(Style.CellAt(evt.localPosition));
        if (evt.clickCount >= 2) DoubleClicked?.Invoke();
        evt.StopPropagation();
    }

    void OnKeyDown(KeyDownEvent evt) => KeyPressed?.Invoke(evt);

    // Private members

    readonly VisualElement _tiles;
    readonly PaintLayer _lower;
    readonly PaintLayer _upper;

    readonly List<(Lane lane, int step)> _playheads = new();
    readonly List<(Lane lane, int step)> _paintedPlayheads = new();
    readonly List<Vector2> _path = new();

    int _columns = 48;
    int _rows = 28;

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
