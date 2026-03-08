using CombatQuill.Services;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace CombatQuill.UI;

public partial class CombatQuillOverlay : CanvasLayer
{
    private enum ToolMode
    {
        None,
        Draw,
        Erase
    }

    private const string DrawIconPath = "res://images/packed/map/drawing_quill.png";
    private const string DrawIconGlowPath = "res://images/packed/map/drawing_quill_glow.png";
    private const string EraseIconPath = "res://images/packed/map/drawing_eraser.png";
    private const string EraseIconGlowPath = "res://images/packed/map/drawing_eraser_glow.png";
    private const string ClearIconPath = "res://images/packed/map/drawing_clear.png";
    private const string ClearIconGlowPath = "res://images/packed/map/drawing_clear_glow.png";
    private const string QuillCursorPath = "res://images/packed/common_ui/cursor_quill.png";
    private const string QuillCursorTiltedPath = "res://images/packed/common_ui/cursor_quill_tilted.png";
    private const string EraserCursorPath = "res://images/packed/common_ui/cursor_eraser.png";
    private const string EraserCursorTiltedPath = "res://images/packed/common_ui/cursor_eraser_tilted.png";
    private const float ControllerCursorSpeed = 700f;

    private static readonly Color DrawActiveColor = new("57C4FFFF");
    private static readonly Color EraseActiveColor = new("FF5757FF");
    private static readonly Color ClearActiveColor = new("FFE57DFF");
    private static readonly Color UtilityActiveColor = new("D5E7FFFF");
    private static readonly Color InactiveColor = new("FFFFFF80");

    private readonly Dictionary<CombatQuillColorSwatchButton, int> _colorButtons = [];

    private CombatQuillSettings _settings = new();
    private CombatQuillScreenKind _screenKind;
    private CombatQuillDrawings? _drawings;
    private PanelContainer? _toolbarPanel;
    private PanelContainer? _settingsPanel;
    private CombatQuillToolButton? _drawButton;
    private CombatQuillToolButton? _eraseButton;
    private CombatQuillToolButton? _clearButton;
    private CombatQuillGlyphButton? _settingsButton;
    private Label? _colorLabel;
    private Label? _strokeWidthLabel;
    private Label? _strokeWidthValueLabel;
    private Label? _strokeOpacityLabel;
    private Label? _strokeOpacityValueLabel;
    private Label? _panelOpacityLabel;
    private Label? _panelOpacityValueLabel;
    private Label? _eraserWidthLabel;
    private Label? _eraserWidthValueLabel;
    private Control? _controllerCursor;
    private TextureRect? _controllerCursorTexture;
    private Texture2D? _controllerCursorNormalTexture;
    private Texture2D? _controllerCursorTiltedTexture;
    private Vector2 _controllerCursorIconOffset;
    private bool _controllerCursorInitialized;
    private bool _controllerPressed;
    private bool _isToolbarDragging;
    private Vector2 _toolbarDragMouseOrigin;
    private Vector2 _toolbarDragPanelOrigin;
    private bool _settingsOpen;
    private ToolMode _selectedTool;
    private ToolMode _effectiveTool;
    private ToolMode _lastSelectedTool = ToolMode.Draw;
    private MouseButton _activeMouseButton;
    private bool _isSuppressedByTargeting;

    internal void Initialize(CombatQuillSettings settings, CombatQuillScreenKind screenKind)
    {
        _settings = settings;
        _settings.Normalize();
        _screenKind = screenKind;
        _selectedTool = _settings.StartInQuillMode ? ToolMode.Draw : ToolMode.None;
        _effectiveTool = ToolMode.None;
    }

    public override void _Ready()
    {
        Layer = 120;
        SetProcess(true);
        SetProcessInput(true);

        CombatQuillI18n.Initialize();
        CreateDrawingsLayer();
        CreateControllerCursor();
        CreateToolbar();
        CreateSettingsPanel();
        SubscribeSignals();
        ApplyLocalization();
        ApplyEffectiveTool();
        CallDeferred(nameof(ApplyInitialToolbarPosition));
    }

    public override void _ExitTree()
    {
        CombatQuillI18n.Changed -= OnLocalizationChanged;
        UnsubscribeSignals();
        EndLocalLine();
        _selectedTool = ToolMode.None;
        _isSuppressedByTargeting = false;
        ApplyEffectiveTool();
    }

