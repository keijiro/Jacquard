using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// The cell icons, ported from the SVG in mockup.html.
//
// Everything is drawn in a 15x15 box with 1px strokes on half-integer coordinates
// so that a stroke centre lands on a pixel boundary and stays crisp. The two gate
// icons have boxes of their own size, because their shape carries a value: the
// cycle gate shows its laps as a block of boxes and the probability gate shows its
// chance as a wedge, so the cell shows the rough figure and the dialog takes the
// exact one.

static class TileIcons
{
    const float Size = 15.0f;
    const float Top = 1.5f;
    const float Bottom = 13.5f;

    // Which tiles are drawn rather than labelled. A channel head shows text and a
    // note shows its own name; everything else is an icon.
    public static bool HasIcon(Tile tile)
      => tile is ParamTile or GateTile or TerminatorTile or JumpTile or JumpDestTile;

    public static void Draw(Painter2D painter, Tile tile, Color color)
    {
        painter.strokeColor = color;
        painter.fillColor = color;
        painter.lineWidth = 1.0f;
        painter.lineJoin = LineJoin.Round;
        painter.lineCap = LineCap.Round;

        var o = new Vector2(Mathf.Floor((Style.CellWidth - Size) / 2),
                            Mathf.Floor((Style.CellHeight - Size) / 2));

        switch (tile)
        {
            case AbsoluteParamTile:
                painter.BeginPath();
                Fader(painter, o, 7.5f, 6.0f);
                painter.Stroke();
                break;

            case RelativeParamTile:
                painter.BeginPath();
                Fader(painter, o, 4.5f, 6.0f);
                UpDown(painter, o, 11.5f);
                painter.Stroke();
                break;

            case CycleGateTile cycle:
                Cycle(painter, cycle);
                break;

            case ProbGateTile prob:
                Prob(painter, prob.Percent);
                break;

            case TerminatorTile:
                UTurn(painter, o);
                break;

            case JumpTile:
                Zigzag(painter, o);
                break;

            case JumpDestTile:
                Entry(painter, o);
                break;
        }
    }

    // Parameter locks: a fader, with a modifier on the right saying which kind.

    static void Fader(Painter2D painter, Vector2 o, float cx, float cy)
    {
        const float kw = 6.0f, kh = 3.0f;
        var top = cy - kh / 2;
        var bottom = cy + kh / 2;

        Line(painter, o, cx, Top, cx, top);
        Line(painter, o, cx, bottom, cx, Bottom);
        Box(painter, o, cx - kw / 2, top, kw, kh);
    }

    static void UpDown(Painter2D painter, Vector2 o, float cx)
    {
        const float hw = 2.0f, hh = 2.8f;

        Line(painter, o, cx, Top, cx, Bottom);
        Chevron(painter, o, cx, Top, Top + hh, hw);
        Chevron(painter, o, cx, Bottom, Bottom - hh, hw);
    }

    // Gates

