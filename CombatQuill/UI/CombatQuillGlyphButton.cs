using Godot;

namespace CombatQuill.UI;

internal sealed partial class CombatQuillGlyphButton : Button
{
    internal enum GlyphKind
    {
        Settings
    }

    private GlyphKind _glyphKind;

    private Color _activeColor = Colors.White;

    private Color _inactiveColor = new(1f, 1f, 1f, 0.5f);

    private bool _isActive;

    public event Action? Activated;

    public void Initialize(GlyphKind glyphKind, Color activeColor, Color inactiveColor)
    {
        _glyphKind = glyphKind;
        _activeColor = activeColor;
        _inactiveColor = inactiveColor;
    }

    public override void _Ready()
    {
        Flat = true;
        FocusMode = FocusModeEnum.All;
        CustomMinimumSize = new Vector2(34f, 34f);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        AddThemeStyleboxOverride("disabled", new StyleBoxEmpty());

        Pressed += OnPressedInternal;
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
    }

    public void SetActiveState(bool active)
    {
        _isActive = active;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var rect = GetRect();
        var center = rect.Size * 0.5f;
        var highlighted = _isActive || HasFocus() || IsHovered();
        var glyphColor = highlighted ? _activeColor : _inactiveColor;
        var accentColor = new Color(_activeColor, highlighted ? 0.16f : 0f);
        var plateColor = new Color(1f, 1f, 1f, highlighted ? 0.06f : 0.03f);

        if (accentColor.A > 0f)
        {
            DrawCircle(center, 12.75f, accentColor);
        }

        switch (_glyphKind)
        {
            case GlyphKind.Settings:
                DrawCircle(center, 8.5f, plateColor);
                DrawSettingsGlyph(center, glyphColor, highlighted);
                break;
        }
    }

    private void OnPressedInternal()
    {
        Activated?.Invoke();
    }

    private void DrawSettingsGlyph(Vector2 center, Color color, bool highlighted)
    {
        var shadowColor = new Color(0f, 0f, 0f, highlighted ? 0.22f : 0.16f);
        var lineColor = highlighted ? color.Lightened(0.08f) : color;
        const float outerRadius = 6.1f;
        const float innerRadius = 2.45f;
        const float toothStart = 6.85f;
        const float toothEnd = 9.5f;
        const float shadowOffset = 0.65f;

        DrawArc(center + new Vector2(0f, shadowOffset), outerRadius, 0f, Mathf.Tau, 28, shadowColor, 3.1f, antialiased: true);
        DrawArc(center, outerRadius, 0f, Mathf.Tau, 28, lineColor, 2.45f, antialiased: true);
        DrawCircle(center + new Vector2(0f, shadowOffset), innerRadius + 0.4f, shadowColor);
        DrawCircle(center, innerRadius, lineColor);

        for (var index = 0; index < 6; index++)
        {
            var angle = index * Mathf.Tau / 6f + Mathf.Pi / 6f;
            var direction = Vector2.Right.Rotated(angle);
            DrawLine(
                center + new Vector2(0f, shadowOffset) + direction * toothStart,
                center + new Vector2(0f, shadowOffset) + direction * toothEnd,
                shadowColor,
                3.35f,
                antialiased: true);
            DrawLine(
                center + direction * toothStart,
                center + direction * toothEnd,
                lineColor,
                2.5f,
                antialiased: true);
        }
    }
}
