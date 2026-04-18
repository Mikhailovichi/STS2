using Godot;

namespace RandomVision.Services;

internal partial class RandomVisionOverlayPanel : PanelContainer
{
    private bool _isDragging;
    private bool _isCollapsed;
    private float _dragRegionHeight = 30f;
    private float _dragBlockedTopRightWidth;
    private Vector2 _dragOffset;
    private float _viewportPadding = 12f;

    public event Action<Vector2>? PositionCommitted;
    public event Action<float>? ZoomRequested;

    public bool IsCollapsed => _isCollapsed;

    public override void _Ready()
    {
        Position = SnapToPixel(Position);
    }

    public void ConfigureDrag(float dragRegionHeight, float viewportPadding, float dragBlockedTopRightWidth = 0f)
    {
        _dragRegionHeight = dragRegionHeight;
        _viewportPadding = viewportPadding;
        _dragBlockedTopRightWidth = dragBlockedTopRightWidth;
    }

    public void SetCollapsed(bool collapsed)
    {
        _isCollapsed = collapsed;
    }

    public override void _Input(InputEvent @event)
    {
        if (TryHandleZoomInput(@event))
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButtonEvent &&
            mouseButtonEvent.ButtonIndex == MouseButton.Left)
        {
            if (mouseButtonEvent.Pressed && IsPointInsideDragRegion(mouseButtonEvent.GlobalPosition))
            {
                _isDragging = true;
                _dragOffset = mouseButtonEvent.GlobalPosition - GlobalPosition;
                MoveToFront();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!mouseButtonEvent.Pressed && _isDragging)
            {
                _isDragging = false;
                PositionCommitted?.Invoke(Position);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventMouseMotion mouseMotionEvent && _isDragging)
        {
            GlobalPosition = SnapToPixel(mouseMotionEvent.GlobalPosition - _dragOffset);
            ClampToParent();
            PositionCommitted?.Invoke(Position);
            GetViewport().SetInputAsHandled();
        }
    }

    private bool TryHandleZoomInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButtonEvent &&
            mouseButtonEvent.Pressed &&
            mouseButtonEvent.CtrlPressed &&
            IsPointInsidePanel(mouseButtonEvent.GlobalPosition))
        {
            var wheelZoomDelta = mouseButtonEvent.ButtonIndex switch
            {
                MouseButton.WheelUp => 1f,
                MouseButton.WheelDown => -1f,
                _ => 0f
            };
            if (!Mathf.IsZeroApprox(wheelZoomDelta))
            {
                ZoomRequested?.Invoke(wheelZoomDelta);
                GetViewport().SetInputAsHandled();
                return true;
            }
        }

        if (@event is InputEventKey keyEvent &&
            keyEvent.Pressed &&
            !keyEvent.Echo &&
            keyEvent.CtrlPressed)
        {
            var keyZoomDelta = keyEvent.Keycode switch
            {
                Key.Equal or Key.KpAdd => 1f,
                Key.Minus or Key.KpSubtract => -1f,
                _ => 0f
            };
            if (!Mathf.IsZeroApprox(keyZoomDelta))
            {
                ZoomRequested?.Invoke(keyZoomDelta);
                GetViewport().SetInputAsHandled();
                return true;
            }
        }

        return false;
    }

    private bool IsPointInsideDragRegion(Vector2 globalPoint)
    {
        var rect = GetGlobalRect();
        return rect.HasPoint(globalPoint) &&
               globalPoint.X <= rect.End.X - _dragBlockedTopRightWidth &&
               globalPoint.Y <= rect.Position.Y + _dragRegionHeight;
    }

    private bool IsPointInsidePanel(Vector2 globalPoint)
    {
        return GetGlobalRect().HasPoint(globalPoint);
    }

    private void ClampToParent()
    {
        if (GetParent() is not Control parent)
        {
            return;
        }

        var maxX = Mathf.Max(_viewportPadding, parent.Size.X - Size.X - _viewportPadding);
        var maxY = Mathf.Max(_viewportPadding, parent.Size.Y - Size.Y - _viewportPadding);
        Position = SnapToPixel(new Vector2(
            Mathf.Clamp(Position.X, _viewportPadding, maxX),
            Mathf.Clamp(Position.Y, _viewportPadding, maxY)));
    }

    private static Vector2 SnapToPixel(Vector2 position)
    {
        return new Vector2(Mathf.Round(position.X), Mathf.Round(position.Y));
    }
}
