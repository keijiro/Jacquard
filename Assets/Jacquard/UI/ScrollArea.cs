using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Jacquard.App {

// A pannable viewport that scrolls its content with trackpad gestures and
// command-key drags, after the uitk-scrollarea experiment. The content is moved
// with a transform rather than by layout, so panning stays cheap however large the
// plane grows — and a score plane is meant to grow.

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
        RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);
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
    bool _dragging;

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
        if (!IsPanModifierHeld(evt)) return;
        (_dragging, _dragPoint) = (true, evt.position);
        this.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    void OnPointerMove(PointerMoveEvent evt)
    {
        if (!_dragging) return;
        var point = (Vector2)evt.position;
        SetOffset(_offset - (point - _dragPoint));
        _dragPoint = point;
        evt.StopPropagation();
    }

    void OnPointerUp(PointerUpEvent evt)
    {
        if (!_dragging) return;
        _dragging = false;
        this.ReleasePointer(evt.pointerId);
        evt.StopPropagation();
    }
}

} // namespace Jacquard.App
