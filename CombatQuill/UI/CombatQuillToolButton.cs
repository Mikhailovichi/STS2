using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace CombatQuill.UI;

internal sealed partial class CombatQuillToolButton : Button
{
    private string _iconPath = string.Empty;

    private string _glowPath = string.Empty;

    private Color _activeColor = Colors.White;

    private Color _inactiveColor = new(1f, 1f, 1f, 0.5f);

    private Func<HoverTip>? _hoverTipFactory;

    private TextureRect? _icon;

    private Tween? _tween;

    private bool _isActive;

    public event Action? Activated;

    public void Initialize(
        string iconPath,
        string glowPath,
        Color activeColor,
        Color inactiveColor,
        Func<HoverTip> hoverTipFactory)
    {
        _iconPath = iconPath;
        _glowPath = glowPath;
        _activeColor = activeColor;
        _inactiveColor = inactiveColor;
        _hoverTipFactory = hoverTipFactory;
        RefreshVisualState(immediate: true, hovered: false);
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

        var center = new CenterContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        _icon = new TextureRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(22f, 22f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        _icon.PivotOffset = _icon.CustomMinimumSize * 0.5f;
        center.AddChild(_icon);

        Pressed += OnPressedInternal;
        MouseEntered += OnMouseEnteredInternal;
        MouseExited += OnMouseExitedInternal;
        FocusEntered += OnMouseEnteredInternal;
        FocusExited += OnMouseExitedInternal;

        RefreshVisualState(immediate: true, hovered: false);
    }

    public void SetActiveState(bool active)
    {
        _isActive = active;
        RefreshVisualState(immediate: false, hovered: HasFocus() || IsHovered());
    }

    private void OnPressedInternal()
    {
        Activated?.Invoke();
    }

    private void OnMouseEnteredInternal()
    {
        RefreshVisualState(immediate: false, hovered: true);
        if (_hoverTipFactory is null)
        {
            return;
        }

        var tipSet = NHoverTipSet.CreateAndShow(this, _hoverTipFactory.Invoke());
        tipSet.GlobalPosition = GlobalPosition + new Vector2(-32f, -132f);
    }

    private void OnMouseExitedInternal()
    {
        RefreshVisualState(immediate: false, hovered: false);
        NHoverTipSet.Remove(this);
    }

    private void RefreshVisualState(bool immediate, bool hovered)
    {
        if (_icon is null || string.IsNullOrWhiteSpace(_iconPath) || string.IsNullOrWhiteSpace(_glowPath))
        {
            return;
        }

        var targetTexture = PreloadManager.Cache.GetTexture2D(_isActive || hovered ? _glowPath : _iconPath);
        var targetColor = hovered ? _activeColor : (_isActive ? _activeColor : _inactiveColor);
        var targetScale = hovered ? Vector2.One * 1.2f : Vector2.One * 1.1f;

        _icon.Texture = targetTexture;

        if (immediate)
        {
            _icon.SelfModulate = targetColor;
            _icon.Scale = targetScale;
            return;
        }

        _tween?.Kill();
        _tween = CreateTween().SetParallel();
        _tween.TweenProperty(_icon, "self_modulate", targetColor, 0.06);
        _tween.TweenProperty(_icon, "scale", targetScale, 0.06);
    }
}
