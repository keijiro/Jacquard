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
    //
    // Which is why these two numbers are a property of the face as much as of the eye.
    // Every width below was cut to a word set at them, so a face that sets the same word
    // wider is a face that has to come down: the pair went to eight and nine and a half
    // under a wide one, and came back here under a narrow one. The ratio between them is
    // the thing to hold rather than the numbers, since Width below is that ratio.
    public static float FontSize => Touch ? 13.0f : MouseFontSize;

    // The caption column is as narrow as the longest parameter name will go, since a
    // name that wraps or clips is worse than a bar that is a few pixels shorter. The
    // name is "Reverb send".
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

    // Presses

    // Whether a press is one this element is already holding, which on a touch screen is
    // not a rhetorical question: a finger's drag arrives as two presses, and everything
    // here that begins a gesture on a press has to let the second one go by.
    //
    // Where the second one comes from, since nothing about it is this codebase's doing.
    // The Input System's UI click action is a pass-through bound to the touch's press,
    // and a touch's press is not a button — it is read off the phase, so the phase
    // turning from Began to Moved is a state change of the control with the value still
    // pressed. A pass-through action performs on every state change rather than on every
    // value change, and the InputForUI provider sends a fresh ButtonPressed for it: it
    // works out that the button was already down and then does not use the answer. UI
    // Toolkit turns that into a second PointerDownEvent and hands it to whoever holds the
    // pointer, which is the element in the middle of the drag.
    //
    // Measured on the drag it was found by, a synthetic finger stepped one phase at a
    // time: the second press arrives on the first movement, 1.7 pixels from the first and
    // on the same cell, one input update behind it. One extra press per drag and no more
    // — a phase that is already Moved does not change again, so the rest of a drag is
    // clean, and a flick fast enough to carry the second press onto another cell was never
    // read as a double click at all.
    //
    // A mouse cannot do this, since moving one does not touch the bit its button lives
    // in, which is why this looked like a mystery about iOS: every pointer on the machine
    // the UI is built on behaves, and every finger on the device does not.
    //
    // The capture is what answers the question. A press for a pointer this element
    // already holds is that press arriving twice and is never a new gesture; a second
    // finger carries an id of its own and is not caught by this.
    public static bool PressAlreadyHeld(VisualElement element, PointerDownEvent evt)
      => element.HasPointerCapture(evt.pointerId);

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
        React(button);
        // Every width here is written against the mouse profile, so a caller never has
        // to know which set is in force.
        if (width > 0.0f) button.style.width = Width(width);
        return button;
    }

    // Shows whether what a button toggles is currently on.
    public static void SetActive(Button button, bool active)
    {
        Ground(button, active ? Style.NoteLine : Style.ControlBackground);
        // Which also turns the word over to the weight a light ground takes.
        Style.SetInk(button, active);
    }

    // What a button is doing about the hand on it: the ground it goes back to when the
    // hand is elsewhere, and what the hand is currently doing to it.
    //
    // It has to be kept somewhere, because the three of them are set from three
    // different places — the ground by whoever toggles the switch, the other two by the
    // pointer — and the colour on screen is a function of all three. Hung on the button
    // rather than closed over, since SetActive is handed a button by callers that have
    // never heard of any of this.
    sealed class Reaction
    {
        public Color Ground = Style.ControlBackground;
        public bool Hover;
        public bool Press;
    }

    // Gives a button its reaction to the pointer, which every button here gets: a flat
    // box that does not move when it is touched reads as a picture of a button rather
    // than as one, and on a screen with no cursor a press that leaves no mark is a press
    // a hand cannot tell landed.
    //
    // Five events and not two, because a hand does not only arrive and leave. It presses
    // and releases without going anywhere, it slides off while still holding — where the
    // press is over as far as the button is concerned, and the stock Clickable will not
    // fire — and it slides back on again, which is a press once more and says so: the
    // capture is what knows that, and it is the same capture the click itself is decided
    // by, so the mark and the outcome cannot disagree. And a press can end without any
    // release reaching here at all, a window deactivated or a touch cancelled, which is
    // the one ending that would otherwise leave a button lit with nothing on it.
    //
    // The press and the release are taken on the way down, and that is not a detail.
    // Clickable answers a mouse through the compatibility MouseDownEvent rather than
    // through the pointer event, and it clears the way for that by stopping the pointer
    // event *immediately* the moment it sees the mouse's own id — so a PointerDownEvent
    // handler registered the ordinary way is never called by a mouse at all. It is
    // called by a finger, which is what made this look like it worked: every touch
    // profile lit up and every mouse did nothing. TrickleDown puts these ahead of the
    // manipulator instead of behind it, which is where a thing that only paints belongs
    // anyway — nothing here consumes the event or decides anything about the click.
    //
    // Two things about checking any of that, both learned the hard way. SendEvent with
    // the target set proves nothing: it runs the same propagation, but the wrong-phase
    // handler still fired under it because the events arrived in an order no real device
    // produces, and the whole fault lives in what a real mouse does. And the editor Game
    // view cannot drive a real cursor — it stops processing input the moment
    // Application.isFocused goes false, which is whenever the focused editor window is
    // anything else. What settles it is a development standalone player driven with
    // CGEvent, with the grounds read straight off a screen capture.
    static void React(Button button)
    {
        var reaction = new Reaction();
        button.userData = reaction;

        button.RegisterCallback<PointerEnterEvent>(e =>
        {
            reaction.Hover = true;
            reaction.Press = button.HasPointerCapture(e.pointerId);
            Paint(button, reaction);
        }, TrickleDown.TrickleDown);

        button.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            reaction.Hover = reaction.Press = false;
            Paint(button, reaction);
        }, TrickleDown.TrickleDown);

        button.RegisterCallback<PointerDownEvent>(_ =>
        {
            reaction.Press = true;
            Paint(button, reaction);
        }, TrickleDown.TrickleDown);

        button.RegisterCallback<PointerUpEvent>(_ =>
        {
            reaction.Press = false;
            Paint(button, reaction);
        }, TrickleDown.TrickleDown);

        // Not on the way down, because a capture event does not travel: it is delivered
        // to the element the capture is leaving and to nothing else, so there is nothing
        // ahead of it to be stopped by.
        button.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            reaction.Press = false;
            Paint(button, reaction);
        });
    }

    // The ground a button rests on, which is where a switch says whether it is lit. It
    // is remembered rather than written straight to the style, since the pointer may be
    // on the button while it is being set — a switch is toggled by the very press that is
    // moving it — and what is drawn then is this ground under a hand, not this ground.
    static void Ground(Button button, Color ground)
    {
        if (button.userData is not Reaction reaction)
        {
            button.style.backgroundColor = ground;
            return;
        }

        reaction.Ground = ground;
        Paint(button, reaction);
    }

    static void Paint(Button button, Reaction reaction)
      => button.style.backgroundColor =
           reaction.Press ? Style.UnderHand(reaction.Ground, Style.PressStep)
           : reaction.Hover ? Style.UnderHand(reaction.Ground, Style.HoverStep)
           : reaction.Ground;

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

        // This is the control the second press of a touch drag costs the most: it acts on
        // the press, so a held button whose finger slides a pixel would throw the effect
        // again under the hand that is already holding it. See PressAlreadyHeld.
        button.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0 || PressAlreadyHeld(button, e)) return;
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
        var row = Chooser(options, get, set);
        row.Insert(0, Caption(caption));
        return row;
    }

    // The same without one, for a chooser that is not standing in a column of
    // parameters. A caption is as wide as the longest name on a panel so that the rows
    // line up, and a chooser on the transport row has nothing above or below it to line
    // up with: the word would be width taken off the name being chosen, in the one place
    // on this screen where a name is the whole of what is being read.
    public static VisualElement Chooser(IReadOnlyList<string> options, Func<int> get,
                                        Action<int> set)
      => Chooser(options, get, set, out _);

    // The same, handing back the way to put the readout right again.
    //
    // A chooser reads its list when it is built and whenever it is stepped, which is
    // the whole of what a list written down in this project needs. The one on the
    // transport row is a folder on a disk: it changes while the app is not looking, and
    // what is showing then is a name that was there a moment ago. See
    // JacquardUI.RefreshSlots.
    public static VisualElement Chooser(IReadOnlyList<string> options, Func<int> get,
                                        Action<int> set, out Action sync)
    {
        var row = Row();

        var value = Value("");
        value.style.unityTextAlign = TextAnchor.MiddleCenter;

        // Empty is a real state for a list that is read off a disk, and not one for any
        // of the lists in here that are written down. Nothing to show and nothing to
        // step to, rather than an index into no options.
        void Refresh()
        {
            var index = Mathf.Clamp(get(), 0, options.Count - 1);
            value.text = options.Count > 0 ? options[index] : "";
        }

        void Move(int delta)
        {
            if (options.Count == 0) return;

            var index = (get() + delta + options.Count) % options.Count;
            set(index);
            Refresh();
        }

        row.Add(Arrow(left: true, () => Move(-1)));
        row.Add(value);
        row.Add(Arrow(left: false, () => Move(1)));

        sync = Refresh;

        Refresh();
        return row;
    }

    // One of the two arrows a chooser is stepped with, drawn rather than typeset.
    //
    // An angle bracket is punctuation borrowed to point, and it shows: it is a pair of
    // hairlines at the weight of the type, it sits where a glyph sits in its line box
    // rather than in the middle of the button, and it is the same mark the readout
    // between the two is set in. A filled triangle is the thing itself — solid, centred
    // on the box it is drawn in, and unmistakably a control rather than a character.
    //
    // The stepper keeps its minus and plus. Those are not directions and there is no
    // shape that says *one less* better than the word does; these two are nothing but a
    // direction, which is all a triangle is.
    public static Button Arrow(bool left, Action onClick)
    {
        var button = Push("", onClick, ArrowWidth);
        // The air a word needs inside the box is what a drawn mark does not: the mark is
        // centred on the box rather than laid out from its edges.
        button.style.paddingLeft = 0;
        button.style.paddingRight = 0;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.Center;
        button.Add(ArrowMark(left));
        return button;
    }

    // As wide as the one-glyph buttons it replaces, which is also what the stepper's
    // minus and plus are: a run of chrome should not change width because one mark in
    // it stopped being a letter.
    const float ArrowWidth = 22.0f;

    // The mark. It is a box of its own rather than something painted on the button,
    // because a Button draws its own text and everything else about it is a border and
    // a ground — the same reason the sharp beside a note is an element and not a glyph.
    static VisualElement ArrowMark(bool left)
    {
        // Sized from the type, so the pair grows with the touch profile the way every
        // word beside them does. Even, so that the tip lands on a boundary rather than
        // half way across a pixel: the base is a straight edge and the tip is a point,
        // and the point is what the eye is following.
        var height = 2.0f * Mathf.Round(FontSize * 0.35f);
        var width = Mathf.Round(height * 0.6f);

        var mark = new VisualElement();
        mark.style.width = width;
        mark.style.height = height;
        mark.style.flexShrink = 0;
        mark.pickingMode = PickingMode.Ignore;

        mark.generateVisualContent += context =>
        {
            var painter = context.painter2D;
            painter.fillColor = Style.NoteText;

            // The tip on the side it points to, the base square on the other.
            var tip = new Vector2(left ? 0.0f : width, height / 2);

            painter.BeginPath();
            painter.MoveTo(tip);
            painter.LineTo(new Vector2(left ? width : 0.0f, 0.0f));
            painter.LineTo(new Vector2(left ? width : 0.0f, height));
            painter.ClosePath();
            painter.Fill(FillRule.NonZero);
        };

        return mark;
    }

    // Panels

    // A floating panel. sequencer-spec.md puts the details of a tile in a window of its
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
