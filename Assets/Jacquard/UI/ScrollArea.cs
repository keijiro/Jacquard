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
        RegisterCallback<GeometryChangedEvent>(_ => SetOffset(_offset));
        _content.RegisterCallback<GeometryChangedEvent>(_ => SetOffset(_offset));
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
    Vector2 _dragPoint;
    bool _pressed;
    bool _dragging;

    // A press only becomes a pan once it has travelled far enough to mean one, so
    // that a tap that wobbles under a fingertip still reads as a tap and leaves the
    // plane where it stood.
    const float DragThreshold = 4.0f;

    void SetOffset(Vector2 offset)
    {
        var max = (Vector2)_content.layout.size - contentRect.size;
        max = Vector2.Max(max, Vector2.zero);
        _offset = Vector2.Min(Vector2.Max(offset, Vector2.zero), max);
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
