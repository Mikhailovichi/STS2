using Godot;

namespace CombatQuill.UI;

internal sealed partial class CombatQuillColorSwatchButton : Button
{
    private Color _swatchColor = Colors.White;

    private bool _isSelected;

    public override void _Ready()
    {
        Flat = true;
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        CustomMinimumSize = new Vector2(24f, 24f);
        AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        AddThemeStyleboxOverride("disabled", new StyleBoxEmpty());

        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
    }

    public void SetSwatch(Color swatchColor, bool selected)
    {
        _swatchColor = swatchColor;
        _swatchColor.A = 1f;
        _isSelected = selected;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        var hovered = IsHovered() || HasFocus();
        var fillColor = hovered ? _swatchColor.Lightened(0.08f) : _swatchColor;
        var ringColor = _isSelected ? Colors.White : new Color(1f, 1f, 1f, hovered ? 0.5f : 0.28f);

        if (_isSelected)
        {
            DrawCircle(center, 11.5f, new Color(1f, 1f, 1f, 0.14f));
        }

        DrawCircle(center, 9.25f, fillColor);
        DrawArc(center, 9.25f, 0f, Mathf.Tau, 40, ringColor, _isSelected ? 2.5f : 1.5f, antialiased: true);

        if (hovered && !_isSelected)
        {
            DrawArc(center, 11.25f, 0f, Mathf.Tau, 40, new Color(1f, 1f, 1f, 0.18f), 1.25f, antialiased: true);
        }
    }
}
