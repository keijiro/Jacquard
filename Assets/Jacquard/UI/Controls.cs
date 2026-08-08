using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// The flat monochrome widgets the panels are built from.
//
// Nothing here is a themed input field: everything is drawn from buttons, labels and
// bars this file styles itself, which keeps the chrome in the same visual language as
// the grid instead of inheriting a theme's idea of a slider.
//
// A number is set on a ValueBar, which reads out over a bar of its own and so shows
// where a value sits as well as what it is. What is left to the arrows either side of
// a figure is a choice out of a list, and the one count that cannot be scrubbed
// because changing it adds and removes cells.

static class Controls
{
    public const float LabelWidth = 74.0f;
    public const float RowHeight = 20.0f;

    // Every panel is this wide, which is also what a panel beside another one has to
    // step over to get out of its way.
    public const float PanelWidth = 226.0f;

    // The corner a control's box is cut to, a shade tighter than a cell's so that a
    // row of them does not read as more tiles.
    public const float Radius = 4.0f;

    public static Label Text(string text, float size, Color color)
      => TileElement.Text(text, size, color);

    public static Label Caption(string text)
    {
        var label = Text(text, 11.0f, Style.Label);
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.width = LabelWidth;
        label.style.flexShrink = 0;
        return label;
    }

    public static Label Value(string text)
    {
        var label = Text(text, 11.0f, Style.NoteText);
        label.style.flexGrow = 1;
        return label;
    }

    public static VisualElement Row()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.flexShrink = 0;
        row.style.marginBottom = 3;
        return row;
    }

    public static VisualElement Divider()
    {
        var line = new VisualElement();
        line.style.height = 1;
        line.style.flexShrink = 0;
        line.style.backgroundColor = Style.PanelLine;
        line.style.marginTop = 6;
        line.style.marginBottom = 6;
        return line;
    }

    public static Label Hint(string text)
    {
        var label = Text(text, 10.0f, Style.Hint);
        label.style.unityTextAlign = TextAnchor.UpperLeft;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginTop = 2;
        return label;
    }

    // Buttons

    public static Button Push(string text, Action onClick, float width = 0.0f)
    {
        var button = new Button(onClick) { text = text };
        button.style.fontSize = 11;
        button.style.height = RowHeight;
        button.style.minWidth = 0;
        button.style.marginLeft = 0;
        button.style.marginRight = 3;
        button.style.marginTop = 0;
        button.style.marginBottom = 0;
        button.style.paddingLeft = 6;
        button.style.paddingRight = 6;
        button.style.backgroundColor = Style.ControlBackground;
        button.style.color = Style.NoteText;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        TileElement.SetBorderWidth(button, 1.0f);
        TileElement.SetBorderColor(button, Style.PanelLine);
        TileElement.SetBorderRadius(button, Radius);
        if (width > 0.0f) button.style.width = width;
        return button;
    }

    // Shows whether what a button toggles is currently on.
    public static void SetActive(Button button, bool active)
    {
        button.style.backgroundColor = active ? Style.NoteLine : Style.ControlBackground;
        button.style.color = active ? Style.Background : Style.NoteText;
    }

    // Value bars

    // A labelled number, set on the bar that shows it. The bar takes whatever the
    // caption leaves of the row: the readout is printed on it, so unlike a stepper
    // there is nothing to put beside it.
    //
    // The value is passed as a getter and a setter rather than as a number, because
    // the same parameter is also changed from the grid and by a load, and a bar that
    // only wrote would go stale and then write its stale value back.
    public static VisualElement Bar(string caption, in ValueBar.Range range,
                                    Func<float> get, Action<float> set)
    {
        var row = Row();
        row.Add(Caption(caption));

        var bar = Bar(range, get, set);
        bar.style.flexGrow = 1;
        row.Add(bar);

        return row;
    }

    // A bar on its own, for a row this file did not build: on the transport a caption
    // column would only push everything beside it out of line.
    public static ValueBar Bar(in ValueBar.Range range, Func<float> get, Action<float> set)
    {
        var bar = new ValueBar(range);
        bar.Bind(get, set);
        return bar;
    }

    // Steppers and choosers

    // A labelled value with a minus and a plus. Reads its value back through the
    // getter every refresh, so nothing here has to be kept in step by hand.
    public static VisualElement Stepper(string caption, Func<float> get,
                                        Action<float> set, float step,
                                        string format = "0.###")
    {
        var row = Row();
        row.Add(Caption(caption));

        var value = Value("");
        value.style.unityTextAlign = TextAnchor.MiddleCenter;

        void Refresh() => value.text = get().ToString(format);

        row.Add(Push("-", () => { set(get() - step); Refresh(); }, 22));
        row.Add(value);
        row.Add(Push("+", () => { set(get() + step); Refresh(); }, 22));

        Refresh();
        return row;
    }

    // A labelled choice stepped through with arrows.
    public static VisualElement Chooser(string caption, IReadOnlyList<string> options,
                                        Func<int> get, Action<int> set)
    {
        var row = Row();
        row.Add(Caption(caption));

        var value = Value("");
        value.style.unityTextAlign = TextAnchor.MiddleCenter;

        void Refresh()
        {
            var index = Mathf.Clamp(get(), 0, options.Count - 1);
            value.text = options[index];
        }

        void Move(int delta)
        {
            var index = (get() + delta + options.Count) % options.Count;
            set(index);
            Refresh();
        }

        row.Add(Push("<", () => Move(-1), 22));
        row.Add(value);
        row.Add(Push(">", () => Move(1), 22));

        Refresh();
        return row;
    }

    // Panels

    // A floating panel. sequencer.md puts the details of a tile in a window of its
    // own, and this is that window: the cell shows the kind and the figure that
    // matters for reading the score, and everything else is set here.
    public static VisualElement Panel(string title, Action onClose)
    {
        var panel = new VisualElement();
        panel.style.position = Position.Absolute;
        panel.style.width = PanelWidth;
        panel.style.backgroundColor = Style.Panel;
        panel.style.paddingLeft = 10;
        panel.style.paddingRight = 10;
        panel.style.paddingTop = 8;
        panel.style.paddingBottom = 10;
        TileElement.SetBorderWidth(panel, 1.0f);
        TileElement.SetBorderColor(panel, Style.PanelLine);
        TileElement.SetBorderRadius(panel, 6.0f);

        var header = Row();
        header.style.marginBottom = 6;

        var caption = Text(title, 11.0f, Style.NoteText);
        caption.style.flexGrow = 1;
        caption.style.unityTextAlign = TextAnchor.MiddleLeft;
        header.Add(caption);

        if (onClose != null)
        {
            var close = Push("x", onClose, 20);
            close.style.marginRight = 0;
            header.Add(close);
        }

        panel.Add(header);
        return panel;
    }
}

} // namespace Jacquard.App