    // One box per lap, the laps it fires on filled. Four to a line and a second line
    // under them once there are more than four, because the period now reaches
    // thirty-two and a single row of that many would be a box a pixel wide against a
    // pixel of ground: the cell is thirty pixels across whatever the period is, so
    // what has to give is the number of them on a line.
    //
    // Past eight it stops counting and says so. Six boxes and an ellipsis is a figure
    // rather than a count — nobody reads twelve boxes off a cell either way — and
    // filled against hollow is what carries even the short periods, which is why the
    // shown ones stay the size they were rather than shrinking to admit the rest.
    //
    // Six is also what leaves the ellipsis somewhere to stand: two boxes on the second
    // line leave two boxes' worth of ground at the bottom right, so the dots cost the
    // figure no width at all and an elided icon is the same block as a full one.
    static void Cycle(Painter2D painter, CycleGateTile cycle)
    {
        var period = cycle.Period;
        var elided = period > Shown;
        var count = elided ? Elided : period;

        var columns = Mathf.Min(count, Columns);
        var rows = (count + Columns - 1) / Columns;

        // Fitted to the cell rather than to numbers of its own, so that changing the
        // cell pitch cannot push the widest row out past the edges.
        var span = Style.CellWidth - Margin * 2;

        var w = Mathf.Min(5.0f, Mathf.Floor((span + Space) / columns) - Space);
        var h = rows > 1 ? 6.0f : 8.0f;

        var width = columns * (w + Space) - Space + 1;
        var height = rows * (h + Space) - Space + 1;

        var o = new Vector2(Mathf.Floor((Style.CellWidth - width) / 2),
                            Mathf.Floor((Style.CellHeight - height) / 2));

        // Two passes over the same run, since one path cannot be both filled and
        // stroked.
        for (var pass = 0; pass < 2; pass++)
        {
            painter.BeginPath();

            for (var i = 0; i < count; i++)
                if (cycle.Fires(i + 1) == (pass == 0))
                    Box(painter, o, i % Columns * (w + Space) + 0.5f,
                        i / Columns * (h + Space) + 0.5f, w, h);

            if (pass == 0) painter.Fill(FillRule.NonZero); else painter.Stroke();
        }

        if (!elided) return;

        // In the ground the second line leaves, centred across the boxes that are
        // missing from it and on the line those boxes would have sat on: the run reads
        // to the end of the second line and the dots are where it would have carried
        // on.
        //
        // Whole pixels rather than the half pixel centres the boxes are stroked on: a
        // fill is crisp where its edges are, and a dot one pixel across has no centre
        // to put anywhere.
        var last = count - Columns;
        var free = (Columns - last) * (w + Space) - Space + 1;

        painter.BeginPath();
        for (var i = 0; i < 3; i++)
            Box(painter, o, last * (w + Space) + Mathf.Floor((free - DotSpan) / 2)
                            + i * DotPitch,
                (rows - 1) * (h + Space) + Mathf.Floor(h / 2), 1.0f, 1.0f);
        painter.Fill(FillRule.NonZero);
    }

    // What the row of boxes is laid out to. Four is what a thirty pixel cell reads at
    // once a box has to hold a fill; the run stops at eight, which is two full lines,
    // and what is left standing past that is six and the ellipsis that says so.
    const int Columns = 4, Shown = 8, Elided = 6;

    // The ground the block keeps clear of the tile's border on either side.
    //
    // It used to be a pixel and a half, which is what a row of eight across a thirty
    // pixel cell costs and what made the icon sit against its own outline. The boxes
    // are what gives way for it: this is the one icon whose shape carries a value, and
    // the value it carries is a shape to recognise rather than a count to take off the
    // cell — the panel is where the laps are read one at a time. So the block is now
    // held off the border about as far as it is held off the cell above and below it,
    // and a box is three pixels where it used to be five.
    const float Margin = 5.0f;

    // The pitch between two boxes, which leaves one pixel of ground between their
    // strokes.
    const float Space = 2.0f;

    // Three dots one pixel across, and two pixels of ground between them rather than
    // the one the boxes get. A dot is the smallest mark on the cell and the only one
    // with nothing drawn around it, so it is the first thing to go when the panel
    // lands on a fractional scale — which it does on any screen the reference DPI does
    // not divide. At a one pixel gap the smear closes and the ellipsis reads as a
    // dash; at two it stays three dots, which is the whole of what it has to say.
    const float DotPitch = 3.0f, DotSpan = DotPitch * 2 + 1;

    // The firing chance as a filled wedge inside a ring.
    static void Prob(Painter2D painter, float percent)
    {
        const float c = 5.5f, r = 5.0f;

        var o = new Vector2(Mathf.Floor((Style.CellWidth - 11.0f) / 2),
                            Mathf.Floor((Style.CellHeight - 11.0f) / 2));
        var center = o + new Vector2(c, c);

        if (percent >= 100.0f)
        {
            painter.BeginPath();
            painter.Arc(center, r, Angle.Degrees(0), Angle.Degrees(360));
            painter.Fill(FillRule.NonZero);
        }
        else if (percent > 0.0f)
        {
            painter.BeginPath();
            painter.MoveTo(center);
            painter.LineTo(center + new Vector2(0, -r));
            painter.Arc(center, r, Angle.Degrees(-90),
                        Angle.Degrees(-90.0f + percent / 100.0f * 360.0f));
            painter.ClosePath();
            painter.Fill(FillRule.NonZero);
        }

        painter.BeginPath();
        painter.Arc(center, r, Angle.Degrees(0), Angle.Degrees(360));
        painter.Stroke();
    }

