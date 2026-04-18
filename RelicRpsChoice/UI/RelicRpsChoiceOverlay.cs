using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using RelicRpsChoice.Services;

namespace RelicRpsChoice.UI;

public partial class RelicRpsChoiceOverlay : Control
{
    private Label? _titleLabel;
    private Label? _statusLabel;
    private Button? _rockButton;
    private Button? _paperButton;
    private Button? _scissorsButton;

    public override void _Ready()
    {
        Name = "RelicRpsChoiceOverlay";
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        BuildUi();
        RelicRpsFightService.FightStateChanged += OnFightStateChanged;
        RefreshFromService();
    }

    public override void _ExitTree()
    {
        RelicRpsFightService.FightStateChanged -= OnFightStateChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        var snapshot = RelicRpsFightService.GetUiSnapshot();
        if (!Visible || !snapshot.IsVisible || !snapshot.CanChoose)
        {
            return;
        }

        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        if (TryMapShortcutToMove(keyEvent.Keycode, out var shortcutMove))
        {
            SubmitMove(shortcutMove);
            GetViewport().SetInputAsHandled();
            return;
        }

        switch (keyEvent.Keycode)
        {
            case Key.Left:
            case Key.A:
                CycleFocus(-1);
                GetViewport().SetInputAsHandled();
                return;
            case Key.Right:
            case Key.D:
                CycleFocus(1);
                GetViewport().SetInputAsHandled();
                return;
            case Key.Up:
            case Key.Down:
                CycleFocus(1);
                GetViewport().SetInputAsHandled();
                return;
            case Key.Enter:
            case Key.KpEnter:
            case Key.Space:
                if (GetFocusedMove() is { } focusedMove)
                {
                    SubmitMove(focusedMove);
                    GetViewport().SetInputAsHandled();
                }

                return;
        }
    }

    public void RefreshFromService()
    {
        var snapshot = RelicRpsFightService.GetUiSnapshot();
        Visible = snapshot.IsVisible;
        if (!snapshot.IsVisible)
        {
            return;
        }

        if (_titleLabel is not null)
        {
            _titleLabel.Text = snapshot.Title;
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Text = snapshot.Status;
        }

        ApplyButtonState(_rockButton, "Rock", snapshot.ShowButtons, snapshot.CanChoose);
        ApplyButtonState(_paperButton, "Paper", snapshot.ShowButtons, snapshot.CanChoose);
        ApplyButtonState(_scissorsButton, "Scissors", snapshot.ShowButtons, snapshot.CanChoose);

        if (snapshot.CanChoose)
        {
            CallDeferred(nameof(FocusDefaultButton));
        }
    }

    private void OnFightStateChanged()
    {
        CallDeferred(nameof(RefreshFromService));
    }

    private void BuildUi()
    {
        var anchor = new CenterContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        anchor.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        anchor.OffsetTop = -170f;
        anchor.OffsetBottom = -24f;
        AddChild(anchor);

        var panel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(420f, 0f)
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        anchor.AddChild(panel);

        var margin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Pass
        };
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(margin);

