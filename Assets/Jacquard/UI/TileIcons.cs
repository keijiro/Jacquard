using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// The cell icons, ported from the SVG in bp.html.
//
// Everything is drawn in a 15x15 box with 1px strokes on half-integer coordinates
// so that a stroke centre lands on a pixel boundary and stays crisp. The two gate
// icons have boxes of their own size, because their shape carries a value: the
// cycle gate shows its period as a row of boxes and the probability gate shows its
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

            case AccumParamTile:
                painter.BeginPath();
                Fader(painter, o, 4.5f, 6.0f);
                painter.Stroke();
                Delta(painter, o, 11.5f, 4.5f, 11.5f, 2.8f);
                break;

            case CycleGateTile cycle:
                Cycle(painter, cycle.Period, cycle.Index);
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

    // A typographic delta: thin left and base strokes, a heavier right one. Each
    // edge is pushed inward by its own weight and the resulting inner triangle is
    // punched out of the outline, so the corners miter cleanly instead of growing
    // a lump where strokes of different weights meet.
    static void Delta(Painter2D painter, Vector2 o, float cx, float apexY,
                      float baseY, float hw)
    {
        const float thin = 1.0f, thick = 1.6f;

        var h = baseY - apexY;
        var s = Mathf.Sqrt(hw * hw + h * h);
        var lx = cx + thin * h / s;
        var ly = apexY + thin * hw / s;
        var rx = cx - thick * h / s;
        var ry = apexY + thick * hw / s;
        var by = baseY - thin;

        painter.BeginPath();

        painter.MoveTo(o + new Vector2(cx, apexY));
        painter.LineTo(o + new Vector2(cx - hw, baseY));
        painter.LineTo(o + new Vector2(cx + hw, baseY));
        painter.ClosePath();

        painter.MoveTo(o + Cross(lx, ly, -hw, h, rx, ry, -hw, -h));
        painter.LineTo(o + new Vector2(lx - hw * (by - ly) / h, by));
        painter.LineTo(o + new Vector2(rx + hw * (by - ry) / h, by));
        painter.ClosePath();

        painter.Fill(FillRule.OddEven);
    }

    // Gates

    // One box per lap, the firing one filled. The boxes narrow as the period grows
    // so that the row stays inside the cell; even at the tightest spacing the
    // filled one still reads, because filled against hollow is a stronger contrast
    // than wide against narrow.
    static void Cycle(Painter2D painter, int period, int index)
    {
        const float h = 8.0f;

        var gap = period > 6 ? 1 : 2;
        var w = Mathf.Min(5, Mathf.FloorToInt((31.0f + gap) / period) - gap);
        var total = period * (w + gap) - gap + 1;

        var o = new Vector2(Mathf.Floor((Style.CellWidth - total) / 2),
                            Mathf.Floor((Style.CellHeight - (h + 1)) / 2));

        painter.BeginPath();
        for (var i = 0; i < period; i++)
            if (i == index - 1) Box(painter, o, i * (w + gap) + 0.5f, 0.5f, w, h);
        painter.Fill(FillRule.NonZero);

        painter.BeginPath();
        for (var i = 0; i < period; i++)
            if (i != index - 1) Box(painter, o, i * (w + gap) + 0.5f, 0.5f, w, h);
        painter.Stroke();
    }

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

    static Vector2 Cross(float px, float py, float dx, float dy,
                         float qx, float qy, float ex, float ey)
    {
        var t = ((qx - px) * ey - (qy - py) * ex) / (dx * ey - dy * ex);
        return new Vector2(px + dx * t, py + dy * t);
    }
}

} // namespace Jacquard.App
