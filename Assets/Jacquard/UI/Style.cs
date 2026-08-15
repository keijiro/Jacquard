using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// Metrics and colours, taken from mockup.html.
//
// The cell pitch is what everything else is derived from: a cell is 30x32 with a
// 4px gutter, so a column is 34 apart and a row 36. Keeping those numbers in one
// place is what lets the painted layers and the tile elements agree on where a
// cell is to the pixel.
//
// The pitch is set by what has to fit inside a cell rather than by taste: a sharp
// note name at NoteSize, letter and gutter and octave, is a little over twenty pixels
// wide, and stacked over a length label it is a little under thirty tall. What is left
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

    public const float NoteSize = 13.0f;
    public const float LengthSize = 9.0f;
    public const float ControlSize = 11.0f;

    // The gutter the sharp is drawn in, between the note letter and the octave. It is
    // there only when there is a sharp to put in it: a note that has none reads as two
    // characters side by side.
    public const float AccidentalGutter = 5.0f;

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

    // How far back something goes when it is on the screen but out of reach: a mute
    // that is not being consulted, a released lock row, the score while another one is
    // waiting to come in. Dimmed whole rather than greyed control by control, since a
    // control disabled in the layout engine's sense brings the default theme's idea of
    // grey with it rather than this one's.
    public const float DimmedOpacity = 0.45f;

    // Colours
    //
    // One number each, because the palette is grey through and through. It used to
    // carry a hint of blue in the dark end and of warmth in the light one — a tint
    // small enough that no one would name it, but enough that the two ends leaned
    // opposite ways and the mid greys sat between two hues rather than on one scale.
    // What is left is the ramp that was doing the work all along, and Grey takes a
    // single value so there is no longer a place to put a tint back.

    public static readonly Color Background = Grey(0x16);
    public static readonly Color NoteLine = Grey(0xe8);
    public static readonly Color NoteText = Grey(0xf2);
    public static readonly Color Marker = Grey(0x9a);
    public static readonly Color Dot = Grey(0x4e);
    public static readonly Color ControlBackground = Grey(0x34);
    public static readonly Color ControlHover = Grey(0x44);
    public static readonly Color Link = Grey(0x86);

    // What a control's ground does while a hand is on it: it moves along the ramp, one
    // step for a pointer over it and a longer one for a pointer pressing it.
    //
    // Which way it moves is decided by where the ground already is, and it has to be. A
    // dark ground goes lighter, which is what *engaged* means everywhere else here — a
    // lit switch, a solid cell, a bar opened to be typed into are all the pale end of the
    // ramp. A pale ground has nowhere to go in that direction: a lit switch is a hair
    // under white, so lifting it is a change of seven values that nobody can see, and a
    // switch that is on was left with no answer to a press at all. So it goes the other
    // way, into the ramp rather than off the end of it, and what the two cases have in
    // common is the thing that matters — the ground moves, and it moves further under a
    // press than under a hover.
    //
    // Two steps rather than one repeated, so that a press is not a hover with a longer
    // reaction time. The second is four times the first, and it is that far apart
    // because the first is deliberately small: a hover is a control saying it is under
    // the pointer, which is a whisper, and a press is the control answering a hand,
    // which has to be felt without being looked for. At two and a half times the hover
    // step — where this started — a press read as a hover that had drifted, and the
    // thing it was drifting from is already the faintest mark on the screen.
    //
    // Written as an amount applied to whatever ground a control is on rather than as a
    // second and third palette entry per control, since a switch has two grounds — dark
    // when it is off and pale when it is lit — and a hand on it means the same thing in
    // either state. From the ordinary dark ground the first step lands exactly on
    // ControlHover, which is the colour the bars were already lifting to before the
    // buttons did anything at all.
    public const float HoverStep = 0x10 / 255.0f;
    public const float PressStep = 0x40 / 255.0f;

    // Away from the end of the ramp the ground is already at, and clamped, since a step
    // is a fixed size and neither end of a byte wraps.
    public static Color UnderHand(Color ground, float amount)
    {
        var move = ground.grayscale > 0.5f ? -amount : amount;

        return new Color(Mathf.Clamp01(ground.r + move),
                         Mathf.Clamp01(ground.g + move),
                         Mathf.Clamp01(ground.b + move), ground.a);
    }

    // A value bar's fill, and the same while it is being dragged. It has to stay light
    // enough to be read against the box it fills and dark enough to read the value
    // over, which is printed on top of it.
    public static readonly Color Fill = Grey(0x6c);
    public static readonly Color FillActive = Grey(0x84);

    public static readonly Color Panel = Grey(0x1e);
    public static readonly Color PanelLine = Grey(0x3a);
    public static readonly Color Label = Grey(0x9a);

    public static readonly Color Cursor = Grey(0xf2);
    public static readonly Color Playhead = Grey(0xf2);

    // Ink
    //
    // Which of the two text colours a word is in, and how heavily it is cut.
    //
    // The face is monoline and light: one stroke weight from end to end, and a thin one.
    // No hairline to lose, then — but a light face has little to give either, and a dark
    // mark on a bright ground is eaten at its edges by the ground while a bright one on a
    // dark ground spreads into it. At the eleven and thirteen pixels this chrome is set
    // at that is a stroke's worth of difference between the two polarities, which on a
    // stroke this thin is most of the stroke.
    //
    // So the two grounds do not take the same weight. Dark ground is plain; light
    // ground — a lit switch, a solid cell, a bar opened to be typed into — is bold,
    // which here is the one cut dilated rather than a second one. Jura has real weights
    // up to 700, but what is checked in is one static instance of them, and a second
    // file to carry the bold would be a second thing to keep in step with the first for
    // a difference this synthesises well enough at these sizes.
    //
    // Dropping the weight and keeping the colour was tried, on the argument that a
    // synthesised bold is a face that does not exist standing next to one that does. It
    // reads as the better argument and it is the worse screen: plain type on the pale
    // ground is markedly harder to read at this size, which is the thing the rule was
    // written for and the thing the argument talked past. Reverted by eye, and the way
    // to settle it again is to look at a lit switch and not at this comment.
    //
    // The colour and the weight are set together and in one place because they are one
    // decision: there is no light ground in this UI that takes plain type, and no dark
    // one that takes bold.
    public static void SetInk(VisualElement element, bool onLight)
    {
        element.style.color = onLight ? Background : NoteText;
        element.style.unityFontStyleAndWeight =
          onLight ? FontStyle.Bold : FontStyle.Normal;
    }

    public static Color Grey(uint value)
      => new Color32((byte)value, (byte)value, (byte)value, 255);

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