    // Flow

    // Runs right along the top, turns back on itself and leaves to the left.
    static void UTurn(Painter2D painter, Vector2 o)
    {
        const float y0 = 2.5f, y1 = 10.5f, xr = 9.5f;

        painter.BeginPath();
        Line(painter, o, 2.5f, y0, xr, y0);
        painter.MoveTo(o + new Vector2(xr, y0));
        painter.Arc(o + new Vector2(xr, (y0 + y1) / 2), (y1 - y0) / 2,
                    Angle.Degrees(-90), Angle.Degrees(90));
        Line(painter, o, xr, y1, 4.4f, y1);
        painter.Stroke();

        ArrowHead(painter, o, 2.0f, 5.0f, y1, 2.4f);
    }

    // A Z rounded off at both turns: the sequence leaves this lane.
    static void Zigzag(Painter2D painter, Vector2 o)
    {
        const float y0 = 3.5f, y1 = 10.5f;
        const float x0 = 2.5f, x1 = 10.5f, x2 = 4.5f, x3 = 10.4f;
        const float r = 1.7f;

        var length = Mathf.Sqrt((x2 - x1) * (x2 - x1) + (y1 - y0) * (y1 - y0));
        var ux = (x2 - x1) / length * r;
        var uy = (y1 - y0) / length * r;

        painter.BeginPath();
        painter.MoveTo(o + new Vector2(x0, y0));
        painter.LineTo(o + new Vector2(x1 - r, y0));
        painter.QuadraticCurveTo(o + new Vector2(x1, y0),
                                 o + new Vector2(x1 + ux, y0 + uy));
        painter.LineTo(o + new Vector2(x2 - ux, y1 - uy));
        painter.QuadraticCurveTo(o + new Vector2(x2, y1),
                                 o + new Vector2(x2 + r, y1));
        painter.LineTo(o + new Vector2(x3, y1));
        painter.Stroke();

        ArrowHead(painter, o, x3 + 3.0f, x3, y1, 2.2f);
    }

    // An arrow rising out of a bar: where a jump lands and a lane begins.
    static void Entry(Painter2D painter, Vector2 o)
    {
        const float cy = 7.5f, x = 2.5f;

        painter.BeginPath();
        Line(painter, o, x, 3.5f, x, 11.5f);
        Line(painter, o, x, cy, 10.4f, cy);
        painter.Stroke();

        ArrowHead(painter, o, 13.4f, 10.4f, cy, 2.2f);
    }

    // Primitives

    static void Line(Painter2D painter, Vector2 o, float x1, float y1,
                     float x2, float y2)
    {
        painter.MoveTo(o + new Vector2(x1, y1));
        painter.LineTo(o + new Vector2(x2, y2));
    }

    static void Box(Painter2D painter, Vector2 o, float x, float y,
                    float w, float h)
    {
        painter.MoveTo(o + new Vector2(x, y));
        painter.LineTo(o + new Vector2(x + w, y));
        painter.LineTo(o + new Vector2(x + w, y + h));
        painter.LineTo(o + new Vector2(x, y + h));
        painter.ClosePath();
    }

    static void Chevron(Painter2D painter, Vector2 o, float cx, float tipY,
                        float baseY, float hw)
    {
        painter.MoveTo(o + new Vector2(cx - hw, baseY));
        painter.LineTo(o + new Vector2(cx, tipY));
        painter.LineTo(o + new Vector2(cx + hw, baseY));
    }

    static void ArrowHead(Painter2D painter, Vector2 o, float tipX, float baseX,
                          float cy, float hw)
    {
        painter.BeginPath();
        painter.MoveTo(o + new Vector2(tipX, cy));
        painter.LineTo(o + new Vector2(baseX, cy - hw));
        painter.LineTo(o + new Vector2(baseX, cy + hw));
        painter.ClosePath();
        painter.Fill(FillRule.NonZero);
    }
}

} // namespace Jacquard.App
