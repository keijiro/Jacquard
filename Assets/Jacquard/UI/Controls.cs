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

    // Spacing
    //
    // Three numbers and one rule. Gap is the space between any two things standing next
    // to each other in a panel — two rows, two buttons, a heading and what it heads.
    // Inset is the panel's own, from its edge to everything it holds. GroupGap is what
    // is added where one group of rows ends and the next thing begins.
    //
    // The rule is that a gap is carried underneath and to the right, by the thing
    // above and to the left of it, and that anything wanting more than a gap adds only
    // what is missing rather than a gap of its own. The panel's bottom inset is short
    // by a gap for the same reason — the last row already laid one down — so the inset
    // reads the same on all four sides.
    //
    // Nothing here is emphasis. A header is a line of the panel like any other, spaced
    // like any other, because which panel it is was never the question the eye is
    // asking; what it says is.
    //
    // None of these grows with the touch profile, for the reason the metrics above
    // give: what a fingertip needs is a bigger target and not more space between
    // targets, and the column has to stand Sound under Tile and still reach the bottom
    // of the shortest screen this runs on.
    public const float Gap = 3.0f;
    public const float Inset = 10.0f;

    // What parts one group of rows from the next. Twice a gap, which is as much as a
    // break can take before the panel reads as a stack of separate things — the
    // grouping is said by the heading and the rule under it, and this is only what
    // stops the last row of one group from sitting as close to the next heading as it
    // does to its own neighbours.
    public const float GroupGap = Gap * 2;

    // The space a panel keeps under it, and the same distance the column of them is
    // held off the edges of the screen: the gap around the panels reads as one gap
    // whichever side of one it is on. Wider than the inset, so that two panels read as
    // two things rather than as one with a line drawn across it.
    public const float PanelGap = 12.0f;

    // How long after a press a second one still reads as the same gesture. Both places
    // that ask — a bar being opened to be typed into, a cell being copied or a lane
    // started — count it themselves rather than take the event's own clickCount, so the
    // number has to be one number: two gestures on one screen that disagree about how
    // quick a double click is would be a hand that cannot learn either.
    public const long DoubleClickMilliseconds = 400;

    // A width given in the mouse profile's terms, stretched to hold the same words at
    // the other's larger type. Never narrower than a row is tall, which is what turns
    // the one-glyph buttons — the arrows either side of a figure, the plus and minus of
    // a stepper — from a slot into something square enough to hit.
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

    // A heading over a group of rows, for a panel that holds more than one group. It
    // is a caption in the same dim grey — a group is named, not announced — standing
    // where a row would and carrying the gap a row carries.
    //
    // It is also as tall as a row, which is what actually makes it sit like one. A
    // control is a twenty pixel box around thirteen pixels of text, so a bare line of
    // text between two of them is short by the air the boxes hold: measured between
    // the boxes every gap here is a gap, and measured between the words the line
    // would hug whatever is over it.
    //
    // The rule under it is the only rule left in a panel, and it is the heading's
    // rather than a divider standing between two groups. A line between them said only
    // that something ends here and something begins, which left the first row of a
    // group looking as much like the end of the one above as the start of its own; a
    // line under the name ties the name to what it names, and what parts one group from
    // the next is then the air above the heading rather than a second mark.
    //
    // follows is whether anything already stands above it in the panel, which is what
    // that air is for. The first heading in a panel has the header over it and needs
    // none: the header is already a line of its own with a gap under it.
    public static Label Heading(string text, bool follows = false)
    {
        var label = Caption(text);
        label.style.height = RowHeight;
        label.style.marginTop = follows ? GroupGap : 0.0f;
        label.style.marginBottom = Gap;

        // Across the panel and not across the word. A caption is as wide as the caption
        // column so that a column of them lines up with the controls beside them, and a
        // heading has no control beside it: left at that width the rule would stop a
        // third of the way over, under the name rather than over the rows.
        label.style.width = StyleKeyword.Auto;

        label.style.borderBottomWidth = 1.0f;
        label.style.borderBottomColor = Style.PanelLine;

        return label;
    }

    public static VisualElement Row()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.flexShrink = 0;
        row.style.marginBottom = Gap;
        return row;
    }

    // A row that stands at the foot of a panel, away from the rows above it.
    //
    // What ends up here is the one button the panel has and the panel's whole point is
    // not — Audition, Delete — so what it wants is to be told apart from the list it
    // follows rather than to be the end of it. It used to be told apart by a rule,
    // which is now the heading's mark and would say the wrong thing here: there is
    // nothing under this to head.
    public static VisualElement Foot()
    {
        var row = Row();
        row.style.marginTop = GroupGap;
        return row;
    }

    // Buttons

    public static Button Push(string text, Action onClick, float width = 0.0f)
    {
        var button = new Button(onClick) { text = text };
        button.style.fontSize = FontSize;
        button.style.height = RowHeight;
        button.style.minWidth = 0;
        button.style.marginLeft = 0;
        button.style.marginRight = Gap;
        button.style.marginTop = 0;
        button.style.marginBottom = 0;
        // Air inside the box rather than around it, so a word is not up against the
        // border. Twice the gap, since this is the one place two edges meet.
        button.style.paddingLeft = Gap * 2;
        button.style.paddingRight = Gap * 2;
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

    // A button that is on while it is held and off the moment it is let go, which is
    // the whole of what a live effect is: there is no state to leave behind, so
    // there is nothing to press a second time to undo.
    //
    // The stock Clickable is taken off rather than worked around. It reports on the
    // release and captures the pointer to decide whether the release counts, so a
    // press and a release read through it would arrive together at the end and the
    // effect would never be on for any length of time. What is left is a box dressed
    // as a button with the two events read directly.
    //
    // The capture is what makes a hand sliding off the button still end the effect,
    // and it is per pointer, so two fingers on two of these are two independent
    // holds rather than one that steals the other's release.
    //
    // onUp runs off the lost capture rather than off the release, which is also where
    // a caller can hand the keyboard back to the plane: the focus controller settles a
    // press after this element has seen it, so a Focus from the down handler would
    // simply be undone.
    public static Button Hold(string text, Action onDown, Action onUp,
                              float width = 0.0f)
    {
        var button = Push(text, null, width);
        button.clickable = null;

        button.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0) return;
            button.CapturePointer(e.pointerId);
            SetActive(button, true);
            onDown?.Invoke();
            e.StopPropagation();
        });

        // Both endings, because a capture can be lost without a release ever reaching
        // here — a window deactivated mid-press, a touch cancelled — and an effect
        // latched on is the one failure this control cannot have.
        button.RegisterCallback<PointerUpEvent>(e =>
        {
            if (!button.HasPointerCapture(e.pointerId)) return;
            button.ReleasePointer(e.pointerId);
            e.StopPropagation();
        });

        button.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            SetActive(button, false);
            onUp?.Invoke();
        });

        return button;
    }

    // One switch of a run that fills the panel's width, sized so that perRow of them
    // fit across it and square so that a run of them reads as a row of slots rather
    // than as a row of buttons.
    //
    // Nothing is written on it. What a switch stands for is its position in the run —
    // the third box is the third lap — so a caption on each would be a number
    // repeated as many times as there are switches, in the one place there is no room
    // for it. The run is what carries the meaning, and the panel captions the run.
    public static Button Switch(int perRow, Action onClick)
    {
        var size = SwitchSize(perRow);

        var button = Push("", onClick);
        button.style.width = size;
        button.style.height = size;
        // The padding a word needs is what a blank box does not: it is what would
        // stop the box being square.
        button.style.paddingLeft = 0;
        button.style.paddingRight = 0;
        button.style.marginBottom = Gap;
        return button;
    }

    // How big one of them comes out, for a run that has to place a switch itself
    // rather than let a row lay it out. It is a metric of the profile in force, so a
    // caller cannot write the number down; the scale keyboard needs it to put the
    // black keys in the gaps between the white ones.
    public static float SwitchSize(int perRow)
      => Mathf.Floor((PanelWidth - Inset * 2 + Gap) / perRow) - Gap;

    // Value bars

    // A labelled number, set on the bar that shows it. The bar takes whatever the
    // caption leaves of the row: the readout is printed on it, so unlike a stepper
    // there is nothing to put beside it.
    //
    // The value is passed as a getter and a setter rather than as a number, because
    // the same parameter is also changed from the grid and by a load, and a bar that
    // only wrote would go stale and then write its stale value back.
    //
    // settled is the optional second half of that: the same value once it has stopped
    // moving, for the row that has to sound a note about it. See ValueBar.Bind.
    public static VisualElement Bar(string caption, in ValueBar.Range range,
                                    Func<float> get, Action<float> set,
                                    Action settled = null)
    {
        var row = Row();
        row.Add(Caption(caption));

        var bar = Bar(range, get, set, settled);
        bar.style.flexGrow = 1;
        row.Add(bar);

        return row;
    }

    // A bar on its own, for a row this file did not build: on the transport a caption
    // column would only push everything beside it out of line.
    public static ValueBar Bar(in ValueBar.Range range, Func<float> get,
                               Action<float> set, Action settled = null)
    {
        var bar = new ValueBar(range);
        bar.Bind(get, set, settled);
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
    //
    // Nothing on the header but the header. A panel is put away by whatever put it up —
    // the cursor for three of them, the transport row's button for the fourth — so a
    // close of its own would be a second switch for something that already has one, and
    // a control where the eye goes for a title.
    public static VisualElement Panel(string title) => Panel(title, out _);

    // The same, with the title handed back to be rewritten.
    //
    // A panel here names what it is currently showing rather than what kind of panel
    // it is, and what it is showing follows the cursor: the header is "Note Tile" and
    // then "Cycle Gate Tile", not "Tile" with a second line under it repeating the
    // part that changed. That line was a row of chrome per panel saying what the
    // header had room for, in a column that has to reach the bottom of the shortest
    // screen this runs on.
    public static VisualElement Panel(string title, out Label header)
    {
        var panel = new VisualElement();
        panel.style.width = PanelWidth;
        panel.style.flexShrink = 0;
        panel.style.marginBottom = PanelGap;
        panel.style.backgroundColor = Style.Panel;
        panel.style.paddingLeft = Inset;
        panel.style.paddingRight = Inset;
        panel.style.paddingTop = Inset;
        // Short by a gap, which the last row on the panel has already put there.
        panel.style.paddingBottom = Inset - Gap;

        // No outline and no rounded corners. What tells a panel from the plane is that
        // it is a lighter ground with air around it, which is enough on a screen where
        // nothing else is a filled rectangle that size; the border was a second thing
        // saying it, drawn in the same grey the controls inside are outlined in, so a
        // panel read as one more control with controls in it. Square, because a corner
        // radius is what a cell and a control have — they are the things a hand picks up
        // — and the sheet they are laid on should not look pickable.

        // Spaced like any other row, and as tall as one. What the header says changes;
        // that it is a header is not worth a band of air to announce, and it needs no
        // mark under it either — it is the one line on the panel in the bright text
        // every caption below it is not.
        var row = Row();
        row.style.height = RowHeight;

        header = Text(title, FontSize, Style.NoteText);
        header.style.flexGrow = 1;
        header.style.unityTextAlign = TextAnchor.MiddleLeft;
        row.Add(header);

        panel.Add(row);
        return panel;
    }

    // Takes a panel out of reach without taking it off the screen.
    //
    // A shield rather than a flag on every control: a panel holds a dozen bars and
    // buttons that each decide for themselves what a press means, and one stretched
    // element in front of them is picked instead of any of them, so none of them ever
    // sees a press to decide about. Dimmed by the rule the rest of this UI dims by, and
    // not disabled in the layout engine's sense — see Style.DimmedOpacity.
    //
    // It goes on the panel and not on its body, so that a body built again underneath
    // it — which is what a panel does whenever the cursor lands on another kind of tile
    // — comes back exactly as far out of reach as it went.
    public static void SetLocked(VisualElement panel, bool locked)
    {
        panel.style.opacity = locked ? Style.DimmedOpacity : 1.0f;

        var shield = panel.Q(ShieldName);

        if (!locked)
        {
            shield?.RemoveFromHierarchy();
            return;
        }

        if (shield != null) return;

        shield = new VisualElement
          { name = ShieldName, pickingMode = PickingMode.Position };
        shield.StretchToParentSize();
        panel.Add(shield);
    }

    const string ShieldName = "lock-shield";
}

} // namespace Jacquard.App
