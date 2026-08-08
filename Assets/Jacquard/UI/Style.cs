using UnityEngine;

namespace Jacquard.App {

// Metrics and colours, taken from mockup.html.
//
// The cell pitch is what everything else is derived from: a cell is 30x32 with a
// 4px gutter, so a column is 34 apart and a row 36. Keeping those numbers in one
// place is what lets the painted layers and the tile elements agree on where a
// cell is to the pixel.
//
// The pitch is set by what has to fit inside a cell rather than by taste: a note
// name at NoteSize with its accidental gutter is a little over twenty pixels wide,
// and stacked over a length label it is a little under thirty tall. What is left
// over is margin, and it is kept thin, because a plane holds more of the score the
// tighter the pitch is and the score is what this window is for.

static class Style
{
    // Metrics

    public const float CellWidth = 30.0f;
    public const float CellHeight = 32.0f;
    public const float Gap = 4.0f;
    public const float Radius = 5.0f;
    public const float Padding = 18.0f;

    public const float StrideX = CellWidth + Gap;
    public const float StrideY = CellHeight + Gap;

    public const float NoteSize = 15.0f;
    public const float LengthSize = 9.0f;
    public const float ControlSize = 11.0f;

    // The sharp is typeset as a small raised glyph in a fixed gutter between the
    // note letter and the octave, so every note label has the same rhythm whether
    // it carries an accidental or not.
    public const float AccidentalGutter = 5.0f;
    public const float AccidentalSize = NoteSize * 0.62f;
    public const float AccidentalRise = NoteSize * 0.42f;

    // The dotted rail: a 2px dot every 7px, at 35% so that it reads as a guide
    // rather than as content.
    public const float RailDot = 2.0f;
    public const float RailStep = 7.0f;
    public const float RailOpacity = 0.35f;

    // Jump links run off-axis, right of the column centres and below the row
    // centres, so that they read as a separate layer from the rails they cross.
    // The half pixel keeps the 1px stroke on a pixel boundary.
    public const float LinkOffset = 7.5f;
    public const float LinkRadius = 6.0f;

    public const float LatticeDot = 2.0f;

    // Colours

    public static readonly Color Background = Hex(0x16161a);
    public static readonly Color NoteLine = Hex(0xe8e8e4);
    public static readonly Color NoteText = Hex(0xf2f2ee);
    public static readonly Color Marker = Hex(0x9a9a96);
    public static readonly Color Dot = Hex(0x4e4e54);
    public static readonly Color ControlBackground = Hex(0x34343c);
    public static readonly Color ControlHover = Hex(0x44444e);
    public static readonly Color Link = Hex(0x86868c);

    // A value bar's fill, and the same while it is being dragged. Grey rather than a
    // colour of its own, since nothing else here is coloured: it has to stay light
    // enough to be read against the box it fills and dark enough to read the value
    // over, which is printed on top of it.
    public static readonly Color Fill = Hex(0x6c6c76);
    public static readonly Color FillActive = Hex(0x84848e);

    public static readonly Color Panel = Hex(0x1e1e24);
    public static readonly Color PanelLine = Hex(0x3a3a44);
    public static readonly Color Label = Hex(0x9a9a96);

    public static readonly Color Cursor = Hex(0xf2f2ee);
    public static readonly Color Playhead = Hex(0xf2f2ee);

    public static Color Hex(uint rgb)
      => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

    public static Color Fade(Color color, float alpha)
      => new Color(color.r, color.g, color.b, alpha);

    // Geometry helpers

    // Top left corner of a cell in plane coordinates.
    public static Vector2 CellOrigin(GridPoint point)
      => new Vector2(Padding + point.X * StrideX, Padding + point.Y * StrideY);

    public static Vector2 CellCenter(GridPoint point)
      => CellOrigin(point) + new Vector2(CellWidth / 2, CellHeight / 2);

    public static Rect CellRect(GridPoint point)
      => new Rect(CellOrigin(point), new Vector2(CellWidth, CellHeight));

    // Which cell a plane coordinate falls in. Points in a gutter belong to the
    // cell above and to the left of them, so there is no dead zone.
    public static GridPoint CellAt(Vector2 position)
      => new GridPoint(Mathf.FloorToInt((position.x - Padding) / StrideX),
                       Mathf.FloorToInt((position.y - Padding) / StrideY));

    public static Vector2 PlaneSize(int columns, int rows)
      => new Vector2(Padding * 2 + columns * StrideX - Gap,
                     Padding * 2 + rows * StrideY - Gap);
}

} // namespace Jacquard.App
