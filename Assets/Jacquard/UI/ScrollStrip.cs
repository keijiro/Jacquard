using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// A strip of chrome that slides along one axis when it holds more than it can show.
//
// Two of them, and the same problem twice. The transport row carries a switch for each
// thing no cell can ask for, and each is as wide as the word written on it; a column of
// panels carries everything the cursor is standing on, and a channel start names a lane
// and a sound as well as itself. Neither list can be shortened to fit a screen — a
// switch off the edge is a switch that cannot be pressed, and a parameter under the
// bottom of the screen is a parameter that cannot be set — so the strip moves instead.
//
// The content is moved with a transform rather than by layout, the way the score plane
// is panned, so nothing is measured again while it travels.
//
// What separates this from ScrollArea is which press it moves on. The plane pans
// whatever press its content lets through, because on the plane a press means an edit
// often enough that the score view has to decide first. A strip of chrome has no such
// ground to spare: what is between two switches is a three pixel gap, and what is
// between two rows of a panel is another three. So the press is watched on its way down
// to the control it landed on, and taken away from that control if and only if it
// travels far enough along the strip to be a pan rather than a press.

sealed class ScrollStrip : VisualElement
{
    // Public properties

    public float Offset { get => _offset; set => SetOffset(value); }

    // How far the strip could travel, which is nothing at all on a screen it fits.
    public float Travel
      => Mathf.Max(Along(_content.layout.size) - Along(contentRect.size), 0.0f);

    // VisualElement implementation

    public override VisualElement contentContainer => _content;

    // vertical is the whole of the difference between a column of panels and a row of
    // switches. Everything below reads its axis through Along, so there is one set of
    // rules and not two that have to be kept in step.
    public ScrollStrip(bool vertical)
    {
        _vertical = vertical;

        style.overflow = Overflow.Hidden;

        // Stretched along the strip rather than laid out in it, so that what the strip
        // is holding can be longer than the strip without the strip growing to hold it —
        // which is the whole point. Across it the content follows the strip, so a panel
        // is as wide as the column and a control sits centred on the height of the row.
        _content = new VisualElement { name = "scroll-strip-content" };
        _content.style.position = Position.Absolute;
        _content.style.left = 0;
        _content.style.top = 0;

        if (vertical)
        {
            _content.style.right = 0;
            _content.style.flexDirection = FlexDirection.Column;
        }
        else
        {
            _content.style.bottom = 0;
            _content.style.flexDirection = FlexDirection.Row;
            _content.style.alignItems = Align.Center;
        }

        hierarchy.Add(_content);

        // On the way in rather than on the way out. A Button's Clickable stops the press
        // at the button, so a handler waiting for one to bubble back up here would never
        // see it — and every one of these events belongs to something else until it
        // turns out to be a pan.
        RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);

        // Only our own. Taking the capture off a button sends this event to the button,
        // and it bubbles up through here on its way out: read without the check, the pan
        // would end itself in the same breath as it began.
        RegisterCallback<PointerCaptureOutEvent>(evt => { if (evt.target == this) End(); });

        RegisterCallback<WheelEvent>(OnWheel);

