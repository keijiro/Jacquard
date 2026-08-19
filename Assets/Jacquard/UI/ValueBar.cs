using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// A number, edited on the bar that shows it.
//
// The value reads out over a bar that fills as it rises, dragging scrubs it, and a
// double click hands over to a text field for typing an exact one. That shape is a
// DAW's parameter box, and it is here because a pair of arrows either side of a
// figure says what a parameter is but nothing about where it sits inside what it
// could be — which is the one thing you need while dialling in a sound.
//
// A plain VisualElement rather than a BaseField<float>. A field brings a label, a
// class hierarchy and the default theme's dressing along with it, none of which
// belongs in a UI that draws its own flat monochrome controls; implementing
// INotifyValueChanged<float> is all it takes for RegisterValueChangedCallback and
// SetValueWithoutNotify to work the way the rest of the UI expects.
//
// The bar is only the box. The caption beside it, and how wide it ends up, belong
// to the row it goes in, which Controls builds like every other row here.
//
// Dragging is bounded by the range the bar was given, but typing is not: a range
// only says where a parameter is useful, and a prototype has to be able to throw a
// value from well outside one at the synth.

sealed class ValueBar : VisualElement, INotifyValueChanged<float>
{
    // Value mapping

    // How a number relates to its bar, and how it reads out.
    //
    // Curve is an exponent on the bar position, so a parameter whose interesting
    // values are all bunched up at the bottom of its range can still be dialled in:
    // a modulator ratio spends a third of its travel below unity rather than an
    // eighth of it. Scale and Unit are display only, which is how a value held in
    // seconds reads out in milliseconds.
    //
    // Floor makes the bar geometric instead of curved, which is what a range spanning
    // decades needs and what no exponent can give it. An exponent has no slope at all
    // at the bottom — pow(p, 3) leaves the first tenth of an envelope time's travel
    // inside the first millisecond, which is under what the readout shows and under
    // what the ear can hear, and reads as a bar with a dead end. A geometric bar
    // multiplies where a curved one adds, so a step along it is the same *ratio*
    // wherever it is taken: about a twentieth of the value per pixel, from one end to
    // the other. Floor is where that run starts, since no number of ratios reaches
    // zero from anywhere — and a Low beneath it is the one value the very bottom of the
    // travel holds, which is how a decay that switches its envelope off keeps a place
    // on a bar that could otherwise never land on it.
    public readonly struct Range
    {
        public readonly float Low, High;
        public readonly float Curve;
        public readonly float Snap;  // Quantum a drag lands on, 0 to scrub freely
        public readonly float Scale; // Value to readout multiplier
        public readonly string Unit;
        public readonly int Digits;
        public readonly float Floor; // Where a geometric run starts, 0 to use Curve

        // Replaces the numeric readout entirely, for a value that is better read as
        // something else: a note number reads "60 C4". Typing still goes through the
        // number.
        public readonly Func<float, string> Display;

        public Range(float low, float high, float curve = 1.0f, float snap = 0.0f,
                     float scale = 1.0f, string unit = null, int digits = 2,
                     Func<float, string> display = null, float floor = 0.0f)
          => (Low, High, Curve, Snap, Scale, Unit, Digits, Display, Floor) =
             (low, high, curve, snap, scale, unit, digits, display, floor);

        public bool Geometric => Floor > 0.0f;

        // Where the ratios start counting from: the floor, unless the parameter's own
        // range begins above it and there is nothing below to reach.
        float Root => Low > Floor ? Low : Floor;

        // A range that straddles zero is drawn from where zero sits rather than from
        // the left edge, so the sign of the value is visible at a glance.
        public bool Bipolar => Low < 0.0f && High > 0.0f;

        // Bar position [0,1] to value.
        public float ToValue(float position)
        {
            if (Geometric)
            {
                // The bottom of the travel is Low itself, which is the only way an
                // envelope that can be switched off is reachable by a drag: everything
                // above it is a ratio away from the floor.
                if (!Bipolar)
                    return position <= 0.0f ? Low
                           : Root * Mathf.Pow(High / Root, position);

                // A shift, which reaches the same distance either way and holds no
                // shift at all in the middle. Its smallest step off centre is the floor
                // rather than nothing, since a shift of less than a millisecond is not
                // a shift.
                var offset = position * 2.0f - 1.0f;
                var reach = offset < 0.0f ? -Low : High;

                return offset == 0.0f ? 0.0f
                       : Mathf.Sign(offset) * Floor *
                         Mathf.Pow(reach / Floor, Mathf.Abs(offset));
            }

            if (!Bipolar)
                return Low + (High - Low) * Mathf.Pow(position, Curve);

            var signed = position * 2.0f - 1.0f;
            var depth = Mathf.Pow(Mathf.Abs(signed), Curve);
            return (signed < 0.0f ? Low : High) * depth;
        }

        // Value to bar position, clamped to the ends: a typed value from outside the
        // range simply fills or empties the bar.
        public float ToPosition(float value)
        {
            if (Geometric)
            {
                // Anything at or under the floor sits at the bottom of the travel,
                // which is where a Low beneath it lands as well: the two share the end
                // pixel rather than one of them being unreachable.
                if (!Bipolar)
                    return value <= Root ? 0.0f
                           : Mathf.Clamp01(Mathf.Log(value / Root) /
                                           Mathf.Log(High / Root));

                var size = Mathf.Abs(value);
                var reach = value < 0.0f ? -Low : High;
                var away = size <= Floor ? 0.0f
                           : Mathf.Clamp01(Mathf.Log(size / Floor) /
                                           Mathf.Log(reach / Floor));

                return (Mathf.Sign(value) * away + 1.0f) * 0.5f;
            }

            if (!Bipolar)
                return High == Low ? 0.0f
                  : Mathf.Pow(Mathf.Clamp01((value - Low) / (High - Low)), 1.0f / Curve);

            var depth = Mathf.Clamp01(value / (value < 0.0f ? Low : High));
            var signed = Mathf.Sign(value) * Mathf.Pow(depth, 1.0f / Curve);
            return (signed + 1.0f) * 0.5f;
        }

        public float Round(float value)
          => Snap > 0.0f ? Mathf.Round(value / Snap) * Snap : value;

        public string ToText(float value)
          => Display != null ? Display(value)
             : Unit == null ? ToNumber(value) : ToNumber(value) + " " + Unit;

        // The readout as it is typed: the same number without its unit, so an edit
        // starts from what was on screen.
        public string ToNumber(float value)
        {
            var shown = value * Scale;
            return shown.ToString("F" + Places(shown), CultureInfo.InvariantCulture);
        }

        // How many decimals to print, which on a geometric bar is a property of the
        // number rather than of the parameter: three figures wherever the value stands,
        // so the digits move whenever it does.
        //
        // A fixed count cannot manage that on a bar whose steps are ratios. It matches
        // the travel at one point along it and is wrong on both sides — too coarse below,
        // where a whole millisecond of readout hides a run of pixels that each moved the
        // value by a twentieth, and too fine above, where one pixel steps the last digit
        // by ninety. Three figures track the ratio the same way the bar does.
        //
        // Zero prints bare rather than as a row of decimals, because it is the one value
        // on such a bar that is a setting instead of a quantity: an envelope switched
        // off should say so and not 0.00.
        int Places(float shown)
        {
            if (!Geometric) return Digits;

            var size = Mathf.Abs(shown);
            return size <= 0.0f ? 0 : size < 10.0f ? 2 : size < 100.0f ? 1 : 0;
        }

        public bool TryParse(string text, out float value)
        {
            value = 0.0f;

            // Invariant, to match the readout. A box that prints 0.50 and then
            // refuses to take it back would be worse than one that ignores the
            // machine's locale.
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
                                out var typed)) return false;

