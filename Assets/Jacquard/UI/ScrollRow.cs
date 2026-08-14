using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// A row of controls that slides sideways when it is wider than the screen it is on.
//
// The transport row is the one part of the chrome whose width is not a decision: it
// carries a switch for each thing no cell can ask for, and each of those is as wide as
// the word written on it. On a narrow screen the row runs off the right edge, and a
// switch off the edge is a switch that cannot be pressed — the same failure as one that
// was never built. Dropping a control to make the rest fit trades a switch nobody can
// reach for a switch nobody has; moving the row is what keeps all of them.
//
// The content is moved with a transform rather than by layout, the way the score plane
// is panned, so nothing is measured again while the row travels.
//
// What separates this from ScrollArea is which press it pans on. The plane pans
// whatever press its content lets through, because on the plane a press means an edit
// often enough that the score view has to decide first. A row of buttons has no such
// ground to spare: what is between two switches is a three pixel gap, and a pan that can
// only be started in one cannot be started at all. So the press is watched on its way
// down to the control it landed on, and taken away from that control if and only if it
// travels far enough sideways to be a pan rather than a press.

sealed class ScrollRow : VisualElement
{
    // Public properties

    public float Offset { get => _offset; set => SetOffset(value); }

    // How far the row could travel, which is nothing at all on a screen it fits.
    public float Travel
      => Mathf.Max(_content.layout.width - contentRect.width, 0.0f);

    // VisualElement implementation

    public override VisualElement contentContainer => _content;

    public ScrollRow()
    {
        style.overflow = Overflow.Hidden;

        // Stretched down the row rather than laid out in it, so that what the row is
        // holding can be wider than the row without the row growing to hold it — which
        // is the whole point — and so that a control still sits centred on the height
        // it was given.
        _content = new VisualElement { name = "scroll-row-content" };
        _content.style.position = Position.Absolute;
        _content.style.left = 0;
        _content.style.top = 0;
        _content.style.bottom = 0;
        _content.style.flexDirection = FlexDirection.Row;
        _content.style.alignItems = Align.Center;
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
        // resized narrows the row, and a slot name of a different length widens what is
        // on it.
        RegisterCallback<GeometryChangedEvent>(_ => Clamp());
        _content.RegisterCallback<GeometryChangedEvent>(_ => Clamp());
    }

    // Private members

    readonly VisualElement _content;

    float _offset;

    // What the press landed on, and therefore what is probably holding the pointer. See
    // Watch: the row has to hear moves that are being delivered to that control alone.
    VisualElement _pressedOn;

    float _origin;  // Pointer x the travel so far is measured from
    int _pointerId; // The finger the press belongs to, and no other
    bool _pressed;
    bool _dragging;

    // The same distance a press on the plane has to travel before it is a drag. One
    // number for both, because it is a property of the hand rather than of what is
    // under it.
    const float DragThreshold = 4.0f;

    const float WheelSpeed = 2.0f;

    void SetOffset(float offset)
    {
        _offset = Mathf.Clamp(offset, 0.0f, Travel);
        _content.style.translate = new Translate(-_offset, 0.0f);
    }

    void Clamp() => SetOffset(_offset);

    // Listens on the control a press landed on for as long as that press lasts.
    //
    // A pointer held by a control is delivered to that control and to nothing else: the
    // event does not travel the hierarchy at all, so the handlers above are deaf from
    // the moment a Button's Clickable takes the press until it gives it back. That is
    // exactly the stretch a drag happens in, so the row listens where the events
    // actually are — on the control itself — and stops as soon as the press is over.
    //
    // Two copies of each handler are live for the length of a press, and a move may be
    // answered by only one of them or the row would travel twice as far as the hand.
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

    // Whether this is the copy a move is reaching the row through. The one on the
    // pressed control answers while that control is holding the pointer, and the row's
    // own answers the rest of the time — since an event that is not being delivered to
    // a holder is one that travels the whole way down and is seen here anyway.
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
        (_pointerId, _origin) = (evt.pointerId, evt.position.x);
        Watch(evt.target as VisualElement);
    }

    void OnPointerMove(PointerMoveEvent evt)
    {
        if (!_pressed || evt.pointerId != _pointerId) return;
        if (!Mine(evt.currentTarget, evt.pointerId)) return;

        var x = evt.position.x;

        if (!_dragging)
        {
            // A row with nowhere to go never takes a press off anything, so on a screen
            // wide enough for it every control behaves exactly as it did before this
            // element existed.
            if (Travel <= 0.0f) return;

            // Sideways only. The row moves in one direction, so a press dragged down the
            // screen is not a pan of it and has no business being taken from whatever is
            // under it.
            if (Mathf.Abs(x - _origin) < DragThreshold) return;

            // Taking the capture is what cancels the press that was going to be a click:
            // the release goes to whoever holds the pointer, and the button that was
            // pressed hears that it lost it and puts itself back down. It cannot be left
            // to the release landing outside the button, the way a list of them usually
            // decides — the button travels with the row and so stays under the pointer
            // for the whole of the pan.
            //
            // The travel spent reaching the threshold is spent rather than banked, or the
            // row would jump by it the moment it starts to move.
            (_dragging, _origin) = (true, x);
            this.CapturePointer(evt.pointerId);
            return;
        }

        SetOffset(_offset - (x - _origin));
        _origin = x;
        evt.StopPropagation();
    }

    // Every release ends the press, whichever copy of this hears it and whether or not
    // it was a pan — a row still holding a press that is over would read the next
    // pointer to cross it as a hand that never let go, and pan on a screen nobody is
    // touching. There is nothing to give back unless it panned: while it did, the row
    // is the one holding the pointer and the release comes to it alone.
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

    // Whichever way the wheel is turned, the row moves the one way it can. A trackpad
    // swept sideways over the row and a mouse wheel turned on it are the same gesture
    // here, since there is nothing above or below to reach.
    void OnWheel(WheelEvent evt)
    {
        var delta = evt.delta;
        var travel = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? delta.x : delta.y;

        SetOffset(_offset + travel * WheelSpeed);
        evt.StopPropagation();
    }

    // A control whose own gesture is a drag keeps it. On the transport that is the
    // tempo, which is scrubbed rather than pressed: a bar handed the row's rule would
    // be a bar that cannot be set on the screens where the rule applies.
    static bool KeepsItsOwnDrag(IEventHandler target)
      => target is VisualElement element &&
         (element is ValueBar || element.GetFirstAncestorOfType<ValueBar>() != null);
}

} // namespace Jacquard.App