        // Either side of the travel can change without the other moving: a window
        // resized shortens the strip, and a panel built for another kind of cell
        // lengthens what is on it.
        RegisterCallback<GeometryChangedEvent>(_ => Clamp());
        _content.RegisterCallback<GeometryChangedEvent>(_ => Clamp());
    }

    // Private members

    readonly VisualElement _content;
    readonly bool _vertical;

    float _offset;

    // What the press landed on, and therefore what is probably holding the pointer. See
    // Watch: the strip has to hear moves that are being delivered to that control alone.
    VisualElement _pressedOn;

    float _origin;  // Pointer position along the strip that the travel is measured from
    int _pointerId; // The finger the press belongs to, and no other
    bool _pressed;
    bool _dragging;

    // The same distance a press on the plane has to travel before it is a drag. One
    // number for all of them, because it is a property of the hand rather than of what
    // is under it.
    const float DragThreshold = 4.0f;

    const float WheelSpeed = 2.0f;

    // The one component of a point or a size that this strip moves along.
    float Along(Vector2 value) => _vertical ? value.y : value.x;

    void SetOffset(float offset)
    {
        _offset = Mathf.Clamp(offset, 0.0f, Travel);
        _content.style.translate = _vertical ? new Translate(0.0f, -_offset)
                                             : new Translate(-_offset, 0.0f);
    }

    void Clamp() => SetOffset(_offset);

    // Listens on the control a press landed on for as long as that press lasts.
    //
    // A pointer held by a control is delivered to that control and to nothing else: the
    // event does not travel the hierarchy at all, so the handlers above are deaf from
    // the moment a Button's Clickable takes the press until it gives it back. That is
    // exactly the stretch a drag happens in, so the strip listens where the events
    // actually are — on the control itself — and stops as soon as the press is over.
    //
    // Two copies of each handler are live for the length of a press, and a move may be
    // answered by only one of them or the strip would travel twice as far as the hand.
    // Which one answers is Mine. A release is not the same case: it ends the press
    // whichever copy hears it, and the second copy finds nothing left to end.
    void Watch(VisualElement element)
    {
        Unwatch();

        if (element == null || element == this) return;

        _pressedOn = element;
        _pressedOn.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        _pressedOn.RegisterCallback<PointerUpEvent>(OnPointerUp);
    }

    void Unwatch()
    {
        if (_pressedOn == null) return;

        _pressedOn.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        _pressedOn.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        _pressedOn = null;
    }

    // Whether this is the copy a move is reaching the strip through. The one on the
    // pressed control answers while that control is holding the pointer, and the
    // strip's own answers the rest of the time — since an event that is not being
    // delivered to a holder is one that travels the whole way down and is seen here
    // anyway.
    bool Mine(IEventHandler currentTarget, int pointerId)
      => currentTarget == (IEventHandler)this
         ? _pressedOn == null || !_pressedOn.HasPointerCapture(pointerId)
         : _pressedOn != null && _pressedOn.HasPointerCapture(pointerId);

    void End()
    {
        Unwatch();
        (_pressed, _dragging) = (false, false);
    }

    // Nothing is captured here and nothing is stopped: at this point the press still
    // belongs to whatever it landed on, which is free to light up, and most of them will
    // never be anything else.
    void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0 || KeepsItsOwnDrag(evt.target)) return;

        (_pressed, _dragging) = (true, false);
        (_pointerId, _origin) = (evt.pointerId, Along(evt.position));
        Watch(evt.target as VisualElement);
    }

    void OnPointerMove(PointerMoveEvent evt)
    {
        if (!_pressed || evt.pointerId != _pointerId) return;
        if (!Mine(evt.currentTarget, evt.pointerId)) return;

        var position = Along(evt.position);

        if (!_dragging)
        {
            // A strip with nowhere to go never takes a press off anything, so on a
            // screen wide or tall enough for it every control behaves exactly as it did
            // before this element existed.
            if (Travel <= 0.0f) return;

            // Along the strip only. It moves in one direction, so a press dragged across
            // it is not a pan of it and has no business being taken from whatever is
            // under it.
            if (Mathf.Abs(position - _origin) < DragThreshold) return;

            // Taking the capture is what cancels the press that was going to be a click:
            // the release goes to whoever holds the pointer, and the button that was
            // pressed hears that it lost it and puts itself back down. It cannot be left
            // to the release landing outside the button, the way a list of them usually
            // decides — the button travels with the strip and so stays under the pointer
            // for the whole of the pan.
            //
            // The travel spent reaching the threshold is spent rather than banked, or the
            // strip would jump by it the moment it starts to move.
            (_dragging, _origin) = (true, position);
            this.CapturePointer(evt.pointerId);
            return;
        }

        SetOffset(_offset - (position - _origin));
        _origin = position;
        evt.StopPropagation();
    }

    // Every release ends the press, whichever copy of this hears it and whether or not
    // it was a pan — a strip still holding a press that is over would read the next
    // pointer to cross it as a hand that never let go. There is nothing to give back
    // unless it panned: while it did, the strip is the one holding the pointer and the
    // release comes to it alone.
    void OnPointerUp(PointerUpEvent evt)
    {
        if (!_pressed || evt.pointerId != _pointerId) return;

        if (_dragging)
        {
            this.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        End();
    }

    // Whichever way the wheel is turned, the strip moves the one way it can. A mouse has
    // one wheel and it is the vertical one, so a row that read only its own axis could
    // not be turned with a mouse at all; the larger of the two is what the hand meant
    // either way.
    void OnWheel(WheelEvent evt)
    {
        var delta = evt.delta;
        var travel = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? delta.x : delta.y;

        SetOffset(_offset + travel * WheelSpeed);
        evt.StopPropagation();
    }

    // A control whose own gesture is a drag keeps it. That is the value bar wherever one
    // stands: it is scrubbed rather than pressed, and it reads both axes, so a bar handed
    // the strip's rule would be a bar that cannot be set on the screens where the rule
    // applies — the tempo on a narrow row, every parameter on a panel too tall for the
    // screen.
    static bool KeepsItsOwnDrag(IEventHandler target)
      => target is VisualElement element &&
         (element is ValueBar || element.GetFirstAncestorOfType<ValueBar>() != null);
}

} // namespace Jacquard.App
