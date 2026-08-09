using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// The one the Device Simulator stands in for. Identical to UnityEngine.Application
// everywhere else, so this is not an editor-only spelling.
using DeviceApplication = UnityEngine.Device.Application;

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

// What the chrome is laid out for. Auto asks the platform; the other two are here
// because the difference cannot be felt on the machine the UI is built on — a Mac can
// show what a tablet will get, but only if it can be told to.
public enum PointerKind { Auto, Mouse, Touch }

static class Controls
{
    // Metrics
    //
    // Two sets of them, settled once before anything is built. What separates them is
    // not the screen but the pointer: a mouse lands on whatever it is over, and a
    // fingertip covers about nine millimetres of glass no matter what is underneath.
    // The panel is at a constant pixel size and doubled, so on a tablet one unit here
    // is one iOS point — which makes a twenty pixel row twenty points against a
    // guideline of forty-four, and a pair of stepper arrows twenty-two by twenty.
    //
    // Only the controls move. The cell pitch in Style is left where it is, because the
    // score at its current size is the one thing the tablet already got right: what is
    // too small there is the chrome, and the chrome is all this touches. The two will
    // separate for good once the plane can be pinched, since the score's size on
    // screen becomes something the hand holding it decides.
    //
    // The growth is spent on the targets and not on the space between them. Paddings,
    // margins and the rules between sections stay where they are: air around two
    // things already big enough to hit is air a ten row panel cannot afford, and the
    // column has to stand Sound under Tile and still reach the bottom of the shortest
    // screen this runs on.

    public static bool Touch { get; private set; }

    // Read as elements are built, and nothing goes back to correct one afterwards, so
    // this has to be called before the first control is made.
    //
    // Auto asks UnityEngine.Device rather than UnityEngine. The Device Simulator's
    // simulated Application, Screen and SystemInfo live in a namespace of their own,
    // and the plain ones are never simulated: asked the wrong one, a simulated iPhone
    // answers with the Mac the editor is running on, and laid the chrome out at a 32
    // pixel transport row and 192 pixel panels — the mouse set, on a phone. Outside
    // the editor the two are the same class, so a player pays nothing for the spelling.
    //
    // Which means Auto follows a simulated device and nothing else. It deliberately
    // does not read the build target: an editor building for iOS but showing a plain
    // Game View is not showing a phone, and dressing it in the touch metrics would
    // only produce a third thing that is neither — the tablet's controls at the Mac's
    // scale, the wrong size for both. Previewing touch is what the simulator is for
    // now that it resolves the right scale, and Touch above is what forces it without
    // one.
    public static void LayOutFor(PointerKind kind)
      => Touch = kind switch { PointerKind.Mouse => false,
                               PointerKind.Touch => true,
                               _ => DeviceApplication.isMobilePlatform };

    // A control's box, which is also the height of the row it sits on.
    public static float RowHeight => Touch ? 30.0f : 20.0f;

    // Text on the chrome. It grows by less than the box does, since eleven pixels was
    // legible on the tablet and only the target was not — and every caption column and
    // button width below is measured in words, so each of them pays for this.
    public static float FontSize => Touch ? 13.0f : MouseFontSize;

    // The caption column is as narrow as the longest parameter name will go, since a
    // name that wraps or clips is worse than a bar that is a few pixels shorter.
    public static float LabelWidth => Touch ? 88.0f : 74.0f;

    // Every panel is this wide.
    //
    // What is left after the caption column and the padding is the bar, and a bar
    // only has to be long enough to read a number off and to see roughly where in its
    // range that number sits. Neither wants the width a readout of "500 ms" would
    // have going spare, so the panel is cut back to where the widest row it carries —
    // the lane's Move buttons — still fits.
    public static float PanelWidth => Touch ? 248.0f : 192.0f;

    // The transport row: one row of controls with the same air over and under them.
    public static float ToolbarHeight => RowHeight + (Touch ? 16.0f : 12.0f);

    // The corner a control's box is cut to, a shade tighter than a cell's so that a
    // row of them does not read as more tiles.
    public static float Radius => Touch ? 5.0f : 4.0f;

    // The space a panel keeps under it, and the same distance the column of them is
    // held off the edges of the screen: the gap around the panels reads as one gap
    // whichever side of one it is on. A gap, so it is the same in both sets.
    public const float PanelGap = 12.0f;

    // A width given in the mouse profile's terms, stretched to hold the same words at
    // the other's larger type. Never narrower than a row is tall, which is what turns
    // the one-glyph buttons — the arrows either side of a figure, a panel's close —
    // from a slot into something square enough to hit.
    public static float Width(float width)
      => Touch ? Mathf.Max(Mathf.Round(width * FontSize / MouseFontSize), RowHeight)
         : width;

    const float MouseFontSize = 11.0f;

    public static Label Text(string text, float size, Color color)
      => TileElement.Text(text, size, color);

    public static Label Caption(string text)
    {
        var label = Text(text, FontSize, Style.Label);
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.width = LabelWidth;
        label.style.flexShrink = 0;
        return label;
    }

    public static Label Value(string text)
    {
        var label = Text(text, FontSize, Style.NoteText);
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

    // Buttons

    public static Button Push(string text, Action onClick, float width = 0.0f)
    {
        var button = new Button(onClick) { text = text };
        button.style.fontSize = FontSize;
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
        // Every width here is written against the mouse profile, so a caller never has
        // to know which set is in force.
        if (width > 0.0f) button.style.width = Width(width);
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
    //
    // Where it lands is not decided here. Panels stack down a column of their own, so
    // one lays itself out in the flow like anything else and carries the gap to the
    // panel under it; the column is what is pinned to a corner of the screen.
    public static VisualElement Panel(string title, Action onClose)
    {
        var panel = new VisualElement();
        panel.style.width = PanelWidth;
        panel.style.flexShrink = 0;
        panel.style.marginBottom = PanelGap;
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

        var caption = Text(title, FontSize, Style.NoteText);
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