        var root = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Pass
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        _titleLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 18);
        _titleLabel.AddThemeColorOverride("font_color", new Color("F5F2E8"));
        root.AddChild(_titleLabel);

        _statusLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);
        _statusLabel.AddThemeColorOverride("font_color", new Color("D9E3EE"));
        root.AddChild(_statusLabel);

        var buttons = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        buttons.AddThemeConstantOverride("separation", 10);
        root.AddChild(buttons);

        _rockButton = CreateChoiceButton("Rock", () => SubmitMove(RelicPickingFightMove.Rock));
        _paperButton = CreateChoiceButton("Paper", () => SubmitMove(RelicPickingFightMove.Paper));
        _scissorsButton = CreateChoiceButton("Scissors", () => SubmitMove(RelicPickingFightMove.Scissors));

        buttons.AddChild(_rockButton);
        buttons.AddChild(_paperButton);
        buttons.AddChild(_scissorsButton);

        WireControllerFocus();
    }

    private void SubmitMove(RelicPickingFightMove move)
    {
        if (RelicRpsFightService.SubmitLocalMove(move))
        {
            RefreshFromService();
        }
    }

    private void FocusDefaultButton()
    {
        _rockButton?.GrabFocus();
    }

    private void ApplyButtonState(Button? button, string label, bool showButtons, bool canChoose)
    {
        if (button is null)
        {
            return;
        }

        button.Visible = showButtons;
        button.Disabled = !canChoose;
        button.Text = label;
        button.AddThemeStyleboxOverride(
            "normal",
            CreateButtonStyle(new Color(0.16f, 0.22f, 0.30f, 0.95f)));
        button.AddThemeStyleboxOverride(
            "hover",
            CreateButtonStyle(new Color(0.24f, 0.32f, 0.43f, 1f)));
        button.AddThemeStyleboxOverride(
            "pressed",
            CreateButtonStyle(new Color(0.21f, 0.28f, 0.38f, 1f)));
        button.AddThemeStyleboxOverride(
            "disabled",
            CreateButtonStyle(new Color(0.12f, 0.18f, 0.26f, 0.72f)));
    }

    private void WireControllerFocus()
    {
        if (_rockButton is null || _paperButton is null || _scissorsButton is null)
        {
            return;
        }

        _rockButton.FocusNeighborLeft = _scissorsButton.GetPath();
        _rockButton.FocusNeighborRight = _paperButton.GetPath();
        _paperButton.FocusNeighborLeft = _rockButton.GetPath();
        _paperButton.FocusNeighborRight = _scissorsButton.GetPath();
        _scissorsButton.FocusNeighborLeft = _paperButton.GetPath();
        _scissorsButton.FocusNeighborRight = _rockButton.GetPath();
    }

    private static Button CreateChoiceButton(string text, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            Flat = true,
            FocusMode = FocusModeEnum.All,
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(96f, 40f),
            Alignment = HorizontalAlignment.Center
        };
        button.MouseDefaultCursorShape = CursorShape.PointingHand;
        button.AddThemeFontSizeOverride("font_size", 16);
        button.Pressed += onPressed;
        return button;
    }

    private static bool TryMapShortcutToMove(Key keycode, out RelicPickingFightMove move)
    {
        switch (keycode)
        {
            case Key.Key1:
            case Key.Kp1:
            case Key.R:
                move = RelicPickingFightMove.Rock;
                return true;
            case Key.Key2:
            case Key.Kp2:
            case Key.P:
                move = RelicPickingFightMove.Paper;
                return true;
            case Key.Key3:
            case Key.Kp3:
            case Key.S:
                move = RelicPickingFightMove.Scissors;
                return true;
            default:
                move = default;
                return false;
        }
    }

    private RelicPickingFightMove? GetFocusedMove()
    {
        var focused = GetViewport().GuiGetFocusOwner();
        if (focused == _rockButton)
        {
            return RelicPickingFightMove.Rock;
        }

        if (focused == _paperButton)
        {
            return RelicPickingFightMove.Paper;
        }

        if (focused == _scissorsButton)
        {
            return RelicPickingFightMove.Scissors;
        }

        return null;
    }

    private void CycleFocus(int direction)
    {
        if (_rockButton is null || _paperButton is null || _scissorsButton is null)
        {
            return;
        }

        var order = new[] { _rockButton, _paperButton, _scissorsButton };
        var focused = GetViewport().GuiGetFocusOwner();
        var index = Array.IndexOf(order, focused);
        if (index < 0)
        {
            index = 0;
        }
        else
        {
            index = (index + direction + order.Length) % order.Length;
        }

        order[index].GrabFocus();
    }

    private static StyleBoxFlat CreatePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.09f, 0.14f, 0.93f),
            BorderColor = new Color(0.78f, 0.66f, 0.37f, 0.85f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            ShadowColor = new Color(0f, 0f, 0f, 0.25f),
            ShadowSize = 8
        };
    }

    private static StyleBoxFlat CreateButtonStyle(Color backgroundColor)
    {
        return new StyleBoxFlat
        {
            BgColor = backgroundColor,
            BorderColor = new Color(0.89f, 0.85f, 0.73f, 0.7f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
    }
}
