using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// One occupied cell.
//
// Three looks for four categories: a note is outlined, a parameter lock or a gate
// sits on a grey field, and a flow tile is filled solid because it does something
// other than carry on to the right. sequencer.md notes that this leaves locks and
// gates telling apart only by their position in a stack, and that the look is what
// will eventually give, not the categories.
//
// Cells take no input of their own: the plane below resolves a click into a grid
// coordinate, which it has to do anyway to let an empty cell be typed into.

sealed class TileElement : VisualElement
{
    public Tile Tile { get; }

    // The cell it was built for. A drag needs to find the elements standing on a
    // run of cells, and the position is the only thing that tells two of them
    // apart: the terminator tile is one shared instance across every lane.
    public GridPoint Point { get; }

    public TileElement(Tile tile, GridPoint point)
    {
        (Tile, Point) = (tile, point);

        var origin = Style.CellOrigin(point);

        style.position = Position.Absolute;
        style.left = origin.x;
        style.top = origin.y;
        style.width = Style.CellWidth;
        style.height = Style.CellHeight;
        style.alignItems = Align.Center;
        style.justifyContent = Justify.Center;
        pickingMode = PickingMode.Ignore;

        SetBorderRadius(this, Style.Radius);

        // Anything that does not simply carry on to the right is a solid cell.
        var inverted = tile is FlowTile;

        _color = inverted ? Style.Background : Style.NoteText;

        if (tile is NoteTile note)
        {
            style.backgroundColor = Style.Background;
            SetBorderWidth(this, 1.0f);
            SetBorderColor(this, Style.NoteLine);
            Add(NoteLabel(note));
        }
        else if (inverted)
        {
            style.backgroundColor = Style.NoteLine;
        }
        else
        {
            style.backgroundColor = Style.ControlBackground;
        }

        if (tile is ChannelTile channel)
            Add(Text("CH" + channel.Channel, Style.ControlSize, _color));
        else if (TileIcons.HasIcon(tile))
            generateVisualContent += OnGenerateVisualContent;
    }

    // Private members

    readonly Color _color;

    void OnGenerateVisualContent(MeshGenerationContext context)
      => TileIcons.Draw(context.painter2D, Tile, _color);

    // A note reads as letter and octave, with the gutter the sharp is drawn in between
    // them only when there is a sharp.
    //
    // The gutter used to stand on every note, so that the letter kept its place as a
    // note was transposed through a sharp and back. That was a gap in the middle of
    // every plain name to spare a movement no one was watching for: five pixels of the
    // twenty a name has to fit in, spent on the two thirds of notes that have nothing
    // to put there. A name is read, not aligned against the name it was a moment ago.
    //
    // The length only appears when it is not the default single step.
    static VisualElement NoteLabel(NoteTile note)
    {
        var column = new VisualElement();
        column.style.alignItems = Align.Center;
        column.style.justifyContent = Justify.Center;
        column.pickingMode = PickingMode.Ignore;

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.pickingMode = PickingMode.Ignore;

        var name = Pitch.ToClassName(note.Note);

        row.Add(Text(name.Substring(0, 1), Style.NoteSize, Style.NoteText));
        if (name.Length > 1) row.Add(Accidental());
        row.Add(Text(Pitch.ToOctave(note.Note).ToString(), Style.NoteSize,
                     Style.NoteText));

        column.Add(row);

        if (!note.HasDefaultLength)
        {
            var length = Text(note.Length.ToString("0.###"), Style.LengthSize,
                              Style.Fade(Style.NoteText, 0.7f));
            length.style.marginTop = -2;
            column.Add(length);
        }

        return column;
    }

    // The sharp is drawn rather than typeset: it is the one glyph in the whole UI
    // that a runtime font may not carry, and at this size a few strokes are more
    // legible than a scaled down character anyway.
    //
    // Its box is a fixed five pixels rather than a share of the note size, because what
    // it holds is four 1px strokes: a gutter that scaled would put them on half pixels
    // at most sizes, and the mark is small enough that a blurred one is a smudge.
    static VisualElement Accidental()
    {
        var element = new VisualElement();
        element.style.width = Style.AccidentalGutter;
        element.style.height = Style.NoteSize;
        element.style.flexShrink = 0;
        element.pickingMode = PickingMode.Ignore;

        element.generateVisualContent += context =>
        {
            var painter = context.painter2D;
            painter.strokeColor = Style.NoteText;
            painter.lineWidth = 1.0f;

            // Two uprights and two rails with a slight rise, sitting in the upper
            // half of the line box so that it reads as a superscript.
            const float top = 1.0f, bottom = 8.5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(1.5f, top + 0.6f));
            painter.LineTo(new Vector2(1.5f, bottom));
            painter.MoveTo(new Vector2(3.5f, top));
            painter.LineTo(new Vector2(3.5f, bottom - 0.6f));
            painter.MoveTo(new Vector2(0.5f, 4.2f));
            painter.LineTo(new Vector2(4.5f, 3.4f));
            painter.MoveTo(new Vector2(0.5f, 6.4f));
            painter.LineTo(new Vector2(4.5f, 5.6f));
            painter.Stroke();
        };

        return element;
    }

    // Shared helpers

    // The default theme gives a Label a top margin and vertical padding of its own.
    // Everything here is positioned by hand, so those have to go: left on they push
    // the text out of line with whatever it labels.
    public static Label Text(string text, float size, Color color)
    {
        var label = new Label(text);
        label.style.fontSize = size;
        label.style.color = color;
        SetMargin(label, 0.0f);
        SetPadding(label, 0.0f);
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.pickingMode = PickingMode.Ignore;
        return label;
    }

    public static void SetBorderWidth(VisualElement element, float value)
      => (element.style.borderLeftWidth, element.style.borderRightWidth,
          element.style.borderTopWidth, element.style.borderBottomWidth) =
         (value, value, value, value);

    public static void SetBorderColor(VisualElement element, Color color)
      => (element.style.borderLeftColor, element.style.borderRightColor,
          element.style.borderTopColor, element.style.borderBottomColor) =
         (color, color, color, color);

    public static void SetBorderRadius(VisualElement element, float value)
      => (element.style.borderTopLeftRadius, element.style.borderTopRightRadius,
          element.style.borderBottomLeftRadius, element.style.borderBottomRightRadius) =
         (value, value, value, value);

    public static void SetMargin(VisualElement element, float value)
      => (element.style.marginLeft, element.style.marginRight,
          element.style.marginTop, element.style.marginBottom) =
         (value, value, value, value);

    public static void SetPadding(VisualElement element, float value)
      => (element.style.paddingLeft, element.style.paddingRight,
          element.style.paddingTop, element.style.paddingBottom) =
         (value, value, value, value);
}

} // namespace Jacquard.App
