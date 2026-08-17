using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Jacquard.App {

// A pannable viewport that scrolls its content with trackpad gestures and with
// drags, after the uitk-scrollarea experiment. The content is moved with a
// transform rather than by layout, so panning stays cheap however large the plane
// grows — and a score plane is meant to grow.
//
// Which drags reach here is not this element's business: it pans whatever press
// its content let through. The score view stops the ones that mean an edit, so a
// press on free ground arrives and a press on a tile does not.

[UxmlElement]
public sealed partial class ScrollArea : VisualElement
{
    // Public properties

    [UxmlAttribute]
    public float WheelSpeed { get => _wheelSpeed; set => _wheelSpeed = value; }

    [UxmlAttribute]
    public bool InvertWheel { get => _invertWheel; set => _invertWheel = value; }

    public Vector2 Offset { get => _offset; set => SetOffset(value); }

    // A strip along the bottom edge that will not start a pan.
    //
    // What happens there is not this element's to have. A drag that begins at the bottom
    // of a phone is the gesture that puts the app away, and the system claims it before
    // the app is told a finger landed: the plane would follow the hand for a few pixels,
    // hear that its press was cancelled, and stop — with the app going to the home screen
    // over the top of it. So a press down there is left alone, and a pan begins a
    // fingertip higher up.
    //
    // Only presses, and only this edge. A cell that happens to lie in the strip is still
    // edited, because an edit is a tap and a tap is not what the system is watching for;
    // and the top and sides of the plane are nobody else's — what the screen keeps on its
    // sides is not touchable glass at all, and the gesture at the top of the screen
    // begins on the transport row, which only ever travels sideways.
    public float DeadBottom { get; set; }

    // The offset that was asked for, which is not always the one in force: a plane that
    // has just grown is a plane whose layout has not run yet, so a request reaching past
    // what it used to hold is clamped for a frame. Anything that means to survive that
    // frame reads this and not Offset.
    public Vector2 Requested => _requested;

    // Takes up a distance without reading back what is in force, which is what the
    // score plane needs when it moves the score bodily underneath itself: the plane grew
    // on its left and everything on it went right, and adding that here is what leaves
    // the picture where it stood. Reading Offset instead would throw away a request the
    // layout has not caught up with, which is the whole of what this exists for.
    public void Shift(Vector2 delta)
    {
        _requested += delta;
        Clamp();
    }

    // VisualElement implementation

    public override VisualElement contentContainer => _content;

    public ScrollArea()
    {
        style.overflow = Overflow.Hidden;

        _content = new VisualElement { name = "scroll-area-content" };
        _content.style.position = Position.Absolute;
        _content.style.left = 0;
        _content.style.top = 0;
        hierarchy.Add(_content);

        RegisterCallback<WheelEvent>(OnWheel);
        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerUpEvent>(OnPointerUp);
        RegisterCallback<PointerCaptureOutEvent>(_ => (_pressed, _dragging) = (false, false));
        RegisterCallback<GeometryChangedEvent>(_ => Clamp());

        // The content's own geometry is what a request is waiting on, so this is where a
        // request stops being pending: the size it was clamped against is now the real
        // one. Collapsing it here and not on the viewport's event matters — the viewport
        // can be resized in a pass the content has not been measured in, and a request
        // dropped there would be dropped before it was ever answered. Collapsed at all
        // because a request that outlived its answer would spring the view back to it
        // the next time the plane happened to grow.
        _content.RegisterCallback<GeometryChangedEvent>(_ => { Clamp(); _requested = _offset; });
    }

    // Holding the pan modifier grabs the plane instead of editing it, which is
    // also how the score view knows to leave a click alone.
    public static bool IsPanModifierHeld(IPointerEvent evt)
    {
        if (evt.actionKey || evt.commandKey || evt.ctrlKey) return true;
        var keyboard = Keyboard.current;
        if (keyboard == null) return false;
        return keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed ||
               keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
    }

    // Private members

    readonly VisualElement _content;

    float _wheelSpeed = 2.0f;
    bool _invertWheel;

    Vector2 _offset;

    // What was asked for, against _offset which is what the plane can currently give.
    // A gesture adds its delta to the latter and never to this, which is what keeps a
    // pan against an edge from banking travel: push at the end of the plane for a second
    // and it comes back the moment the hand turns round rather than a second later. Only
    // something that has asked for ground the layout has not caught up with reads this.
    Vector2 _requested;

    Vector2 _dragPoint;
    bool _pressed;
    bool _dragging;

    // A press only becomes a pan once it has travelled far enough to mean one, so
    // that a tap that wobbles under a fingertip still reads as a tap and leaves the
    // plane where it stood.
    const float DragThreshold = 4.0f;

    void SetOffset(Vector2 offset)
    {
        _requested = offset;
        Clamp();
    }

    // What the request comes to against the plane as it is now laid out.
    void Clamp()
    {
        var max = (Vector2)_content.layout.size - contentRect.size;
        max = Vector2.Max(max, Vector2.zero);
        _offset = Vector2.Min(Vector2.Max(_requested, Vector2.zero), max);
        _content.style.translate = new Translate(-_offset.x, -_offset.y);
    }

    void OnWheel(WheelEvent evt)
    {
        var speed = _wheelSpeed * (_invertWheel ? -1.0f : 1.0f);
        SetOffset(_offset + (Vector2)evt.delta * speed);
        evt.StopPropagation();
    }

    void OnPointerDown(PointerDownEvent evt)
    {
        // Not captured and not stopped, so the press carries on to whatever is above this
        // element — which is nothing that pans, and that is the point.
        if (DeadBottom > 0.0f &&
            this.WorldToLocal(evt.position).y > contentRect.height - DeadBottom) return;

        (_pressed, _dragging, _dragPoint) = (true, false, evt.position);
        this.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    void OnPointerMove(PointerMoveEvent evt)
    {
        if (!_pressed) return;

        var point = (Vector2)evt.position;

        // The travel spent reaching the threshold is spent, not banked: taking it
        // as offset too would jerk the plane by four pixels the moment it moves.
        if (!_dragging)
        {
            if ((point - _dragPoint).magnitude < DragThreshold) return;
            (_dragging, _dragPoint) = (true, point);
            return;
        }

        SetOffset(_offset - (point - _dragPoint));
        _dragPoint = point;
        evt.StopPropagation();
    }

    void OnPointerUp(PointerUpEvent evt)
    {
        if (!_pressed) return;
        (_pressed, _dragging) = (false, false);
        this.ReleasePointer(evt.pointerId);
        evt.StopPropagation();
    }
}

} // namespace Jacquard.App
