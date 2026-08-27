using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// One occupied cell.
//
// Three looks for four categories: a note is outlined, a parameter lock or a gate
// sits on a grey field, and a flow tile is filled solid because it does something
// other than carry on to the right. sequencer-spec.md notes that this leaves locks and
// gates telling apart only by their position in a stack, and that the look is what
// will eventually give, not the categories.
//
// Cells take no input of their own: the plane below resolves a click into a grid
// coordinate, which it has to do anyway to let an empty cell be typed into.
//
// A cell is built once and then repurposed. The constructor holds only what every
// cell has in common, and Apply hands a standing element whatever tile the score now
// has in its slot — the argument for that, and what it is worth, is in
// ScoreView.Rebuild, which is the one caller that reuses them.

sealed class TileElement : VisualElement
{
    public Tile Tile { get; private set; }

    // The cell it is standing on. A drag needs to find the elements standing on a
    // run of cells, and the position is the only thing that tells two of them
    // apart: the terminator tile is one shared instance across every lane.
    //
    // Off the plane to begin with, so that the first Apply writes a position even for
    // an element that lands on the corner cell.
    public GridPoint Point { get; private set; } = new GridPoint(-1, -1);

    // The shell: the part of a cell that is the same whichever tile stands on it.
    // Everything else waits for Apply.
    public TileElement()
    {
        style.position = Position.Absolute;
        style.width = Style.CellWidth;
        style.height = Style.CellHeight;
        style.alignItems = Align.Center;
        style.justifyContent = Justify.Center;
        pickingMode = PickingMode.Ignore;

        SetBorderRadius(this, Style.Radius);

        // Built once and assigned rather than subscribed, so that Draw can hand the
        // element its icon or take it away without allocating a delegate each time.
        _draw = OnGenerateVisualContent;
    }

    // For the callers with one cell to show and no element to spare — a ghost under
    // the hand, and the cell a rebuild finds past the end of the plane.
    public TileElement(Tile tile, GridPoint point, bool off = false) : this()
      => Apply(tile, point, off);

    // Puts this element on a cell. Moving it is a couple of style writes and nothing
    // else; the picture is redrawn only when what it is a picture of has changed.
    //
    // off is for the one tile that has a state as well as a kind: a channel start that
    // will not send a runner. It gives up the solid field for the grey one a lock or a
    // gate sits on, which is the pair of colours this UI already says on and off with —
    // Controls.SetActive dresses a switch in exactly these two.
    public void Apply(Tile tile, GridPoint point, bool off = false)
    {
        if (point != Point)
        {
            Point = point;
            var origin = Style.CellOrigin(point);
            style.left = origin.x;
            style.top = origin.y;
        }

        var look = Look.Of(tile, off);

        if (!look.Equals(_look))
        {
            (_look, Tile) = (look, tile);
            Draw(tile, off);
        }
        else if (TileIcons.IsStateful(tile))
            MarkDirtyRepaint();
    }

    // Private members

    Color _color;
    Look _look;

    readonly Action<MeshGenerationContext> _draw;

    // What a cell's picture is made of. Two equal looks draw the same thing, so an
    // element whose look has not moved can be left standing as it is.
    //
    // The tile is in here and is not enough on its own: a tile is edited in place — a
    // transpose writes the new pitch back into the same NoteTile — so the reference
    // says the cell is showing the right tile and the numbers say it is showing the
    // right thing about it. Nothing here allocates to compare.
    //
    // **A newly drawn field on a tile belongs here in the same change.** Left out, the
    // cell goes on showing what it showed before the edit — which is the one failure
    // this design can have, and the one the compiler cannot see. The exception is a
    // field an icon draws: TileIcons.IsStateful covers those instead.
    readonly struct Look : IEquatable<Look>
    {
        public static Look Of(Tile tile, bool off) => tile switch
        {
            NoteTile note => new Look(tile, off, note.Note, note.Length),
            ChannelTile channel => new Look(tile, off, channel.Channel, 0.0f),
            _ => new Look(tile, off, 0, 0.0f)
        };

        public bool Equals(Look other)
          => _tile == other._tile && _off == other._off &&
             _number == other._number && _length == other._length;

        Look(Tile tile, bool off, int number, float length)
          => (_tile, _off, _number, _length) = (tile, off, number, length);

        readonly Tile _tile;
        readonly bool _off;
        readonly int _number;
        readonly float _length;
    }

    // Draws the cell from scratch. The children go with the look, so they are made
    // again here; the styles on the element itself are written whatever they are worth,
    // including the zero border of a cell that has no border, so that after a cell's
    // first draw every property it will ever need is already on it and every later
    // write is free. Eight style writes at startup buys a repurposing that allocates
    // nothing.
    void Draw(Tile tile, bool off)
    {
        Clear();

        // Anything that does not simply carry on to the right is a solid cell.
        var inverted = tile is FlowTile && !off;

        _color = inverted ? Style.Background : Style.NoteText;

        // A note is the one cell that is drawn as an outline on the ground colour.
        var outlined = tile is NoteTile;

        style.backgroundColor = outlined ? Style.Background :
                                inverted ? Style.NoteLine : Style.ControlBackground;

        SetBorderWidth(this, outlined ? 1.0f : 0.0f);
        SetBorderColor(this, Style.NoteLine);

        if (tile is NoteTile note) Add(NoteLabel(note));

        if (tile is ChannelTile channel)
        {
            // The one cell with a word on it, and the only place the plane sets type
            // on a light ground: a channel start that is running is a solid cell.
            var label = Text("CH" + channel.Channel, Style.ControlSize, _color);
            Style.SetInk(label, inverted);
            Add(label);
        }

        // Assigning null is how a cell that used to draw an icon stops. The property
        // setter is not trusted to dirty the element, so say so.
        generateVisualContent = TileIcons.HasIcon(tile) ? _draw : null;
        MarkDirtyRepaint();
    }

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