    public override void _Input(InputEvent @event)
    {
        if (TryHandleActionInput(@event) || TryHandleKeyInput(@event))
        {
            return;
        }

        if (_drawings is null)
        {
            return;
        }

        if (NControllerManager.Instance?.IsUsingController == true)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton mouseButtonEvent:
                if (mouseButtonEvent.Pressed &&
                    _settingsOpen &&
                    !IsInsideUi(mouseButtonEvent.GlobalPosition))
                {
                    SetSettingsOpen(false);
                }

                HandleMouseButton(mouseButtonEvent);
                break;
            case InputEventMouseMotion mouseMotionEvent:
                HandleMouseMotion(mouseMotionEvent);
                break;
        }
    }

    public override void _Process(double delta)
    {
        ProcessControllerCursor(delta);
    }

    private void CreateDrawingsLayer()
    {
        var runState = RunManager.Instance.DebugOnlyGetState();
        if (runState is not IPlayerCollection playerCollection)
        {
            return;
        }

        _drawings = new CombatQuillDrawings
        {
            Name = "CombatQuillDrawings",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _drawings.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_drawings);
        _drawings.Initialize(
            RunManager.Instance.NetService,
            playerCollection,
            RunManager.Instance.InputSynchronizer,
            _screenKind.ToNetScreenType());
    }

    private void CreateControllerCursor()
    {
        _controllerCursor = new Control
        {
            Name = "CombatQuillControllerCursor",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _controllerCursor.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _controllerCursor.Size = new Vector2(64f, 64f);
        AddChild(_controllerCursor);

        _controllerCursorTexture = new TextureRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            StretchMode = TextureRect.StretchModeEnum.Keep,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
        };
        _controllerCursorTexture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _controllerCursorTexture.Size = new Vector2(64f, 64f);
        _controllerCursorTexture.PivotOffset = _controllerCursorTexture.Size * 0.5f;
        _controllerCursor.AddChild(_controllerCursorTexture);
    }

    private void CreateToolbar()
    {
        _toolbarPanel = new PanelContainer
        {
            Name = "CombatQuillToolbar",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _toolbarPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _toolbarPanel.AddThemeStyleboxOverride("panel", CreateToolbarStyle());
        _toolbarPanel.GuiInput += OnToolbarGuiInput;
        AddChild(_toolbarPanel);

        var margin = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        _toolbarPanel.AddChild(margin);

        var row = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        row.AddThemeConstantOverride("separation", 6);
        margin.AddChild(row);

        _drawButton = CreateToolButton(DrawIconPath, DrawIconGlowPath, DrawActiveColor, CreateDrawHoverTip);
        _drawButton.Activated += ToggleDrawTool;
        row.AddChild(_drawButton);

        _eraseButton = CreateToolButton(EraseIconPath, EraseIconGlowPath, EraseActiveColor, CreateEraseHoverTip);
        _eraseButton.Activated += ToggleEraseTool;
        row.AddChild(_eraseButton);

        _clearButton = CreateToolButton(ClearIconPath, ClearIconGlowPath, ClearActiveColor, CreateClearHoverTip);
        _clearButton.Activated += ClearCanvasFromToolbar;
        row.AddChild(_clearButton);

        _settingsButton = new CombatQuillGlyphButton();
        _settingsButton.Initialize(CombatQuillGlyphButton.GlyphKind.Settings, UtilityActiveColor, InactiveColor);
        _settingsButton.Activated += ToggleSettingsPanel;
        row.AddChild(_settingsButton);

        WireToolbarFocus();
        CallDeferred(nameof(RefreshToolbarSize));
    }

    private void CreateSettingsPanel()
    {
        _settingsPanel = new PanelContainer
        {
            Name = "CombatQuillSettings",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _settingsPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _settingsPanel.CustomMinimumSize = new Vector2(220f, 0f);
        _settingsPanel.AddThemeStyleboxOverride("panel", CreateSettingsPanelStyle());
        AddChild(_settingsPanel);

        var margin = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _settingsPanel.AddChild(margin);

        var root = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        root.AddThemeConstantOverride("separation", 6);
        margin.AddChild(root);

        _colorLabel = CreateSectionLabel(string.Empty);
        root.AddChild(_colorLabel);

        var colorGrid = new GridContainer
        {
            Columns = 6,
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        colorGrid.AddThemeConstantOverride("h_separation", 4);
        colorGrid.AddThemeConstantOverride("v_separation", 4);
        root.AddChild(colorGrid);

        foreach (var color in _settings.GetPalette())
        {
            colorGrid.AddChild(CreateColorButton(color));
        }

        root.AddChild(CreateStepperRow(out _strokeWidthLabel, out _strokeWidthValueLabel, () => ChangeStrokeWidth(-1f), () => ChangeStrokeWidth(1f)));
        root.AddChild(CreateStepperRow(out _strokeOpacityLabel, out _strokeOpacityValueLabel, () => ChangeOpacity(-0.05f), () => ChangeOpacity(0.05f)));
        root.AddChild(CreateStepperRow(out _panelOpacityLabel, out _panelOpacityValueLabel, () => ChangePanelOpacity(-0.05f), () => ChangePanelOpacity(0.05f)));
        root.AddChild(CreateStepperRow(out _eraserWidthLabel, out _eraserWidthValueLabel, () => ChangeEraserWidth(-2f), () => ChangeEraserWidth(2f)));

        CallDeferred(nameof(RefreshSettingsPanelLayout));
    }

    private void SubscribeSignals()
    {
        CombatQuillI18n.Changed += OnLocalizationChanged;
        CombatQuillStyleRegistry.StyleChanged += OnStyleChanged;

        if (_screenKind == CombatQuillScreenKind.Combat && NTargetManager.Instance is not null)
        {
            NTargetManager.Instance.TargetingBegan += OnTargetingBegan;
            NTargetManager.Instance.TargetingEnded += OnTargetingEnded;
        }
    }

    private void UnsubscribeSignals()
    {
        CombatQuillStyleRegistry.StyleChanged -= OnStyleChanged;

        if (_screenKind == CombatQuillScreenKind.Combat && NTargetManager.Instance is not null)
        {
            NTargetManager.Instance.TargetingBegan -= OnTargetingBegan;
            NTargetManager.Instance.TargetingEnded -= OnTargetingEnded;
        }
    }

    private void ApplyInitialToolbarPosition()
    {
        if (_toolbarPanel is null)
        {
            return;
        }

        RefreshToolbarSize();

        if (_settings.ToolbarPositionX < 0f || _settings.ToolbarPositionY < 0f)
        {
            var viewportSize = GetViewport().GetVisibleRect().Size;
            _toolbarPanel.Position = new Vector2(
                viewportSize.X - _toolbarPanel.Size.X - 18f,
                18f);
        }
        else
        {
            _toolbarPanel.Position = new Vector2(_settings.ToolbarPositionX, _settings.ToolbarPositionY);
        }

        ClampToolbarPosition();
        RefreshSettingsPanelLayout();
    }

    private bool TryHandleActionInput(InputEvent @event)
    {
        if (@event.IsActionPressed(MegaInput.viewExhaustPileAndTabRight))
        {
            FocusToolbarForController();
            GetViewport().SetInputAsHandled();
            return true;
        }

        if ((NControllerManager.Instance?.IsUsingController ?? false) &&
            (@event.IsActionPressed(MegaInput.cancel) || @event.IsActionPressed(MegaInput.back)))
        {
            if (_settingsOpen)
            {
                SetSettingsOpen(false);
                GetViewport().SetInputAsHandled();
                return true;
            }

            if (_selectedTool != ToolMode.None)
            {
                SetSelectedTool(ToolMode.None);
                ActiveScreenContext.Instance.FocusOnDefaultControl();
                GetViewport().SetInputAsHandled();
                return true;
            }
        }

        return false;
    }

    private bool TryHandleKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return false;
        }

        if (MatchesKey(keyEvent, Key.Escape))
        {
            if (_settingsOpen)
            {
                SetSettingsOpen(false);
                return true;
            }

            if (_selectedTool != ToolMode.None)
            {
                SetSelectedTool(ToolMode.None);
                return true;
            }
        }

        if (MatchesKey(keyEvent, _settings.GetToggleKey()))
        {
            TogglePrimaryTool();
            return true;
        }

        if (_selectedTool == ToolMode.None)
        {
            return false;
        }

        if (MatchesKey(keyEvent, _settings.GetClearKey()))
        {
            ClearCanvas();
            return true;
        }

        if (MatchesKey(keyEvent, _settings.GetEraserKey()))
        {
            SetSelectedTool(_selectedTool == ToolMode.Erase ? ToolMode.Draw : ToolMode.Erase);
            return true;
        }

        return false;
    }

    private void HandleMouseButton(InputEventMouseButton mouseButtonEvent)
    {
        if (_drawings is null || mouseButtonEvent.ButtonIndex is not (MouseButton.Left or MouseButton.Right))
        {
            return;
        }

        if (mouseButtonEvent.Pressed && IsInsideUi(mouseButtonEvent.GlobalPosition))
        {
            return;
        }

        if (!mouseButtonEvent.Pressed)
        {
            if (_activeMouseButton == mouseButtonEvent.ButtonIndex && _drawings.IsLocalDrawing())
            {
                EndLocalLine();
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (!TryGetDrawingRequest(mouseButtonEvent.ButtonIndex, out var overrideDrawingMode))
        {
            return;
        }

        BeginLocalLine(mouseButtonEvent.GlobalPosition, overrideDrawingMode);
        if (_drawings.IsLocalDrawing())
        {
            _activeMouseButton = mouseButtonEvent.ButtonIndex;
            GetViewport().SetInputAsHandled();
        }
    }

    private void HandleMouseMotion(InputEventMouseMotion mouseMotionEvent)
    {
        if (_drawings is null || !_drawings.IsLocalDrawing() || _activeMouseButton == MouseButton.None)
        {
            return;
        }

        if (!mouseMotionEvent.ButtonMask.HasFlag(ToMouseButtonMask(_activeMouseButton)))
        {
            EndLocalLine();
            return;
        }

        ContinueLocalLine(mouseMotionEvent.GlobalPosition);
        GetViewport().SetInputAsHandled();
    }

    private void ProcessControllerCursor(double delta)
    {
        if (_controllerCursor is null || _controllerCursorTexture is null || _drawings is null)
        {
            return;
        }

        var usingController = NControllerManager.Instance?.IsUsingController ?? false;
        var canUseControllerCursor = usingController && _effectiveTool != ToolMode.None && !IsUiFocused();
        if (!canUseControllerCursor)
        {
            StopControllerDrawing();
            _controllerCursor.Visible = false;
            return;
        }

        _controllerCursor.Visible = true;
        if (!_controllerCursorInitialized)
        {
            _controllerCursor.Position = GetViewport().GetVisibleRect().Size * 0.5f;
            _controllerCursorInitialized = true;
        }

        var direction = Input.GetVector(Controller.joystickLeft, Controller.joystickRight, Controller.joystickUp, Controller.joystickDown);
        if (direction.Length() < 0.1f)
        {
            direction = Input.GetVector(Controller.dPadWest, Controller.dPadEast, Controller.dPadNorth, Controller.dPadSouth);
        }

        if (direction.Length() > 0f)
        {
            _controllerCursor.Position += direction * ControllerCursorSpeed * (float)delta;
            ClampControllerCursor();
        }

        var selectPressed = Input.IsActionPressed(MegaInput.select);
        if (selectPressed && !_controllerPressed)
        {
            BeginLocalLine(_controllerCursor.GlobalPosition);
            _controllerPressed = true;
            UpdateControllerCursorVisual(pressed: true);
        }
        else if (!selectPressed && _controllerPressed)
        {
            EndLocalLine();
            _controllerPressed = false;
            UpdateControllerCursorVisual(pressed: false);
        }

        if (_drawings.IsLocalDrawing())
        {
            ContinueLocalLine(_controllerCursor.GlobalPosition);
        }
    }

    private void StopControllerDrawing()
    {
        if (_controllerPressed)
        {
            EndLocalLine();
            _controllerPressed = false;
        }

        UpdateControllerCursorVisual(pressed: false);
    }

    private void TogglePrimaryTool()
    {
        if (_selectedTool == ToolMode.None)
        {
            SetSelectedTool(_lastSelectedTool == ToolMode.None ? ToolMode.Draw : _lastSelectedTool);
        }
        else
        {
            SetSelectedTool(ToolMode.None);
        }
    }

    private void ToggleDrawTool()
    {
        SetSelectedTool(_selectedTool == ToolMode.Draw ? ToolMode.None : ToolMode.Draw);
    }

    private void ToggleEraseTool()
    {
        SetSelectedTool(_selectedTool == ToolMode.Erase ? ToolMode.None : ToolMode.Erase);
    }

    private void ToggleSettingsPanel()
    {
        SetSettingsOpen(!_settingsOpen);
    }

    private void ClearCanvasFromToolbar()
    {
        ClearCanvas();
        if (NControllerManager.Instance?.IsUsingController ?? false)
        {
            GetViewport().GuiReleaseFocus();
        }
    }

    private void SetSelectedTool(ToolMode toolMode)
    {
        _selectedTool = toolMode;
        if (toolMode is ToolMode.Draw or ToolMode.Erase)
        {
            _lastSelectedTool = toolMode;
        }

        ApplyEffectiveTool();

        if (NControllerManager.Instance?.IsUsingController ?? false)
        {
            GetViewport().GuiReleaseFocus();
        }
    }

    private void SetSettingsOpen(bool open)
    {
        _settingsOpen = open;
        UpdateToolStateUi();
    }

    private void ApplyEffectiveTool()
    {
        if (_drawings is null)
        {
            return;
        }

        var nextTool = _isSuppressedByTargeting ? ToolMode.None : _selectedTool;
        if (_effectiveTool != nextTool && _drawings.IsLocalDrawing())
        {
            _drawings.StopLineLocal();
        }

        _effectiveTool = nextTool;
        var targetMode = ToDrawingMode(_effectiveTool);
        if (_drawings.GetLocalDrawingMode(useOverride: false) != targetMode)
        {
            _drawings.SetDrawingModeLocal(targetMode);
        }

        if (_effectiveTool == ToolMode.None && _activeMouseButton == MouseButton.Left)
        {
            _activeMouseButton = MouseButton.None;
        }

        UpdateControllerCursorVisual(pressed: _controllerPressed);
        UpdateToolStateUi();
    }

    private void UpdateToolStateUi()
    {
        _drawButton?.SetActiveState(_selectedTool == ToolMode.Draw);
        _eraseButton?.SetActiveState(_selectedTool == ToolMode.Erase);
        _clearButton?.SetActiveState(false);
        _settingsButton?.SetActiveState(_settingsOpen);

        if (_toolbarPanel is not null)
        {
            _toolbarPanel.SelfModulate = new Color(
                1f,
                1f,
                1f,
                _isSuppressedByTargeting ? Mathf.Clamp(_settings.PanelOpacity * 0.75f, 0.2f, 1f) : _settings.PanelOpacity);
        }

        if (_settingsPanel is not null)
        {
            _settingsPanel.Visible = _settingsOpen;
            _settingsPanel.SelfModulate = new Color(
                1f,
                1f,
                1f,
                _isSuppressedByTargeting ? Mathf.Clamp(_settings.PanelOpacity * 0.75f, 0.2f, 1f) : _settings.PanelOpacity);
        }

        UpdateColorButtons();
        UpdateValueLabels();
        ApplyLocalization();
        CallDeferred(nameof(RefreshToolbarSize));
        CallDeferred(nameof(RefreshSettingsPanelLayout));
    }

    private void UpdateColorButtons()
    {
        var palette = _settings.GetPalette();
        foreach (var pair in _colorButtons)
        {
            var index = pair.Value;
            if (index >= palette.Length)
            {
                continue;
            }

            var normalized = palette[index].TrimStart('#');
            var color = Color.FromHtml($"#{normalized}");
            var isSelected = index == _settings.SelectedColorIndex;
            pair.Key.SetSwatch(color, isSelected);
            pair.Key.TooltipText = string.Format(
                CombatQuillI18n.Get("tooltip.color", "Color {0}"),
                $"#{normalized}");
        }
    }

    private void UpdateValueLabels()
    {
        if (_strokeWidthValueLabel is not null)
        {
            _strokeWidthValueLabel.Text = $"{_settings.GetStrokeWidth():0}";
        }

        if (_strokeOpacityValueLabel is not null)
        {
            _strokeOpacityValueLabel.Text = $"{Mathf.RoundToInt(_settings.StrokeOpacity * 100f)}%";
        }

        if (_panelOpacityValueLabel is not null)
        {
            _panelOpacityValueLabel.Text = $"{Mathf.RoundToInt(_settings.PanelOpacity * 100f)}%";
        }

        if (_eraserWidthValueLabel is not null)
        {
            _eraserWidthValueLabel.Text = $"{_settings.GetEraserWidth():0}";
        }
    }

    private void ApplyLocalization()
    {
        if (_colorLabel is not null)
        {
            _colorLabel.Text = CombatQuillI18n.Get("settings.color", "Color");
        }

        if (_strokeWidthLabel is not null)
        {
            _strokeWidthLabel.Text = CombatQuillI18n.Get("settings.stroke_width", "Width");
        }

        if (_strokeOpacityLabel is not null)
        {
            _strokeOpacityLabel.Text = CombatQuillI18n.Get("settings.stroke_opacity", "Ink");
        }

        if (_panelOpacityLabel is not null)
        {
            _panelOpacityLabel.Text = CombatQuillI18n.Get("settings.panel_opacity", "Panel");
        }

        if (_eraserWidthLabel is not null)
        {
            _eraserWidthLabel.Text = CombatQuillI18n.Get("settings.eraser_width", "Erase");
        }

        if (_settingsButton is not null)
        {
            _settingsButton.TooltipText = CombatQuillI18n.Get("tooltip.settings", "Open brush settings");
        }
    }

    private void RefreshToolbarSize()
    {
        if (_toolbarPanel is null)
        {
            return;
        }

        _toolbarPanel.Size = _toolbarPanel.GetCombinedMinimumSize();
        ClampToolbarPosition();
    }

    private void RefreshSettingsPanelLayout()
    {
        if (_settingsPanel is null)
        {
            return;
        }

        _settingsPanel.Size = _settingsPanel.GetCombinedMinimumSize();
        UpdateSettingsPanelPlacement();
    }

    private void UpdateSettingsPanelPlacement()
    {
        if (_settingsPanel is null || _toolbarPanel is null || !_settingsOpen)
        {
            return;
        }

        var viewportSize = GetViewport().GetVisibleRect().Size;
        var desiredPosition = _toolbarPanel.Position + new Vector2(0f, _toolbarPanel.Size.Y + 6f);
        if (desiredPosition.Y + _settingsPanel.Size.Y > viewportSize.Y - 8f)
        {
            desiredPosition.Y = _toolbarPanel.Position.Y - _settingsPanel.Size.Y - 6f;
        }

        desiredPosition.X = Mathf.Clamp(desiredPosition.X, 8f, Math.Max(8f, viewportSize.X - _settingsPanel.Size.X - 8f));
        desiredPosition.Y = Mathf.Clamp(desiredPosition.Y, 8f, Math.Max(8f, viewportSize.Y - _settingsPanel.Size.Y - 8f));
        _settingsPanel.Position = desiredPosition;
    }

    private void SelectColor(CombatQuillColorSwatchButton button)
    {
        if (!_colorButtons.TryGetValue(button, out var index))
        {
            return;
        }

        var palette = _settings.GetPalette();
        if (index < 0 || index >= palette.Length)
        {
            return;
        }

        _settings.SelectedColorIndex = index;
        _settings.StrokeColorHtml = palette[index];
        PersistStyleSettings();
    }

    private void ChangeStrokeWidth(float delta)
    {
        _settings.StrokeWidth = Mathf.Clamp(_settings.StrokeWidth + delta, 2f, 20f);
        PersistStyleSettings();
    }

    private void ChangeOpacity(float delta)
    {
        _settings.StrokeOpacity = Mathf.Clamp(_settings.StrokeOpacity + delta, 0.15f, 1f);
        PersistStyleSettings();
    }

    private void ChangePanelOpacity(float delta)
    {
        _settings.PanelOpacity = Mathf.Clamp(_settings.PanelOpacity + delta, 0.25f, 1f);
        CombatQuillService.PersistSettings(_settings, broadcastStyle: false);
        UpdateToolStateUi();
    }

    private void ChangeEraserWidth(float delta)
    {
        _settings.EraserWidth = Mathf.Clamp(_settings.GetEraserWidth() + delta, 8f, 72f);
        PersistStyleSettings();
    }

    private void PersistStyleSettings()
    {
        _settings.Normalize();
        CombatQuillService.PersistSettings(_settings);
        _drawings?.RefreshAllLineStyles();
        UpdateToolStateUi();
    }

    private void ClearCanvas()
    {
        _drawings?.ClearDrawnLinesLocal();
    }

    private void BeginLocalLine(Vector2 globalPosition, DrawingMode? overrideDrawingMode = null)
    {
        if (_drawings is null)
        {
            return;
        }

        if (overrideDrawingMode is null && _effectiveTool == ToolMode.None)
        {
            return;
        }

        _drawings.BeginLineLocal(ToDrawingPosition(globalPosition), overrideDrawingMode);
    }

    private void ContinueLocalLine(Vector2 globalPosition)
    {
        if (_drawings is null || !_drawings.IsLocalDrawing())
        {
            return;
        }

        _drawings.UpdateCurrentLinePositionLocal(ToDrawingPosition(globalPosition));
    }

    private void EndLocalLine()
    {
        if (_drawings?.IsLocalDrawing() == true)
        {
            _drawings.StopLineLocal();
        }

        _activeMouseButton = MouseButton.None;
    }

    private Vector2 ToDrawingPosition(Vector2 globalPosition)
    {
        return _drawings!.GetGlobalTransform().Inverse() * globalPosition;
    }

    private void OnTargetingBegan()
    {
        if (_selectedTool == ToolMode.None)
        {
            return;
        }

        _isSuppressedByTargeting = true;
        ApplyEffectiveTool();
    }

    private void OnTargetingEnded()
    {
        if (!_isSuppressedByTargeting)
        {
            return;
        }

        _isSuppressedByTargeting = false;
        ApplyEffectiveTool();
    }

    private void OnStyleChanged(ulong _)
    {
        _drawings?.RefreshAllLineStyles();
        UpdateColorButtons();
    }

    private void OnLocalizationChanged()
    {
        ApplyLocalization();
    }

    private bool TryGetDrawingRequest(MouseButton button, out DrawingMode? overrideDrawingMode)
    {
        overrideDrawingMode = null;
        if (_isSuppressedByTargeting)
        {
            return false;
        }

        if (button == MouseButton.Left && _effectiveTool != ToolMode.None)
        {
            return true;
        }

        if (button == MouseButton.Right && _selectedTool == ToolMode.None)
        {
            overrideDrawingMode = DrawingMode.Drawing;
            return true;
        }

        return false;
    }

    private void FocusToolbarForController()
    {
        if (_selectedTool == ToolMode.Erase)
        {
            _eraseButton?.GrabFocus();
            return;
        }

        if (_selectedTool == ToolMode.Draw)
        {
            _drawButton?.GrabFocus();
            return;
        }

        _settingsButton?.GrabFocus();
    }

    private void WireToolbarFocus()
    {
        var buttons = new Control?[] { _drawButton, _eraseButton, _clearButton, _settingsButton }
            .Where(static button => button is not null)
            .Cast<Control>()
            .ToArray();

        for (var index = 0; index < buttons.Length; index++)
        {
            var current = buttons[index];
            var left = buttons[Math.Max(index - 1, 0)];
            var right = buttons[Math.Min(index + 1, buttons.Length - 1)];
            current.FocusNeighborLeft = left.GetPath();
            current.FocusNeighborRight = right.GetPath();
        }
    }

    private void OnToolbarGuiInput(InputEvent @event)
    {
        if (_toolbarPanel is null)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton mouseButtonEvent when mouseButtonEvent.ButtonIndex == MouseButton.Left:
                if (mouseButtonEvent.Pressed)
                {
                    if (IsOverToolbarButton(mouseButtonEvent.GlobalPosition))
                    {
                        return;
                    }

                    _isToolbarDragging = true;
                    _toolbarDragMouseOrigin = mouseButtonEvent.GlobalPosition;
                    _toolbarDragPanelOrigin = _toolbarPanel.Position;
                }
                else if (_isToolbarDragging)
                {
                    _isToolbarDragging = false;
                    SaveToolbarPosition();
                }

                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseMotion mouseMotionEvent when _isToolbarDragging:
                _toolbarPanel.Position = _toolbarDragPanelOrigin + (mouseMotionEvent.GlobalPosition - _toolbarDragMouseOrigin);
                ClampToolbarPosition();
                UpdateSettingsPanelPlacement();
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private void ClampToolbarPosition()
    {
        if (_toolbarPanel is null)
        {
            return;
        }

        var viewportSize = GetViewport().GetVisibleRect().Size;
        var clampedX = Mathf.Clamp(_toolbarPanel.Position.X, 8f, Math.Max(8f, viewportSize.X - _toolbarPanel.Size.X - 8f));
        var clampedY = Mathf.Clamp(_toolbarPanel.Position.Y, 8f, Math.Max(8f, viewportSize.Y - _toolbarPanel.Size.Y - 8f));
        _toolbarPanel.Position = new Vector2(clampedX, clampedY);
    }

    private void SaveToolbarPosition()
    {
        if (_toolbarPanel is null)
        {
            return;
        }

        _settings.ToolbarPositionX = _toolbarPanel.Position.X;
        _settings.ToolbarPositionY = _toolbarPanel.Position.Y;
        CombatQuillService.PersistSettings(_settings, broadcastStyle: false);
    }

    private bool IsOverToolbarButton(Vector2 globalPosition)
    {
        return (_drawButton?.GetGlobalRect().HasPoint(globalPosition) ?? false) ||
               (_eraseButton?.GetGlobalRect().HasPoint(globalPosition) ?? false) ||
               (_clearButton?.GetGlobalRect().HasPoint(globalPosition) ?? false) ||
               (_settingsButton?.GetGlobalRect().HasPoint(globalPosition) ?? false);
    }

    private bool IsInsideUi(Vector2 globalPosition)
    {
        if (_toolbarPanel?.GetGlobalRect().HasPoint(globalPosition) == true)
        {
            return true;
        }

        return _settingsPanel is not null &&
               _settingsOpen &&
               _settingsPanel.GetGlobalRect().HasPoint(globalPosition);
    }

    private bool IsUiFocused()
    {
        var focusOwner = GetViewport().GuiGetFocusOwner();
        if (focusOwner is null)
        {
            return false;
        }

        return (_toolbarPanel is not null && _toolbarPanel.IsAncestorOf(focusOwner)) ||
               (_settingsPanel is not null && _settingsPanel.IsAncestorOf(focusOwner));
    }

    private void ClampControllerCursor()
    {
        if (_controllerCursor is null)
        {
            return;
        }

        var viewportSize = GetViewport().GetVisibleRect().Size;
        _controllerCursor.Position = new Vector2(
            Mathf.Clamp(_controllerCursor.Position.X, 0f, viewportSize.X),
            Mathf.Clamp(_controllerCursor.Position.Y, 0f, viewportSize.Y));
    }

    private void UpdateControllerCursorVisual(bool pressed)
    {
        if (_controllerCursorTexture is null)
        {
            return;
        }

        if (_effectiveTool == ToolMode.None)
        {
            _controllerCursorTexture.Texture = null;
            return;
        }

        var useEraser = _effectiveTool == ToolMode.Erase;
        _controllerCursorNormalTexture = ImageTexture.CreateFromImage(
            PreloadManager.Cache.GetAsset<Image>(useEraser ? EraserCursorPath : QuillCursorPath));
        _controllerCursorTiltedTexture = ImageTexture.CreateFromImage(
            PreloadManager.Cache.GetAsset<Image>(useEraser ? EraserCursorTiltedPath : QuillCursorTiltedPath));
        _controllerCursorIconOffset = useEraser ? new Vector2(-34f, -76f) : new Vector2(-10f, -76f);

        _controllerCursorTexture.Texture = pressed ? _controllerCursorTiltedTexture : _controllerCursorNormalTexture;
        _controllerCursorTexture.Position = _controllerCursorIconOffset;
    }

    private static CombatQuillToolButton CreateToolButton(
        string iconPath,
        string glowPath,
        Color activeColor,
        Func<HoverTip> hoverTipFactory)
    {
        var button = new CombatQuillToolButton();
        button.Initialize(iconPath, glowPath, activeColor, InactiveColor, hoverTipFactory);
        return button;
    }

    private CombatQuillColorSwatchButton CreateColorButton(string colorHtml)
    {
        var normalized = colorHtml.TrimStart('#');
        var color = Color.FromHtml($"#{normalized}");
        var button = new CombatQuillColorSwatchButton();
        button.SetSwatch(color, selected: false);
        button.Pressed += () => SelectColor(button);
        _colorButtons[button] = _colorButtons.Count;
        return button;
    }

    private static HBoxContainer CreateStepperRow(
        out Label label,
        out Label valueLabel,
        Action onMinusPressed,
        Action onPlusPressed)
    {
        var row = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        row.AddThemeConstantOverride("separation", 6);

        label = new Label
        {
            CustomMinimumSize = new Vector2(52f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        row.AddChild(label);

        row.AddChild(CreateMiniButton("-", onMinusPressed));

        valueLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(48f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        valueLabel.AddThemeFontSizeOverride("font_size", 11);
        row.AddChild(valueLabel);

        row.AddChild(CreateMiniButton("+", onPlusPressed));
        return row;
    }

    private static Button CreateMiniButton(string text, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            Flat = true,
            FocusMode = Control.FocusModeEnum.All,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(24f, 24f)
        };
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        button.AddThemeStyleboxOverride("normal", CreateMiniButtonStyle(new Color(0.14f, 0.19f, 0.26f, 0.95f)));
        button.AddThemeStyleboxOverride("hover", CreateMiniButtonStyle(new Color(0.19f, 0.26f, 0.36f, 0.98f)));
        button.AddThemeStyleboxOverride("pressed", CreateMiniButtonStyle(new Color(0.24f, 0.32f, 0.44f, 1f)));
        button.AddThemeFontSizeOverride("font_size", 12);
        button.Pressed += () => onPressed.Invoke();
        return button;
    }

    private static Label CreateSectionLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", new Color("D4E4F5"));
        return label;
    }

    private static StyleBoxFlat CreateToolbarStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.11f, 0.16f, 0.9f),
            BorderColor = new Color(0.42f, 0.52f, 0.64f, 0.74f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ShadowColor = new Color(0f, 0f, 0f, 0.28f),
            ShadowSize = 6
        };
    }

    private static StyleBoxFlat CreateSettingsPanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.11f, 0.16f, 0.92f),
            BorderColor = new Color(0.42f, 0.52f, 0.64f, 0.74f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ShadowColor = new Color(0f, 0f, 0f, 0.28f),
            ShadowSize = 6
        };
    }

    private static StyleBoxFlat CreateMiniButtonStyle(Color backgroundColor)
    {
        return new StyleBoxFlat
        {
            BgColor = backgroundColor,
            BorderColor = new Color(0.48f, 0.6f, 0.72f, 0.75f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
    }

    private static HoverTip CreateDrawHoverTip()
    {
        return new HoverTip(
            new LocString("map", "DRAWING_BUTTON.title_mkb"),
            new LocString("map", "DRAWING_BUTTON.description"));
    }

    private static HoverTip CreateEraseHoverTip()
    {
        return new HoverTip(
            new LocString("map", "ERASING_BUTTON.title_mkb"),
            new LocString("map", "ERASING_BUTTON.description"));
    }

    private static HoverTip CreateClearHoverTip()
    {
        return new HoverTip(
            new LocString("map", "CLEAR_DRAWING.title"),
            new LocString("map", "CLEAR_DRAWING.description"));
    }

    private static MouseButtonMask ToMouseButtonMask(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => MouseButtonMask.Left,
            MouseButton.Right => MouseButtonMask.Right,
            MouseButton.Middle => MouseButtonMask.Middle,
            MouseButton.Xbutton1 => MouseButtonMask.MbXbutton1,
            MouseButton.Xbutton2 => MouseButtonMask.MbXbutton2,
            _ => 0
        };
    }

    private static DrawingMode ToDrawingMode(ToolMode mode)
    {
        return mode switch
        {
            ToolMode.Draw => DrawingMode.Drawing,
            ToolMode.Erase => DrawingMode.Erasing,
            _ => DrawingMode.None
        };
    }

    private static bool MatchesKey(InputEventKey keyEvent, Key expectedKey)
    {
        return keyEvent.Keycode == expectedKey || keyEvent.PhysicalKeycode == expectedKey;
    }
}