            value = typed / Scale;
            return true;
        }
    }

    // Range presets, one per shape of parameter this UI has.

    // A time in seconds, read out in milliseconds, over a geometric bar: an envelope
    // time runs from a millisecond to seconds, which is three decades and the one shape
    // of range an exponent cannot serve at both ends. A millisecond is the floor
    // because it is the shortest time this synth has any use for — under it an attack
    // is a click and a decay is a switch, and neither becomes anything else by being
    // halved again. Where a parameter's own low end is zero, that end of the travel
    // keeps it, since there the envelope is off rather than brief.
    public static Range Seconds(float low, float high)
      => new Range(low, high, scale: 1000.0f, unit: "ms", floor: 0.001f);

    // A bare amount, whatever it happens to mean. Ends that straddle zero need nothing
    // said about them: the bar reads out from where zero sits on its own.
    public static Range Amount(float low, float high, float snap = 0.0f)
      => new Range(low, high, snap: snap);

    // A whole number, optionally read out as something other than its digits.
    public static Range Integer(float low, float high, Func<float, string> display = null)
      => new Range(low, high, snap: 1.0f, digits: 0, display: display);

    // Construction

    public ValueBar(in Range range)
    {
        _range = range;

        style.height = Controls.RowHeight;
        style.flexShrink = 0;
        style.backgroundColor = Style.ControlBackground;
        TileElement.SetBorderWidth(this, 1.0f);
        TileElement.SetBorderColor(this, Style.PanelLine);
        TileElement.SetBorderRadius(this, Controls.Radius);

        // So the fill stays inside the rounded corners instead of squaring them off
        // again.
        style.overflow = Overflow.Hidden;

        // Everything in the box is stacked in the same place: the fill and the
        // readout are one control, not a row of two.
        _fill = new VisualElement();
        _fill.style.position = Position.Absolute;
        _fill.style.top = 0.0f;
        _fill.style.bottom = 0.0f;
        _fill.style.backgroundColor = Style.Fill;
        _fill.pickingMode = PickingMode.Ignore;
        Add(_fill);

        _readout = TileElement.Text("", Controls.FontSize, Style.NoteText);
        Overlay(_readout);
        Add(_readout);

        _input = BuildInput();
        Add(_input);

        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerUpEvent>(OnPointerUp);

        // Losing the capture, which a click outside the panel does, has to end the
        // drag as well, or the next pointer move picks it back up.
        RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());

        RegisterCallback<PointerEnterEvent>(_ => SetHover(true));
        RegisterCallback<PointerLeaveEvent>(_ => SetHover(false));

        UpdateBar();
    }

    // The editor, kept around hidden rather than built on demand: it is one element,
    // and creating it on the double click would mean focusing an element that has
    // never been laid out.
    TextField BuildInput()
    {
        var input = new TextField();
        Overlay(input);
        input.style.display = DisplayStyle.None;
        input.style.fontSize = Controls.FontSize;

        // The theme dresses the inner input element for a taller field: a border, a
        // background of its own and enough padding to push the text out of the box.
        var inner = input.Q(TextField.textInputUssName);
        TileElement.SetMargin(inner, 0.0f);
        TileElement.SetPadding(inner, 0.0f);
        TileElement.SetBorderWidth(inner, 0.0f);
        inner.style.backgroundColor = Color.clear;

        // Alignment has to be set on the element the text actually lives in. The
        // theme aligns that one itself, and a rule of its own beats anything
        // inherited from the field above it, so the number would sit left of where
        // the readout it replaces was.
        foreach (var element in inner.Query<TextElement>().Build())
            element.style.unityTextAlign = TextAnchor.MiddleCenter;

        // The theme's minimum height is taller than this box, and a minimum wins over
        // the stretch that positioning it over the box asks for, which would leave
        // the text sitting low.
        input.style.minHeight = 0.0f;
        inner.style.minHeight = 0.0f;

        // Enter commits and Escape abandons. Taken on the way down, so both are seen
        // before the text engine gets to treat them as something to type.
        input.RegisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);

        // Clicking away commits, which is also what catches the case of the key
        // handler above missing an Enter. The keyboard goes to whatever was clicked
        // rather than back to the grid, since that click asked for it.
        input.RegisterCallback<FocusOutEvent>(_ => EndEdit(true, false));

        return input;
    }

    // Binding

    // Points the bar at a value in the model. A getter as well as a setter, because
    // the model is also changed from the grid and by a load: a bar that only wrote
    // would go stale and then put its stale value back on the next drag.
    //
    // settled is for whatever has to happen once the number has stopped moving rather
    // than at every number on the way there — sounding a note is the case it exists
    // for. A scrub reports it when the hand comes off, and reports it once: a drag
    // down a bar crosses a hundred values, and a note per value is a machine gun
    // rather than an audition. Anything that is not a drag settles the instant it
    // changes, since a typed number arrives already decided.
    public void Bind(Func<float> get, Action<float> set, Action settled = null)
    {
        _get = get;
        _settled = settled;

        SetValueWithoutNotify(get());

        this.RegisterValueChangedCallback(e =>
        {
            set(e.newValue);

            if (_dragging) _scrubbed = true; else _settled?.Invoke();
        });
    }

    // Pulls the bar back in line with the model.
    public void Sync()
    {
        if (_get == null) return;

        var current = _get();
        if (_value != current) SetValueWithoutNotify(current);
    }

    // Every bar under an element. The panels rebuild their bodies as the cursor
    // moves, so a list of the bars they made would only be a second thing to keep in
    // step; the tree already knows what is on screen.
    public static void SyncAll(VisualElement root)
    {
        foreach (var bar in root.Query<ValueBar>().Build()) bar.Sync();
    }

    // Value

    public float value
    {
        get => _value;
        set
        {
            var previous = _value;
            if (previous == value) return;

            SetValueWithoutNotify(value);

            using var change = ChangeEvent<float>.GetPooled(previous, _value);
            change.target = this;
            SendEvent(change);
        }
    }

    // Snapping happens where a value is entered, not here: this is also how the
    // model pushes its own value back in, and rounding that would fight anything
    // that set the parameter from outside this control.
    public void SetValueWithoutNotify(float newValue)
    {
        _value = newValue;
        UpdateBar();
    }

    // Interaction

    void OnPointerDown(PointerDownEvent e)
    {
        if (e.button != 0 || _editing) return;

        RememberKeyboard();

        // Double click detection of its own, rather than the event's clickCount:
        // only a second click that follows one which did not scrub counts, so a
        // quick pair of drags cannot open the editor by accident.
        if (e.timestamp - _clickTime < DoubleClickMilliseconds)
        {
            _clickTime = 0;
            BeginEdit();
            e.StopPropagation();
            return;
        }

        _dragging = true;
        _dragged = false;
        Anchor(e.position, e.shiftKey);
        (_dragFrame, _dragSteady) = (Time.frameCount, _value);

        this.CapturePointer(e.pointerId);
        _fill.style.backgroundColor = Style.FillActive;

        e.StopPropagation();
    }

    void OnPointerMove(PointerMoveEvent e)
    {
        if (!_dragging) return;

        // Holding shift part way through a drag re-anchors it rather than jumping the
        // value: the coarse travel so far stays where it landed and the fine travel
        // carries on from there.
        if (e.shiftKey != _dragFine) Anchor(e.position, e.shiftKey);

        // Right is more, along the bar, and up is more, as it is on a fader. Both
        // axes count, so the value follows the hand whichever way it moves and a
        // diagonal drag is simply the sum of the two.
        //
        // Measured from where the drag was anchored rather than accumulated per
        // event, so a coarse pointer cannot drift.
        var offset = (Vector2)e.position - _dragOrigin;
        var travel = (offset.x - offset.y) / DragDistance;
        if (_dragFine) travel *= FineDragScale;

        if (offset.sqrMagnitude > DragThreshold * DragThreshold) _dragged = true;

        // Where the value stood at the end of the last frame the pointer was seen in.
        // Kept at every frame boundary and not per event, since a frame can carry more
        // than one move and it is the whole frame that is given back below.
        if (_dragFrame != Time.frameCount)
          (_dragFrame, _dragSteady) = (Time.frameCount, _value);

        value = _range.Round(_range.ToValue(Mathf.Clamp01(_dragPosition + travel)));

        e.StopPropagation();
    }

    // Fixes the point the travel is measured from, and the bar position it counts
    // from, at the value the control is on now.
    void Anchor(Vector2 position, bool fine)
    {
        _dragFine = fine;
        _dragOrigin = position;
        _dragPosition = Mathf.Clamp01(_range.ToPosition(_value));
    }

    // Gives back whatever the pointer did in the frame it was lifted in, which is not
    // something any hand asked for.
    //
    // Measured on the phone rather than guessed at: nine drags, and every one of them
    // ended with one move arriving in the release's own frame — zero or one millisecond
    // before it, never a frame earlier — that carried the pointer a pixel and a half
    // further, downwards every single time. It is the finger leaving the glass: the
    // contact patch collapses and its centre slides off. Three of the nine had been
    // held perfectly still for three to five hundred milliseconds first and drifted just
    // the same, so it is the lift and not a hand still travelling; one of them moved the
    // value by nine tenths of a percent on a vertical drift alone, having not moved
    // sideways at all.
    //
    // A trackpad does it too, which is why nothing here asks what kind of pointer it
    // was. A finger and a fingertip on glass are the same hand leaving the same way.
    //
    // What it costs is one frame of a drag released while still moving, which at sixty
    // frames a second is a few pixels of a gesture that was never aiming at a particular
    // number. What it buys is that a drag ending where it was pointed stays there: this
    // bar reads both axes and a downward slide is worth as much as a sideways one, so a
    // drag to the left lost a fiftieth of the range to the lift — always downwards,
    // always the same direction, never given back.
    //
    // The value is written through rather than only shown, since the model has already
    // been handed the drifted number and the settle that follows is what auditions it.
    void OnPointerUp(PointerUpEvent e)
    {
        if (!_dragging) return;

        if (_dragFrame == Time.frameCount) value = _dragSteady;

        this.ReleasePointer(e.pointerId);
        EndDrag();

        // A click that scrubbed nothing is a candidate first half of a double click;
        // one that moved the value is not.
        _clickTime = _dragged ? 0 : e.timestamp;

        e.StopPropagation();
    }

    void EndDrag()
    {
        if (!_dragging) return;

        _dragging = false;
        _fill.style.backgroundColor = Style.Fill;

        // The one report a scrub makes, now that the value it has arrived at is the
        // value it meant. A drag that never moved the number has nothing to report,
        // which is also what keeps a plain click on a bar silent.
        if (_scrubbed)
        {
            _scrubbed = false;
            _settled?.Invoke();
        }

        ReturnKeyboard();
    }

    void OnInputKeyDown(KeyDownEvent e)
    {
        if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            EndEdit(true, true);
        else if (e.keyCode == KeyCode.Escape)
            EndEdit(false, true);
        else
            return;

        e.StopPropagation();
    }

    void BeginEdit()
    {
        _editing = true;

        // Before the field is shown, so there is no frame of whatever was typed into
        // it last.
        _input.SetValueWithoutNotify(_range.ToNumber(_value));
        _input.style.display = DisplayStyle.Flex;
        _readout.style.display = DisplayStyle.None;

        // The bar goes away for the duration and the box turns over to dark on light,
        // the same way a lit button does. It would otherwise be a selection highlight
        // over a bar over a dark box, with the value somewhere underneath all of it;
        // this way an open editor is unmistakable and the number in it is the
        // clearest thing on the row. The bar would be wrong anyway while it is being
        // typed over.
        _fill.style.display = DisplayStyle.None;
        Style.SetInk(_input, true);
        UpdateBackground();

        // Focus has to wait for the element to be laid out, which it has not been
        // while it was hidden. Checked again on the way through, so an edit that is
        // over by then does not leave the keyboard on a hidden field.
        //
        // The value is put in again here as well as above: it is what the edit starts
        // from, so it has to be there when the field takes the keyboard, and
        // selected, so that typing replaces it while a click or an arrow key keeps it
        // to edit.
        _input.schedule.Execute(() =>
        {
            if (!_editing) return;

            _input.SetValueWithoutNotify(_range.ToNumber(_value));
            _input.Focus();
            _input.SelectAll();
            SelectAllOnTouchKeyboard();
        });
    }

    // The same selection over again, on the keyboard a touch screen puts up.
    //
    // On such a screen that keyboard holds the text and this field only shows what it
    // holds: UI Toolkit copies the keyboard into the field every frame and writes the
    // selection back the other way only at the moment the keyboard opens — and what it
    // writes there is the caret parked at the end of the number. So the SelectAll above
    // is invisible exactly where it was wanted, and a double tap that means replace this
    // arrives as add to this.
    //
    // Nothing has to be arranged for the timing. iOS applies the range to its own field
    // at once and remembers it, and applies it again as the keyboard animates in, so a
    // selection set the instant the keyboard opens survives the keyboard arriving. Which
    // is why this can stand here, in the same breath as the focus that opened it.
    //
    // The length is the keyboard's own text and not ours: the setter throws if the range
    // reaches past what it holds, and the two are the same string only for as long as
    // nobody has typed.
    //
    // There is no keyboard at all wherever a pointer does the editing, which is every
    // platform this runs on but one — and an iPad with a keyboard attached, where the
    // field is edited in place and the SelectAll above is the whole of it.
    void SelectAllOnTouchKeyboard()
    {
        var keyboard = _input.textEdition.touchScreenKeyboard;
        if (keyboard == null || !keyboard.canSetSelection) return;

        keyboard.selection = new RangeInt(0, keyboard.text?.Length ?? 0);
    }

    void EndEdit(bool commit, bool returnKeyboard)
    {
        if (!_editing) return;

        // Cleared first, because blurring the field sends the focus out that also
        // arrives here.
        _editing = false;

        _input.style.display = DisplayStyle.None;
        _readout.style.display = DisplayStyle.Flex;
        _fill.style.display = DisplayStyle.Flex;
        UpdateBackground();
        _input.Blur();

        // A typed value is deliberately not clamped to the range, only snapped the
        // way a drag would be, so extremes can still be tried out.
        if (commit && _range.TryParse(_input.value, out var typed))
            value = _range.Round(typed);

        if (returnKeyboard) ReturnKeyboard();
    }

    // Keyboard

    // Whichever element had the keyboard when a drag or an edit began, so that it can
    // have it back afterwards. Here that is the score plane, where the letter keys
    // write notes: tweaking a parameter must not quietly be the end of typing on the
    // grid.
    //
    // The last one seen is kept rather than replaced by whatever is focused now: a
    // click on a bar may well leave nothing focused at all, and the click before the
    // one that opened an editor is exactly such a click. An editor of a bar's own is
    // never remembered, since it is only ever on its way out.
    void RememberKeyboard()
    {
        var focused = focusController?.focusedElement as VisualElement;
        if (focused == null || focused.GetFirstAncestorOfType<ValueBar>() != null) return;

        _keyboard = focused;
    }

    void ReturnKeyboard() => _keyboard?.Focus();

    // Appearance

    void UpdateBar()
    {
        var position = Mathf.Clamp01(_range.ToPosition(_value));

        // Zero is where a bipolar bar grows from, and it only sits in the middle when
        // the two ends are symmetric.
        var origin = _range.Bipolar ? Mathf.Clamp01(_range.ToPosition(0.0f)) : 0.0f;

        _fill.style.left = Length.Percent(Mathf.Min(position, origin) * 100.0f);
        _fill.style.width = Length.Percent(Mathf.Abs(position - origin) * 100.0f);

        _readout.text = _range.ToText(_value);
    }

    void SetHover(bool on)
    {
        _hover = on;
        UpdateBackground();
    }

    // The box lifts under the pointer, the way a cell does in the mockup, and turns
    // light while it is being typed into.
    void UpdateBackground()
      => style.backgroundColor = _editing ? Style.NoteLine
         : _hover ? Style.ControlHover : Style.ControlBackground;

    // Stacks an element over the whole box.
    static void Overlay(VisualElement element)
    {
        TileElement.SetMargin(element, 0.0f);
        TileElement.SetPadding(element, 0.0f);
        element.style.position = Position.Absolute;
        (element.style.left, element.style.right,
         element.style.top, element.style.bottom) = (0.0f, 0.0f, 0.0f, 0.0f);
    }

    // Metrics

    // Pointer travel, in pixels, that covers the whole range. Independent of how wide
    // the bar happens to be: what it buys is a consistent feel from one panel to the
    // next, not a pointer that stays under the fill.
    const float DragDistance = 160.0f;

    const float FineDragScale = 0.2f;

    // Enough slack that a click with a shaky hand still counts as a click.
    const float DragThreshold = 2.0f;

    const long DoubleClickMilliseconds = Controls.DoubleClickMilliseconds;

    // Private members

    readonly Range _range;
    readonly VisualElement _fill;
    readonly Label _readout;
    readonly TextField _input;

    Func<float> _get;
    Action _settled;

    float _value;

    bool _hover;

    bool _dragging;
    bool _dragged;  // Whether this drag has moved far enough to count as one
    bool _scrubbed; // Whether it has moved the value, which is what is reported
    bool _dragFine;
    Vector2 _dragOrigin; // Pointer position the drag is measured from
    float _dragPosition; // Bar position it counts from

    // The value as it stood before anything this frame moved it, and the frame that
    // was. See OnPointerUp: what the pointer does in the frame it is lifted in is not
    // something the hand asked for.
    int _dragFrame;
    float _dragSteady;

    bool _editing;
    long _clickTime; // Timestamp of the last click that did not scrub

    Focusable _keyboard;
}

} // namespace Jacquard.App
