using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DamageMeter.Scripts.Categories;
using Godot;
using CursorShape = Godot.Control.CursorShape;
using ExpandModeEnum = Godot.TextureRect.ExpandModeEnum;
using GrowDirection = Godot.Control.GrowDirection;
using InternalMode = Godot.Node.InternalMode;
using LayoutPreset = Godot.Control.LayoutPreset;
using MouseFilterEnum = Godot.Control.MouseFilterEnum;
using ScrollMode = Godot.ScrollContainer.ScrollMode;
using SizeFlags = Godot.Control.SizeFlags;
using StretchModeEnum = Godot.TextureRect.StretchModeEnum;

namespace DamageMeter.Scripts;

public static class DamageMeterUI
{
	private static readonly Color[] DetailColors = (Color[])(object)new Color[8]
	{
		new Color(0.95f, 0.45f, 0.35f, 1f),
		new Color(0.95f, 0.7f, 0.2f, 1f),
		new Color(0.3f, 0.75f, 0.95f, 1f),
		new Color(0.6f, 0.85f, 0.35f, 1f),
		new Color(0.85f, 0.5f, 0.85f, 1f),
		new Color(0.45f, 0.8f, 0.75f, 1f),
		new Color(0.95f, 0.6f, 0.45f, 1f),
		new Color(0.6f, 0.6f, 0.95f, 1f)
	};

	private static readonly Color GoldColor = new Color(1f, 0.84f, 0f, 1f);

	private static readonly Color GoldBorder = new Color(1f, 0.84f, 0f, 0.3f);

	private static readonly Color TextColor = new Color(0.85f, 0.85f, 0.85f, 1f);

	private static readonly Color DimText = new Color(0.5f, 0.5f, 0.5f, 1f);

	private static readonly Color SegmentTextColor = new Color(0.7f, 0.7f, 0.8f, 1f);

	private static readonly Color BarBgColor = new Color(0.06f, 0.06f, 0.12f, 0.95f);

	private static readonly Dictionary<string, string> CharacterIconMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["IRONCLAD"] = "\u94c1\u7532\u6218\u58eb",
		["SILENT"] = "\u9759\u9ed8\u730e\u624b",
		["DEFECT"] = "\u6545\u969c\u673a\u5668\u4eba",
		["NECROBINDER"] = "\u4ea1\u7075\u5951\u7ea6\u5e08",
		["REGENT"] = "\u50a8\u541b"
	};

	private static readonly Dictionary<string, Texture2D?> _iconCache = new Dictionary<string, Texture2D?>();

	private static readonly List<IStatCategory> _categories = new List<IStatCategory>();

	private static int _categoryIndex;

	private static string? _detailPlayerKey;

	private static readonly List<string> _playerColorOrder = new List<string>();

	private static CanvasLayer? _canvas;

	private static PanelContainer? _panel;

	private static Button? _titleBtn;

	private static Button? _settingsBtn;

	private static Button? _resetBtn;

	private static Button? _leftBtn;

	private static Button? _rightBtn;

	private static HBoxContainer? _segmentRow;

	private static Button? _segmentLabel;

	private static Label? _summaryLabel;

	private static Label? _metaLabel;

	private static VBoxContainer? _contentBox;

	private static InputHandler? _inputHandler;

	private static ScrollContainer? _scrollContainer;

	private static Button? _dashboardBtn;

	private static Button? _opacityBtn;

	private static CanvasLayer? _dashboardCanvas;

	private static bool _dashboardVisible;

	private static readonly Dictionary<int, string?> _dashboardDetailState = new Dictionary<int, string?>();

	private static PopupPanel? _categoryPopup;

	private static GridContainer? _categoryGrid;

	private static PopupMenu? _segmentPopup;

	private static PopupMenu? _settingsPopup;

	private static PopupMenu? _scaleSubmenu;

	private static PopupMenu? _opacitySubmenu;

	private static PopupMenu? _maxBarsSubmenu;

	private static StyleBoxFlat? _panelStyle;

	private static StyleBoxFlat? _summaryStyle;

	private static ConfirmationDialog? _resetConfirmDialog;

	private static bool _isDragging;

	private static Vector2 _dragOffset;

	private static bool _anchored = true;

	private static readonly float[] ScaleOptions = new float[4] { 0.8f, 1f, 1.2f, 1.5f };

	private static readonly float[] OpacityOptions = new float[6] { 0.35f, 0.5f, 0.65f, 0.8f, 0.88f, 1f };

	private static readonly int[] MaxBarsOptions = new int[5] { 5, 8, 10, 15, 20 };

	// Keep control labels centralized so future UI restyling only changes one place.
	private const string PreviousButtonText = "<";

	private const string NextButtonText = ">";

	private const string BackButtonPrefix = "< ";

	private const string CloseButtonText = "X";

	private const string DashboardTitleSeparator = " - ";

	public static bool IsDashboardVisible => _dashboardVisible;

	public static void Initialize()
	{
		_categories.Clear();
		_categories.Add(new DamageDealtCategory());
		_categories.Add(new RealDamageCategory());
		_categories.Add(new DamageTakenCategory());
		_categories.Add(new AssistDamageCategory());
		_categories.Add(new AssistBlockCategory());
		_categories.Add(new DptCategory());
		_categories.Add(new CardUsageCategory());
		_categories.Add(new BlockCategory());
		_categories.Add(new EnergyCategory());
		_categories.Add(new CardEfficiencyCategory());
		_categories.Add(new OverkillCategory());
		_categories.Add(new PotionCategory());
		_categories.Add(new DebuffsCategory());
		_categories.Add(new CardFlowCategory());
		_categories.Add(new DeathLogCategory());
		_categories.Add(new CombatLogCategory());
		_categories.Add(new RecordsCategory());
	}

	public static void CreateAndAttach()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Expected O, but got Unknown
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Expected O, but got Unknown
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Expected O, but got Unknown
		//IL_023e: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_033f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Expected O, but got Unknown
		//IL_036f: Unknown result type (might be due to invalid IL or missing references)
		//IL_039c: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_040f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0414: Unknown result type (might be due to invalid IL or missing references)
		//IL_041a: Expected O, but got Unknown
		//IL_0485: Unknown result type (might be due to invalid IL or missing references)
		//IL_048b: Expected O, but got Unknown
		//IL_048b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0490: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b0: Expected O, but got Unknown
		//IL_04e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ee: Expected O, but got Unknown
		//IL_0516: Unknown result type (might be due to invalid IL or missing references)
		//IL_0558: Unknown result type (might be due to invalid IL or missing references)
		//IL_0562: Expected O, but got Unknown
		//IL_0588: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_0638: Unknown result type (might be due to invalid IL or missing references)
		//IL_060f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0614: Unknown result type (might be due to invalid IL or missing references)
		//IL_061a: Expected O, but got Unknown
		//IL_0688: Unknown result type (might be due to invalid IL or missing references)
		//IL_0692: Expected O, but got Unknown
		//IL_06b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0701: Expected O, but got Unknown
		//IL_0783: Unknown result type (might be due to invalid IL or missing references)
		//IL_0788: Unknown result type (might be due to invalid IL or missing references)
		//IL_078e: Expected O, but got Unknown
		//IL_07d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_07dd: Expected O, but got Unknown
		//IL_07f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_07fa: Expected O, but got Unknown
		//IL_0810: Unknown result type (might be due to invalid IL or missing references)
		//IL_0848: Unknown result type (might be due to invalid IL or missing references)
		//IL_08c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_08ce: Expected O, but got Unknown
		//IL_0934: Unknown result type (might be due to invalid IL or missing references)
		//IL_0939: Unknown result type (might be due to invalid IL or missing references)
		//IL_093f: Expected O, but got Unknown
		//IL_0987: Unknown result type (might be due to invalid IL or missing references)
		//IL_098c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0992: Expected O, but got Unknown
		//IL_09ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_09cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_09d5: Expected O, but got Unknown
		//IL_0a1f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a24: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a2a: Expected O, but got Unknown
		//IL_0aa8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ab2: Expected O, but got Unknown
		//IL_0a74: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a79: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a7f: Expected O, but got Unknown
		//IL_0b2a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b4a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b4f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b54: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b7f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b84: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b89: Unknown result type (might be due to invalid IL or missing references)
		if (_canvas == null)
		{
			_categoryIndex = 0;
			_detailPlayerKey = null;
			_playerColorOrder.Clear();
			_canvas = new CanvasLayer
			{
				Layer = 100,
				Name = "DamageMeterUI"
			};
			_panel = new PanelContainer();
			((Control)_panel).CustomMinimumSize = new Vector2(340f, 0f);
			if (DamageMeterSettings.HasSavedPosition)
			{
				((Control)_panel).Position = new Vector2(DamageMeterSettings.PanelX, DamageMeterSettings.PanelY);
				_anchored = false;
			}
			else
			{
				((Control)_panel).AnchorLeft = 1f;
				((Control)_panel).AnchorRight = 1f;
				((Control)_panel).GrowHorizontal = (GrowDirection)0;
				((Control)_panel).GrowVertical = (GrowDirection)1;
				((Control)_panel).OffsetLeft = -10f;
				((Control)_panel).OffsetTop = 10f;
			}
			_panelStyle = new StyleBoxFlat();
			_panelStyle.BgColor = GetHudPanelColor();
			StyleBoxFlat? panelStyle = _panelStyle;
			StyleBoxFlat? panelStyle2 = _panelStyle;
			StyleBoxFlat? panelStyle3 = _panelStyle;
			int num2 = (_panelStyle.BorderWidthRight = 1);
			int num4 = (panelStyle3.BorderWidthLeft = num2);
			int borderWidthBottom = (panelStyle2.BorderWidthTop = num4);
			panelStyle.BorderWidthBottom = borderWidthBottom;
			_panelStyle.BorderColor = GoldBorder;
			StyleBoxFlat? panelStyle4 = _panelStyle;
			StyleBoxFlat? panelStyle5 = _panelStyle;
			StyleBoxFlat? panelStyle6 = _panelStyle;
			num2 = (_panelStyle.CornerRadiusBottomRight = 8);
			num4 = (panelStyle6.CornerRadiusBottomLeft = num2);
			borderWidthBottom = (panelStyle5.CornerRadiusTopRight = num4);
			panelStyle4.CornerRadiusTopLeft = borderWidthBottom;
			StyleBoxFlat? panelStyle7 = _panelStyle;
			float contentMarginLeft = (((StyleBox)_panelStyle).ContentMarginRight = 10f);
			((StyleBox)panelStyle7).ContentMarginLeft = contentMarginLeft;
			StyleBoxFlat? panelStyle8 = _panelStyle;
			contentMarginLeft = (((StyleBox)_panelStyle).ContentMarginBottom = 8f);
			((StyleBox)panelStyle8).ContentMarginTop = contentMarginLeft;
			((Control)_panel).AddThemeStyleboxOverride("panel", (StyleBox)(object)_panelStyle);
			((Control)_panel).Scale = new Vector2(DamageMeterSettings.Scale, DamageMeterSettings.Scale);
			VBoxContainer val = new VBoxContainer();
			((Control)val).AddThemeConstantOverride("separation", 6);
			((Control)val).MouseFilter = (MouseFilterEnum)2;
			HBoxContainer val2 = new HBoxContainer();
			((Control)val2).AddThemeConstantOverride("separation", 4);
			((Control)val2).MouseFilter = (MouseFilterEnum)2;
			_leftBtn = CreateNavButton(PreviousButtonText, 13, GoldColor, 28f);
			((BaseButton)_leftBtn).Pressed += OnLeftPressed;
			((Node)val2).AddChild((Node)(object)_leftBtn, false, (InternalMode)0);
			_titleBtn = new Button();
			_titleBtn.Flat = true;
			((Control)_titleBtn).SizeFlagsHorizontal = (SizeFlags)3;
			((Control)_titleBtn).AddThemeColorOverride("font_color", GoldColor);
			((Control)_titleBtn).AddThemeColorOverride("font_hover_color", new Color(1f, 0.95f, 0.5f, 1f));
			((Control)_titleBtn).AddThemeColorOverride("font_pressed_color", GoldColor);
			((Control)_titleBtn).AddThemeFontSizeOverride("font_size", 16);
			((Control)_titleBtn).MouseDefaultCursorShape = (CursorShape)2;
			((BaseButton)_titleBtn).Pressed += OnTitlePressed;
			((Control)_titleBtn).GuiInput += OnTitleGuiInput;
			((Node)val2).AddChild((Node)(object)_titleBtn, false, (InternalMode)0);
			_rightBtn = CreateNavButton(NextButtonText, 13, GoldColor, 28f);
			((BaseButton)_rightBtn).Pressed += OnRightPressed;
			((Node)val2).AddChild((Node)(object)_rightBtn, false, (InternalMode)0);
			_settingsBtn = CreateNavButton(I18n.Settings, 11, DimText, 72f);
			((BaseButton)_settingsBtn).Pressed += OnSettingsPressed;
			((Control)_settingsBtn).TooltipText = I18n.Settings;
			((Node)val2).AddChild((Node)(object)_settingsBtn, false, (InternalMode)0);
			((Node)val).AddChild((Node)(object)val2, false, (InternalMode)0);
			PanelContainer val4 = new PanelContainer();
			_summaryStyle = new StyleBoxFlat();
			_summaryStyle.BgColor = GetHudSummaryColor();
			num2 = (_summaryStyle.BorderWidthRight = 1);
			num4 = (_summaryStyle.BorderWidthLeft = num2);
			borderWidthBottom = (_summaryStyle.BorderWidthTop = num4);
			_summaryStyle.BorderWidthBottom = borderWidthBottom;
			_summaryStyle.BorderColor = new Color(1f, 0.84f, 0f, 0.18f);
			num2 = (_summaryStyle.CornerRadiusBottomRight = 6);
			num4 = (_summaryStyle.CornerRadiusBottomLeft = num2);
			borderWidthBottom = (_summaryStyle.CornerRadiusTopRight = num4);
			_summaryStyle.CornerRadiusTopLeft = borderWidthBottom;
			contentMarginLeft = (((StyleBox)_summaryStyle).ContentMarginRight = 10f);
			((StyleBox)_summaryStyle).ContentMarginLeft = contentMarginLeft;
			((StyleBox)_summaryStyle).ContentMarginTop = 8f;
			((StyleBox)_summaryStyle).ContentMarginBottom = 8f;
			((Control)val4).AddThemeStyleboxOverride("panel", (StyleBox)(object)_summaryStyle);
			VBoxContainer val5 = new VBoxContainer();
			((Control)val5).AddThemeConstantOverride("separation", 2);
			_summaryLabel = new Label();
			((Control)_summaryLabel).AddThemeColorOverride("font_color", Colors.White);
			((Control)_summaryLabel).AddThemeFontSizeOverride("font_size", 13);
			_summaryLabel.AutowrapMode = (TextServer.AutowrapMode)2;
			((Node)val5).AddChild((Node)(object)_summaryLabel, false, (InternalMode)0);
			_metaLabel = new Label();
			((Control)_metaLabel).AddThemeColorOverride("font_color", SegmentTextColor);
			((Control)_metaLabel).AddThemeFontSizeOverride("font_size", 11);
			_metaLabel.AutowrapMode = (TextServer.AutowrapMode)2;
			((Node)val5).AddChild((Node)(object)_metaLabel, false, (InternalMode)0);
			((Node)val4).AddChild((Node)(object)val5, false, (InternalMode)0);
			((Node)val).AddChild((Node)(object)val4, false, (InternalMode)0);
			_segmentRow = new HBoxContainer();
			((Control)_segmentRow).AddThemeConstantOverride("separation", 4);
			((Control)_segmentRow).MouseFilter = (MouseFilterEnum)2;
			Button val6 = CreateNavButton(PreviousButtonText, 11, SegmentTextColor, 26f);
			((BaseButton)val6).Pressed += OnSegmentLeftPressed;
			((Node)_segmentRow).AddChild((Node)(object)val6, false, (InternalMode)0);
			_segmentLabel = new Button();
			_segmentLabel.Flat = true;
			((Control)_segmentLabel).SizeFlagsHorizontal = (SizeFlags)3;
			((Control)_segmentLabel).AddThemeColorOverride("font_color", SegmentTextColor);
			((Control)_segmentLabel).AddThemeColorOverride("font_hover_color", new Color(0.85f, 0.85f, 0.95f, 1f));
			((Control)_segmentLabel).AddThemeColorOverride("font_pressed_color", SegmentTextColor);
			((Control)_segmentLabel).AddThemeFontSizeOverride("font_size", 12);
			((Control)_segmentLabel).MouseDefaultCursorShape = (CursorShape)2;
			((BaseButton)_segmentLabel).Pressed += OnSegmentLabelPressed;
			((Control)_segmentLabel).GuiInput += OnSegmentLabelGuiInput;
			((Node)_segmentRow).AddChild((Node)(object)_segmentLabel, false, (InternalMode)0);
			Button val8 = CreateNavButton(NextButtonText, 11, SegmentTextColor, 26f);
			((BaseButton)val8).Pressed += OnSegmentRightPressed;
			((Node)_segmentRow).AddChild((Node)(object)val8, false, (InternalMode)0);
			((Node)val).AddChild((Node)(object)_segmentRow, false, (InternalMode)0);
			HBoxContainer val9 = new HBoxContainer();
			((Control)val9).AddThemeConstantOverride("separation", 4);
			((Control)val9).MouseFilter = (MouseFilterEnum)2;
			_dashboardBtn = CreateNavButton(I18n.Dashboard, 11, TextColor, 88f);
			((Control)_dashboardBtn).SizeFlagsHorizontal = (SizeFlags)3;
			((BaseButton)_dashboardBtn).Pressed += OnDashboardPressed;
			((Control)_dashboardBtn).TooltipText = I18n.Dashboard;
			((Node)val9).AddChild((Node)(object)_dashboardBtn, false, (InternalMode)0);
			_resetBtn = CreateNavButton(I18n.ResetData, 11, DimText, 72f);
			((Control)_resetBtn).SizeFlagsHorizontal = (SizeFlags)3;
			((BaseButton)_resetBtn).Pressed += OnResetPressed;
			((Control)_resetBtn).TooltipText = I18n.ResetData;
			((Node)val9).AddChild((Node)(object)_resetBtn, false, (InternalMode)0);
			Button val10 = CreateNavButton(GetOpacityButtonText(), 11, SegmentTextColor, 64f);
			((Control)val10).SizeFlagsHorizontal = (SizeFlags)3;
			((BaseButton)val10).Pressed += OnOpacityPressed;
			((Control)val10).TooltipText = I18n.SettingsOpacity;
			_opacityBtn = val10;
			((Node)val9).AddChild((Node)(object)val10, false, (InternalMode)0);
			((Node)val).AddChild((Node)(object)val9, false, (InternalMode)0);
			_scrollContainer = new ScrollContainer();
			_scrollContainer.HorizontalScrollMode = (ScrollMode)0;
			_scrollContainer.VerticalScrollMode = (ScrollMode)1;
			((Control)_scrollContainer).CustomMinimumSize = new Vector2(0f, 0f);
			((Control)_scrollContainer).SizeFlagsVertical = (SizeFlags)3;
			((CanvasItem)_scrollContainer.GetVScrollBar()).Modulate = new Color(1f, 1f, 1f, 0f);
			_contentBox = new VBoxContainer();
			((Control)_contentBox).AddThemeConstantOverride("separation", 2);
			((Control)_contentBox).SizeFlagsHorizontal = (SizeFlags)3;
			((Control)_contentBox).MouseFilter = (MouseFilterEnum)2;
			((Node)_scrollContainer).AddChild((Node)(object)_contentBox, false, (InternalMode)0);
			((Node)val).AddChild((Node)(object)_scrollContainer, false, (InternalMode)0);
			((Node)_panel).AddChild((Node)(object)val, false, (InternalMode)0);
			((Node)_canvas).AddChild((Node)(object)_panel, false, (InternalMode)0);
			((Control)_panel).GuiInput += OnPanelGuiInput;
			CombatDataCollector.StatsChanged += UpdateDisplay;
			I18n.Changed += OnLanguageChanged;
			_categoryPopup = new PopupPanel();
			((Window)_categoryPopup).Transparent = true;
			((Viewport)_categoryPopup).TransparentBg = true;
			StyleBoxFlat categoryStyle = new StyleBoxFlat();
			categoryStyle.BgColor = new Color(0.08f, 0.08f, 0.15f, 0.95f);
			num2 = (categoryStyle.BorderWidthRight = 1);
			num4 = (categoryStyle.BorderWidthLeft = num2);
			borderWidthBottom = (categoryStyle.BorderWidthTop = num4);
			categoryStyle.BorderWidthBottom = borderWidthBottom;
			categoryStyle.BorderColor = GoldBorder;
			num2 = (categoryStyle.CornerRadiusBottomRight = 6);
			num4 = (categoryStyle.CornerRadiusBottomLeft = num2);
			borderWidthBottom = (categoryStyle.CornerRadiusTopRight = num4);
			categoryStyle.CornerRadiusTopLeft = borderWidthBottom;
			contentMarginLeft = (((StyleBox)categoryStyle).ContentMarginRight = 8f);
			((StyleBox)categoryStyle).ContentMarginLeft = contentMarginLeft;
			contentMarginLeft = (((StyleBox)categoryStyle).ContentMarginBottom = 8f);
			((StyleBox)categoryStyle).ContentMarginTop = contentMarginLeft;
			((Window)_categoryPopup).AddThemeStyleboxOverride("panel", (StyleBox)(object)categoryStyle);
			_categoryGrid = new GridContainer();
			_categoryGrid.Columns = 3;
			((Control)_categoryGrid).AddThemeConstantOverride("h_separation", 4);
			((Control)_categoryGrid).AddThemeConstantOverride("v_separation", 4);
			((Node)_categoryPopup).AddChild((Node)(object)_categoryGrid, false, (InternalMode)0);
			_segmentPopup = CreateStyledPopup();
			_segmentPopup.IdPressed += OnSegmentSelected;
			((Node)_canvas).AddChild((Node)(object)_categoryPopup, false, (InternalMode)0);
			((Node)_canvas).AddChild((Node)(object)_segmentPopup, false, (InternalMode)0);
			_settingsPopup = CreateStyledPopup();
			_settingsPopup.IdPressed += OnSettingsAction;
			_scaleSubmenu = CreateStyledPopup();
			((Node)_scaleSubmenu).Name = "ScaleMenu";
			_scaleSubmenu.IdPressed += OnScaleSelected;
			((Node)_settingsPopup).AddChild((Node)(object)_scaleSubmenu, false, (InternalMode)0);
			_opacitySubmenu = CreateStyledPopup();
			((Node)_opacitySubmenu).Name = "OpacityMenu";
			_opacitySubmenu.IdPressed += OnOpacitySelected;
			((Node)_settingsPopup).AddChild((Node)(object)_opacitySubmenu, false, (InternalMode)0);
			_maxBarsSubmenu = CreateStyledPopup();
			((Node)_maxBarsSubmenu).Name = "MaxBarsMenu";
			_maxBarsSubmenu.IdPressed += OnMaxBarsSelected;
			((Node)_settingsPopup).AddChild((Node)(object)_maxBarsSubmenu, false, (InternalMode)0);
			((Node)_canvas).AddChild((Node)(object)_settingsPopup, false, (InternalMode)0);
			_resetConfirmDialog = new ConfirmationDialog();
			((AcceptDialog)_resetConfirmDialog).DialogText = I18n.ResetData + "?";
			((AcceptDialog)_resetConfirmDialog).OkButtonText = I18n.ResetData;
			((Window)_resetConfirmDialog).Title = I18n.ResetData;
			((AcceptDialog)_resetConfirmDialog).Confirmed += OnResetConfirmed;
			((Node)_canvas).AddChild((Node)(object)_resetConfirmDialog, false, (InternalMode)0);
			UpdateDisplay();
			Window root = ((SceneTree)Engine.GetMainLoop()).Root;
			((GodotObject)root).CallDeferred(Node.MethodName.AddChild, (Variant[])(object)new Variant[1] { _canvas });
			if (_inputHandler == null)
			{
				_inputHandler = new InputHandler();
				((GodotObject)root).CallDeferred(Node.MethodName.AddChild, (Variant[])(object)new Variant[1] { _inputHandler });
			}
		}
	}

	public static void Remove()
	{
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Expected O, but got Unknown
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Expected O, but got Unknown
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Expected O, but got Unknown
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Expected O, but got Unknown
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Expected O, but got Unknown
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Expected O, but got Unknown
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Expected O, but got Unknown
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Expected O, but got Unknown
		if (_canvas == null)
		{
			return;
		}
		CombatDataCollector.StatsChanged -= UpdateDisplay;
		I18n.Changed -= OnLanguageChanged;
		if (_resetBtn != null)
		{
			((BaseButton)_resetBtn).Pressed -= OnResetPressed;
		}
		if (_dashboardBtn != null)
		{
			((BaseButton)_dashboardBtn).Pressed -= OnDashboardPressed;
		}
		RemoveDashboard();
		if (_leftBtn != null)
		{
			((BaseButton)_leftBtn).Pressed -= OnLeftPressed;
		}
		if (_rightBtn != null)
		{
			((BaseButton)_rightBtn).Pressed -= OnRightPressed;
		}
		if (_titleBtn != null)
		{
			((BaseButton)_titleBtn).Pressed -= OnTitlePressed;
			((Control)_titleBtn).GuiInput -= OnTitleGuiInput;
		}
		if (_settingsBtn != null)
		{
			((BaseButton)_settingsBtn).Pressed -= OnSettingsPressed;
		}
		if (_segmentLabel != null)
		{
			((BaseButton)_segmentLabel).Pressed -= OnSegmentLabelPressed;
			((Control)_segmentLabel).GuiInput -= OnSegmentLabelGuiInput;
		}
		if (_opacityBtn != null)
		{
			((BaseButton)_opacityBtn).Pressed -= OnOpacityPressed;
		}
		if (_panel != null)
		{
			((Control)_panel).GuiInput -= OnPanelGuiInput;
		}
		if (_segmentPopup != null)
		{
			_segmentPopup.IdPressed -= OnSegmentSelected;
		}
		if (_settingsPopup != null)
		{
			_settingsPopup.IdPressed -= OnSettingsAction;
		}
		if (_scaleSubmenu != null)
		{
			_scaleSubmenu.IdPressed -= OnScaleSelected;
		}
		if (_opacitySubmenu != null)
		{
			_opacitySubmenu.IdPressed -= OnOpacitySelected;
		}
		if (_maxBarsSubmenu != null)
		{
			_maxBarsSubmenu.IdPressed -= OnMaxBarsSelected;
		}
		((Node)_canvas).QueueFree();
		_canvas = null;
		_panel = null;
		_titleBtn = null;
		_settingsBtn = null;
		_resetBtn = null;
		_dashboardBtn = null;
		_opacityBtn = null;
		_leftBtn = null;
		_rightBtn = null;
		_segmentRow = null;
		_segmentLabel = null;
		_summaryLabel = null;
		_metaLabel = null;
		_contentBox = null;
		_scrollContainer = null;
		_categoryPopup = null;
		_categoryGrid = null;
		_segmentPopup = null;
		_settingsPopup = null;
		_scaleSubmenu = null;
		_opacitySubmenu = null;
		_maxBarsSubmenu = null;
		_panelStyle = null;
		_summaryStyle = null;
		if (_resetConfirmDialog != null)
		{
			((AcceptDialog)_resetConfirmDialog).Confirmed -= OnResetConfirmed;
		}
		_resetConfirmDialog = null;
		_isDragging = false;
		_anchored = true;
	}

	public static void ToggleVisibility()
	{
		if (_canvas != null)
		{
			_canvas.Visible = !_canvas.Visible;
		}
	}

	public static void CloseDashboard()
	{
		RemoveDashboard();
	}

	private static void OnResetPressed()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (_resetConfirmDialog != null)
		{
			((AcceptDialog)_resetConfirmDialog).DialogText = I18n.ResetData + "?";
			((Window)_resetConfirmDialog).PopupCentered((Vector2I?)new Vector2I(250, 100));
		}
	}

	private static void OnResetConfirmed()
	{
		CombatDataCollector.ResetAll();
	}

	private static void OnLeftPressed()
	{
		if (_detailPlayerKey != null)
		{
			_detailPlayerKey = null;
		}
		else
		{
			_categoryIndex = (_categoryIndex - 1 + _categories.Count) % _categories.Count;
		}
		UpdateDisplay();
	}

	private static void OnRightPressed()
	{
		_detailPlayerKey = null;
		_categoryIndex = (_categoryIndex + 1) % _categories.Count;
		UpdateDisplay();
	}

	private static void OnTitlePressed()
	{
		if (_detailPlayerKey != null)
		{
			_detailPlayerKey = null;
			UpdateDisplay();
			return;
		}
		ShowCategoryMenu();
	}

	private static void OnSegmentLeftPressed()
	{
		CombatDataCollector.CycleViewBackward();
	}

	private static void OnSegmentRightPressed()
	{
		CombatDataCollector.CycleViewForward();
	}

	private static void OnSegmentLabelPressed()
	{
		ShowSegmentMenu();
	}

	private static void OnSettingsPressed()
	{
		ShowSettingsMenu();
	}

	private static void OnOpacityPressed()
	{
		int nextIndex = GetClosestOpacityIndex() + 1;
		if (nextIndex >= OpacityOptions.Length)
		{
			nextIndex = 0;
		}
		OnOpacitySelected(nextIndex);
	}

	private static void OnTitleGuiInput(InputEvent @event)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Invalid comparison between Unknown and I8
		InputEventMouseButton val = (InputEventMouseButton)(object)((@event is InputEventMouseButton) ? @event : null);
		if (val != null && val.Pressed && (long)val.ButtonIndex == 2)
		{
			ShowCategoryMenu();
		}
	}

	private static void OnSegmentLabelGuiInput(InputEvent @event)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Invalid comparison between Unknown and I8
		InputEventMouseButton val = (InputEventMouseButton)(object)((@event is InputEventMouseButton) ? @event : null);
		if (val != null && val.Pressed && (long)val.ButtonIndex == 2)
		{
			ShowSegmentMenu();
		}
	}

	private static void ShowCategoryMenu()
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Expected O, but got Unknown
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Expected O, but got Unknown
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		if (_categoryPopup == null || _categoryGrid == null || _panel == null)
		{
			return;
		}
		ClearContainer((Node)(object)_categoryGrid);
		for (int i = 0; i < _categories.Count; i++)
		{
			int catIdx = i;
			Button val = new Button();
			val.Text = _categories[i].Name;
			val.Flat = true;
			((Control)val).CustomMinimumSize = new Vector2(100f, 28f);
			((Control)val).AddThemeFontSizeOverride("font_size", 13);
			if (i == _categoryIndex)
			{
				((Control)val).AddThemeColorOverride("font_color", GoldColor);
				((Control)val).AddThemeColorOverride("font_hover_color", new Color(1f, 0.95f, 0.5f, 1f));
			}
			else
			{
				((Control)val).AddThemeColorOverride("font_color", TextColor);
				((Control)val).AddThemeColorOverride("font_hover_color", GoldColor);
			}
			((BaseButton)val).Pressed += delegate
			{
				_categoryIndex = catIdx;
				_detailPlayerKey = null;
				PopupPanel? categoryPopup2 = _categoryPopup;
				if (categoryPopup2 != null)
				{
					((Window)categoryPopup2).Hide();
				}
				UpdateDisplay();
			};
			((Node)_categoryGrid).AddChild((Node)(object)val, false, (InternalMode)0);
		}
		Button val2 = new Button();
		val2.Text = I18n.Settings;
		val2.Flat = true;
		((Control)val2).CustomMinimumSize = new Vector2(100f, 28f);
		((Control)val2).AddThemeFontSizeOverride("font_size", 13);
		((Control)val2).AddThemeColorOverride("font_color", DimText);
		((Control)val2).AddThemeColorOverride("font_hover_color", GoldColor);
		((BaseButton)val2).Pressed += delegate
		{
			PopupPanel? categoryPopup = _categoryPopup;
			if (categoryPopup != null)
			{
				((Window)categoryPopup).Hide();
			}
			ShowSettingsMenu();
		};
		((Node)_categoryGrid).AddChild((Node)(object)val2, false, (InternalMode)0);
		((Window)_categoryPopup).Position = DisplayServer.MouseGetPosition();
		((Window)_categoryPopup).ResetSize();
		((Window)_categoryPopup).Popup((Rect2I?)null);
	}

	private static void ShowSegmentMenu()
	{
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		if (_segmentPopup != null && _panel != null)
		{
			int segmentCount = CombatDataCollector.SegmentCount;
			_segmentPopup.Clear();
			_segmentPopup.AddRadioCheckItem(I18n.SegmentCurrent, 0, (Key)0);
			_segmentPopup.AddRadioCheckItem(I18n.SegmentOverall, 1, (Key)0);
			for (int i = 0; i < segmentCount; i++)
			{
				_segmentPopup.AddRadioCheckItem(CombatDataCollector.GetSegmentLabel(i), i + 2, (Key)0);
			}
			int viewIndex = CombatDataCollector.ViewIndex;
			_segmentPopup.SetItemChecked(viewIndex switch
			{
				-1 => 0, 
				-2 => 1, 
				_ => viewIndex + 2, 
			}, true);
			((Window)_segmentPopup).Position = DisplayServer.MouseGetPosition();
			((Window)_segmentPopup).ResetSize();
			((Window)_segmentPopup).Popup((Rect2I?)null);
		}
	}

	private static void ShowSettingsMenu()
	{
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		if (_settingsPopup != null && _scaleSubmenu != null && _opacitySubmenu != null && _maxBarsSubmenu != null)
		{
			_scaleSubmenu.Clear();
			for (int i = 0; i < ScaleOptions.Length; i++)
			{
				_scaleSubmenu.AddRadioCheckItem($"{(int)(ScaleOptions[i] * 100f)}%", i, (Key)0);
				_scaleSubmenu.SetItemChecked(i, Math.Abs(DamageMeterSettings.Scale - ScaleOptions[i]) < 0.01f);
			}
			_opacitySubmenu.Clear();
			for (int j = 0; j < OpacityOptions.Length; j++)
			{
				_opacitySubmenu.AddRadioCheckItem($"{(int)(OpacityOptions[j] * 100f)}%", j, (Key)0);
				_opacitySubmenu.SetItemChecked(j, j == GetClosestOpacityIndex());
			}
			_maxBarsSubmenu.Clear();
			for (int k = 0; k < MaxBarsOptions.Length; k++)
			{
				_maxBarsSubmenu.AddRadioCheckItem(MaxBarsOptions[k].ToString(), k, (Key)0);
				_maxBarsSubmenu.SetItemChecked(k, DamageMeterSettings.MaxBars == MaxBarsOptions[k]);
			}
			_settingsPopup.Clear();
			_settingsPopup.AddSubmenuNodeItem(I18n.SettingsScale, _scaleSubmenu, -1);
			_settingsPopup.AddSubmenuNodeItem(I18n.SettingsOpacity, _opacitySubmenu, -1);
			_settingsPopup.AddSubmenuNodeItem(I18n.SettingsMaxBars, _maxBarsSubmenu, -1);
			_settingsPopup.AddSeparator("", -1);
			_settingsPopup.AddCheckItem(I18n.SettingsAutoReset, 200, (Key)0);
			_settingsPopup.SetItemChecked(_settingsPopup.GetItemIndex(200), DamageMeterSettings.AutoResetOnNewRun);
			_settingsPopup.AddSeparator("", -1);
			_settingsPopup.AddItem(I18n.SettingsResetPos, 100, (Key)0);
			((Window)_settingsPopup).Position = DisplayServer.MouseGetPosition();
			((Window)_settingsPopup).ResetSize();
			((Window)_settingsPopup).Popup((Rect2I?)null);
		}
	}

	private static void OnSettingsAction(long id)
	{
		switch (id)
		{
		case 200L:
			DamageMeterSettings.AutoResetOnNewRun = !DamageMeterSettings.AutoResetOnNewRun;
			DamageMeterSettings.Save();
			break;
		case 100L:
			DamageMeterSettings.PanelX = float.NaN;
			DamageMeterSettings.PanelY = float.NaN;
			DamageMeterSettings.Save();
			if (_panel != null)
			{
				((Control)_panel).AnchorLeft = 1f;
				((Control)_panel).AnchorRight = 1f;
				((Control)_panel).AnchorTop = 0f;
				((Control)_panel).AnchorBottom = 0f;
				((Control)_panel).GrowHorizontal = (GrowDirection)0;
				((Control)_panel).GrowVertical = (GrowDirection)1;
				((Control)_panel).OffsetLeft = -10f;
				((Control)_panel).OffsetTop = 10f;
				((Control)_panel).OffsetRight = 0f;
				((Control)_panel).OffsetBottom = 0f;
				_anchored = true;
			}
			break;
		}
	}

	private static void OnScaleSelected(long id)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		int num = (int)id;
		if (num >= 0 && num < ScaleOptions.Length)
		{
			DamageMeterSettings.Scale = ScaleOptions[num];
			DamageMeterSettings.Save();
			if (_panel != null)
			{
				((Control)_panel).Scale = new Vector2(DamageMeterSettings.Scale, DamageMeterSettings.Scale);
			}
		}
	}

	private static void OnOpacitySelected(long id)
	{
		int num = (int)id;
		if (num >= 0 && num < OpacityOptions.Length)
		{
			DamageMeterSettings.Opacity = OpacityOptions[num];
			DamageMeterSettings.Save();
			ApplyHudOpacity();
		}
	}

	private static void OnMaxBarsSelected(long id)
	{
		int num = (int)id;
		if (num >= 0 && num < MaxBarsOptions.Length)
		{
			DamageMeterSettings.MaxBars = MaxBarsOptions[num];
			DamageMeterSettings.Save();
			UpdateDisplay();
		}
	}

	private static void OnSegmentSelected(long id)
	{
		int num = (int)id;
		switch (num)
		{
		case 0:
			CombatDataCollector.SetView(-1);
			break;
		case 1:
			CombatDataCollector.SetView(-2);
			break;
		default:
			CombatDataCollector.SetView(num - 2);
			break;
		}
	}

	private static PopupMenu CreateStyledPopup()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Expected O, but got Unknown
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Expected O, but got Unknown
		PopupMenu val = new PopupMenu();
		StyleBoxFlat val2 = new StyleBoxFlat();
		val2.BgColor = new Color(0.08f, 0.08f, 0.15f, 0.95f);
		int num2 = (val2.BorderWidthRight = 1);
		int num4 = (val2.BorderWidthLeft = num2);
		int borderWidthBottom = (val2.BorderWidthTop = num4);
		val2.BorderWidthBottom = borderWidthBottom;
		val2.BorderColor = GoldBorder;
		num2 = (val2.CornerRadiusBottomRight = 6);
		num4 = (val2.CornerRadiusBottomLeft = num2);
		borderWidthBottom = (val2.CornerRadiusTopRight = num4);
		val2.CornerRadiusTopLeft = borderWidthBottom;
		float contentMarginLeft = (((StyleBox)val2).ContentMarginRight = 10f);
		((StyleBox)val2).ContentMarginLeft = contentMarginLeft;
		contentMarginLeft = (((StyleBox)val2).ContentMarginBottom = 6f);
		((StyleBox)val2).ContentMarginTop = contentMarginLeft;
		((Window)val).AddThemeStyleboxOverride("panel", (StyleBox)(object)val2);
		StyleBoxFlat val3 = new StyleBoxFlat();
		val3.BgColor = new Color(0.25f, 0.22f, 0.1f, 0.8f);
		num2 = (val3.CornerRadiusBottomRight = 3);
		num4 = (val3.CornerRadiusBottomLeft = num2);
		borderWidthBottom = (val3.CornerRadiusTopRight = num4);
		val3.CornerRadiusTopLeft = borderWidthBottom;
		((Window)val).AddThemeStyleboxOverride("hover", (StyleBox)(object)val3);
		((Window)val).AddThemeColorOverride("font_color", TextColor);
		((Window)val).AddThemeColorOverride("font_hover_color", GoldColor);
		((Window)val).AddThemeFontSizeOverride("font_size", 13);
		return val;
	}

	private static void OnBarClicked(string playerKey)
	{
		_detailPlayerKey = playerKey;
		UpdateDisplay();
	}

	private static void OnLanguageChanged()
	{
		UpdateDisplay();
	}

	private static void UpdateDisplay()
	{
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
		if (_contentBox == null || _titleBtn == null || _categories.Count == 0)
		{
			return;
		}
		IStatCategory statCategory = _categories[_categoryIndex];
		ClearContainer((Node)(object)_contentBox);
		if (_settingsBtn != null)
		{
			_settingsBtn.Text = I18n.Settings;
			((Control)_settingsBtn).TooltipText = I18n.Settings;
		}
		if (_dashboardBtn != null)
		{
			_dashboardBtn.Text = I18n.Dashboard;
			((Control)_dashboardBtn).TooltipText = I18n.Dashboard;
		}
		if (_resetBtn != null)
		{
			_resetBtn.Text = I18n.ResetData;
			((Control)_resetBtn).TooltipText = I18n.ResetData;
		}
		if (_opacityBtn != null)
		{
			_opacityBtn.Text = GetOpacityButtonText();
			((Control)_opacityBtn).TooltipText = I18n.SettingsOpacity;
		}
		if (_segmentRow != null)
		{
			((CanvasItem)_segmentRow).Visible = true;
			if (_segmentLabel != null)
			{
				_segmentLabel.Text = CombatDataCollector.GetViewLabel();
			}
		}
		int num = 0;
		if (_detailPlayerKey != null)
		{
			string detailTitle = statCategory.GetDetailTitle(_detailPlayerKey);
			_titleBtn.Text = BackButtonPrefix + detailTitle;
			((Control)_titleBtn).MouseDefaultCursorShape = (CursorShape)2;
			List<BarData> detailBars = statCategory.GetDetailBars(_detailPlayerKey);
			UpdateHudSummary(statCategory, detailBars, detailView: true);
			if (detailBars.Count == 0)
			{
				AddNoDataLabel();
				UpdateScrollHeight(1);
				return;
			}
			int maxVal = detailBars.Max((BarData b) => b.Value);
			int totalVal = detailBars.Sum((BarData b) => b.Value);
			for (int i = 0; i < detailBars.Count; i++)
			{
				Color color = DetailColors[i % DetailColors.Length];
				AddBarNode(detailBars[i], color, maxVal, totalVal, clickable: false, null, i == 0);
			}
			num = detailBars.Count;
		}
		else
		{
			_titleBtn.Text = statCategory.Name;
			((Control)_titleBtn).MouseDefaultCursorShape = (CursorShape)2;
			List<BarData> playerBars = statCategory.GetPlayerBars();
			UpdateHudSummary(statCategory, playerBars, detailView: false);
			if (playerBars.Count == 0)
			{
				AddNoDataLabel();
				UpdateScrollHeight(1);
				return;
			}
			bool hasDetail = statCategory.HasDetail;
			int maxVal2 = playerBars.Max((BarData b) => b.Value);
			int totalVal2 = playerBars.Sum((BarData b) => b.Value);
			int num2 = Math.Min(playerBars.Count, DamageMeterSettings.MaxBars);
			for (int j = 0; j < num2; j++)
			{
				Color color2 = (hasDetail ? GetPlayerColor(playerBars[j].Key) : DetailColors[j % DetailColors.Length]);
				CombatDataCollector.PlayerStats value;
				string characterId = (CombatDataCollector.Players.TryGetValue(playerBars[j].Key, out value) ? value.CharacterId : playerBars[j].Key);
				Texture2D icon = (hasDetail ? LoadCharacterIcon(characterId) : null);
				BarData barData = playerBars[j];
				if (hasDetail)
				{
					barData = new BarData
					{
						Key = playerBars[j].Key,
						Label = $"{j + 1}. {playerBars[j].Label}",
						Value = playerBars[j].Value,
						DisplayText = playerBars[j].DisplayText
					};
				}
				AddBarNode(barData, color2, maxVal2, totalVal2, hasDetail, icon, j == 0);
			}
			num = num2;
		}
		UpdateScrollHeight(num);
		UpdateDashboard();
	}

	private static void UpdateScrollHeight(int barCount)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		if (_scrollContainer != null)
		{
			float val = (float)barCount * 24f;
			float val2 = 320f;
			((Control)_scrollContainer).CustomMinimumSize = new Vector2(0f, Math.Min(val, val2));
		}
	}

	private static void UpdateHudSummary(IStatCategory category, List<BarData> bars, bool detailView)
	{
		if (_summaryLabel == null || _metaLabel == null)
		{
			return;
		}

		if (bars.Count == 0)
		{
			_summaryLabel.Text = I18n.WaitingForCombat;
			_metaLabel.Text = BuildHudMetaText(0);
			return;
		}

		BarData topBar = bars[0];
		int total = bars.Sum((BarData bar) => bar.Value);
		float share = (total > 0) ? ((float)topBar.Value / (float)total * 100f) : 0f;
		string topDisplay = string.IsNullOrWhiteSpace(topBar.DisplayText) ? topBar.Value.ToString() : topBar.DisplayText.Trim();
		if (detailView)
		{
			_summaryLabel.Text = $"{I18n.Get("hud_breakdown", "Focus")}: {TrimRankPrefix(topBar.Label)} - {topDisplay}";
		}
		else
		{
			_summaryLabel.Text = $"{I18n.Get("hud_leader", "Leader")}: {TrimRankPrefix(topBar.Label)} - {topDisplay} ({share:F1}%)";
		}
		_metaLabel.Text = BuildHudMetaText(bars.Count);
	}

	private static string BuildHudMetaText(int entryCount)
	{
		List<string> list = new List<string>
		{
			CombatDataCollector.GetViewLabel(),
			$"{CombatDataCollector.SelectedViewTurnCount} {I18n.Get("hud_turns", "turns")}",
			$"{CombatDataCollector.Players.Count} {I18n.Get("hud_players", "players")}"
		};
		if (entryCount > 0)
		{
			list.Add($"{entryCount} {I18n.Get("hud_rows", "rows")}");
		}
		return string.Join("  •  ", list);
	}

	private static string TrimRankPrefix(string text)
	{
		int num = text.IndexOf(". ", StringComparison.Ordinal);
		if (num > 0 && int.TryParse(text.Substring(0, num), out _))
		{
			return text.Substring(num + 2);
		}
		return text;
	}

	private static void AddNoDataLabel()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected O, but got Unknown
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		Label val = new Label();
		val.Text = I18n.WaitingForCombat;
		val.HorizontalAlignment = (HorizontalAlignment)1;
		((Control)val).AddThemeColorOverride("font_color", DimText);
		((Control)val).AddThemeFontSizeOverride("font_size", 13);
		((Control)val).MouseFilter = (MouseFilterEnum)2;
		VBoxContainer? contentBox = _contentBox;
		if (contentBox != null)
		{
			((Node)contentBox).AddChild((Node)(object)val, false, (InternalMode)0);
		}
	}

	private static void AddBarNode(BarData data, Color color, int maxVal, int totalVal, bool clickable, Texture2D? icon = null, bool highlight = false)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Expected O, but got Unknown
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Expected O, but got Unknown
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Expected O, but got Unknown
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Expected O, but got Unknown
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0285: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Expected O, but got Unknown
		//IL_0364: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b5: Expected O, but got Unknown
		if (_contentBox == null)
		{
			return;
		}
		float anchorRight = ((maxVal > 0) ? Math.Clamp((float)data.Value / (float)maxVal, 0f, 1f) : 0f);
		float value = ((totalVal > 0) ? ((float)data.Value / (float)totalVal * 100f) : 0f);
		Panel val = new Panel();
		((Control)val).CustomMinimumSize = new Vector2(0f, 26f);
		((Control)val).SizeFlagsHorizontal = (SizeFlags)3;
		((Control)val).MouseFilter = (MouseFilterEnum)0;
		StyleBoxFlat val2 = new StyleBoxFlat();
		val2.BgColor = BarBgColor;
		int num2 = (val2.CornerRadiusBottomRight = 4);
		int num4 = (val2.CornerRadiusBottomLeft = num2);
		int cornerRadiusTopLeft = (val2.CornerRadiusTopRight = num4);
		val2.CornerRadiusTopLeft = cornerRadiusTopLeft;
		if (highlight)
		{
			num2 = (val2.BorderWidthRight = 1);
			num4 = (val2.BorderWidthLeft = num2);
			cornerRadiusTopLeft = (val2.BorderWidthTop = num4);
			val2.BorderWidthBottom = cornerRadiusTopLeft;
			val2.BorderColor = new Color(1f, 0.84f, 0f, 0.25f);
		}
		((Control)val).AddThemeStyleboxOverride("panel", (StyleBox)(object)val2);
		ColorRect val3 = new ColorRect();
		val3.Color = new Color(color.R, color.G, color.B, highlight ? 0.48f : 0.35f);
		((Control)val3).AnchorRight = anchorRight;
		((Control)val3).AnchorBottom = 1f;
		((Control)val3).OffsetLeft = 0f;
		((Control)val3).OffsetTop = 0f;
		((Control)val3).OffsetRight = 0f;
		((Control)val3).OffsetBottom = 0f;
		((Control)val3).MouseFilter = (MouseFilterEnum)2;
		((Node)val).AddChild((Node)(object)val3, false, (InternalMode)0);
		int num6 = 0;
		if (icon != null)
		{
			TextureRect val4 = new TextureRect();
			val4.Texture = icon;
			val4.ExpandMode = (ExpandModeEnum)1;
			val4.StretchMode = (StretchModeEnum)5;
			((Control)val4).AnchorBottom = 1f;
			((Control)val4).OffsetLeft = 3f;
			((Control)val4).OffsetTop = 2f;
			((Control)val4).OffsetRight = 23f;
			((Control)val4).OffsetBottom = -2f;
			((Control)val4).MouseFilter = (MouseFilterEnum)2;
			((Node)val).AddChild((Node)(object)val4, false, (InternalMode)0);
			num6 = 24;
		}
		Label val5 = new Label();
		val5.Text = ((icon != null) ? (" " + data.Label) : ("  " + data.Label));
		((Control)val5).AnchorRight = 0.58f;
		((Control)val5).AnchorBottom = 1f;
		((Control)val5).OffsetLeft = num6;
		((Control)val5).OffsetTop = 0f;
		((Control)val5).OffsetRight = 0f;
		((Control)val5).OffsetBottom = 0f;
		val5.VerticalAlignment = (VerticalAlignment)1;
		((Control)val5).AddThemeFontSizeOverride("font_size", 12);
		((Control)val5).AddThemeColorOverride("font_color", highlight ? GoldColor : Colors.White);
		val5.ClipText = true;
		((Control)val5).MouseFilter = (MouseFilterEnum)2;
		((Node)val).AddChild((Node)(object)val5, false, (InternalMode)0);
		Label val6 = new Label();
		val6.Text = data.DisplayText ?? $"{data.Value}  ({value:F1}%)  ";
		((Control)val6).AnchorLeft = 0.52f;
		((Control)val6).AnchorRight = 1f;
		((Control)val6).AnchorBottom = 1f;
		((Control)val6).OffsetLeft = 0f;
		((Control)val6).OffsetTop = 0f;
		((Control)val6).OffsetRight = 0f;
		((Control)val6).OffsetBottom = 0f;
		val6.HorizontalAlignment = (HorizontalAlignment)2;
		val6.VerticalAlignment = (VerticalAlignment)1;
		((Control)val6).AddThemeFontSizeOverride("font_size", 12);
		((Control)val6).AddThemeColorOverride("font_color", TextColor);
		val6.ClipText = true;
		((Control)val6).MouseFilter = (MouseFilterEnum)2;
		((Node)val).AddChild((Node)(object)val6, false, (InternalMode)0);
		if (clickable)
		{
			string key = data.Key;
			((Control)val).GuiInput += delegate(InputEvent @event)
			{
				//IL_0013: Unknown result type (might be due to invalid IL or missing references)
				//IL_001a: Invalid comparison between Unknown and I8
				InputEventMouseButton val7 = (InputEventMouseButton)(object)((@event is InputEventMouseButton) ? @event : null);
				if (val7 != null && val7.Pressed && (long)val7.ButtonIndex == 1)
				{
					OnBarClicked(key);
				}
			};
			((Control)val).MouseDefaultCursorShape = (CursorShape)2;
		}
		((Node)_contentBox).AddChild((Node)(object)val, false, (InternalMode)0);
	}

	private static void OnDashboardPressed()
	{
		if (_dashboardVisible)
		{
			RemoveDashboard();
		}
		else
		{
			ShowDashboard();
		}
	}

	private static void ShowDashboard()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Expected O, but got Unknown
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Expected O, but got Unknown
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Expected O, but got Unknown
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Expected O, but got Unknown
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Expected O, but got Unknown
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Expected O, but got Unknown
		//IL_0285: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Expected O, but got Unknown
		//IL_031c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0323: Expected O, but got Unknown
		//IL_0323: Unknown result type (might be due to invalid IL or missing references)
		//IL_0328: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Expected O, but got Unknown
		//IL_037a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0381: Expected O, but got Unknown
		//IL_03a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03af: Expected O, but got Unknown
		//IL_0437: Unknown result type (might be due to invalid IL or missing references)
		//IL_0453: Unknown result type (might be due to invalid IL or missing references)
		//IL_0458: Unknown result type (might be due to invalid IL or missing references)
		//IL_045d: Unknown result type (might be due to invalid IL or missing references)
		if (_dashboardCanvas == null)
		{
			_dashboardCanvas = new CanvasLayer
			{
				Layer = 110,
				Name = "DamageMeterDashboard"
			};
			ColorRect val = new ColorRect();
			val.Color = new Color(0f, 0f, 0f, Math.Clamp(DamageMeterSettings.Opacity - 0.2f, 0.35f, 0.7f));
			((Control)val).SetAnchorsPreset((LayoutPreset)15, false);
			((Control)val).MouseFilter = (MouseFilterEnum)0;
			((Control)val).GuiInput += OnDashboardBgInput;
			((Node)_dashboardCanvas).AddChild((Node)(object)val, false, (InternalMode)0);
			MarginContainer val3 = new MarginContainer();
			((Control)val3).SetAnchorsPreset((LayoutPreset)15, false);
			((Control)val3).AddThemeConstantOverride("margin_left", 40);
			((Control)val3).AddThemeConstantOverride("margin_right", 40);
			((Control)val3).AddThemeConstantOverride("margin_top", 30);
			((Control)val3).AddThemeConstantOverride("margin_bottom", 30);
			((Node)val).AddChild((Node)(object)val3, false, (InternalMode)0);
			PanelContainer val4 = new PanelContainer();
			StyleBoxFlat val5 = new StyleBoxFlat();
			val5.BgColor = new Color(0.04f, 0.04f, 0.1f, Math.Clamp(DamageMeterSettings.Opacity + 0.08f, 0.82f, 0.97f));
			int num2 = (val5.BorderWidthRight = 1);
			int num4 = (val5.BorderWidthLeft = num2);
			int borderWidthBottom = (val5.BorderWidthTop = num4);
			val5.BorderWidthBottom = borderWidthBottom;
			val5.BorderColor = GoldBorder;
			num2 = (val5.CornerRadiusBottomRight = 10);
			num4 = (val5.CornerRadiusBottomLeft = num2);
			borderWidthBottom = (val5.CornerRadiusTopRight = num4);
			val5.CornerRadiusTopLeft = borderWidthBottom;
			float contentMarginLeft = (((StyleBox)val5).ContentMarginRight = 16f);
			((StyleBox)val5).ContentMarginLeft = contentMarginLeft;
			((StyleBox)val5).ContentMarginTop = 12f;
			((StyleBox)val5).ContentMarginBottom = 16f;
			((Control)val4).AddThemeStyleboxOverride("panel", (StyleBox)(object)val5);
			((Node)val3).AddChild((Node)(object)val4, false, (InternalMode)0);
			VBoxContainer val6 = new VBoxContainer();
			((Control)val6).AddThemeConstantOverride("separation", 10);
			((Node)val4).AddChild((Node)(object)val6, false, (InternalMode)0);
			HBoxContainer val7 = new HBoxContainer();
			((Control)val7).AddThemeConstantOverride("separation", 8);
			Label val8 = new Label();
			val8.Text = I18n.Dashboard + DashboardTitleSeparator + CombatDataCollector.GetViewLabel();
			((Control)val8).AddThemeColorOverride("font_color", GoldColor);
			((Control)val8).AddThemeFontSizeOverride("font_size", 18);
			((Control)val8).SizeFlagsHorizontal = (SizeFlags)3;
			((Node)val7).AddChild((Node)(object)val8, false, (InternalMode)0);
			Button val9 = new Button();
			val9.Text = CloseButtonText;
			val9.Flat = true;
			((Control)val9).CustomMinimumSize = new Vector2(32f, 28f);
			((Control)val9).AddThemeFontSizeOverride("font_size", 16);
			((Control)val9).AddThemeColorOverride("font_color", DimText);
			((Control)val9).AddThemeColorOverride("font_hover_color", new Color(1f, 0.4f, 0.4f, 1f));
			((BaseButton)val9).Pressed += RemoveDashboard;
			((Node)val7).AddChild((Node)(object)val9, false, (InternalMode)0);
			((Node)val6).AddChild((Node)(object)val7, false, (InternalMode)0);
			HSeparator val10 = new HSeparator();
			StyleBoxFlat val11 = new StyleBoxFlat
			{
				BgColor = new Color(1f, 0.84f, 0f, 0.2f)
			};
			((Control)val10).AddThemeStyleboxOverride("separator", (StyleBox)(object)val11);
			((Control)val10).AddThemeConstantOverride("separation", 4);
			((Node)val6).AddChild((Node)(object)val10, false, (InternalMode)0);
			ScrollContainer val12 = new ScrollContainer();
			val12.HorizontalScrollMode = (ScrollMode)0;
			val12.VerticalScrollMode = (ScrollMode)1;
			((Control)val12).SizeFlagsVertical = (SizeFlags)3;
			((Node)val6).AddChild((Node)(object)val12, false, (InternalMode)0);
			GridContainer val13 = new GridContainer();
			val13.Columns = 3;
			((Control)val13).AddThemeConstantOverride("h_separation", 12);
			((Control)val13).AddThemeConstantOverride("v_separation", 12);
			((Control)val13).SizeFlagsHorizontal = (SizeFlags)3;
			((Node)val12).AddChild((Node)(object)val13, false, (InternalMode)0);
			for (int i = 0; i < _categories.Count; i++)
			{
				PanelContainer val14 = CreateDashboardMiniPanel(_categories[i], i);
				((Node)val13).AddChild((Node)(object)val14, false, (InternalMode)0);
			}
			_dashboardVisible = true;
			((GodotObject)((SceneTree)Engine.GetMainLoop()).Root).CallDeferred(Node.MethodName.AddChild, (Variant[])(object)new Variant[1] { _dashboardCanvas });
		}
	}

	private static void RemoveDashboard()
	{
		if (_dashboardCanvas != null)
		{
			((Node)_dashboardCanvas).QueueFree();
			_dashboardCanvas = null;
		}
		_dashboardVisible = false;
		_dashboardDetailState.Clear();
	}

	private static void UpdateDashboard()
	{
		if (_dashboardVisible && _dashboardCanvas != null)
		{
			RemoveDashboard();
			ShowDashboard();
		}
	}

	private static void OnDashboardBgInput(InputEvent @event)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Invalid comparison between Unknown and I8
		InputEventMouseButton val = (InputEventMouseButton)(object)((@event is InputEventMouseButton) ? @event : null);
		if (val != null && val.Pressed && (long)val.ButtonIndex == 2)
		{
			RemoveDashboard();
		}
	}

	private static PanelContainer CreateDashboardMiniPanel(IStatCategory category, int categoryIndex)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Expected O, but got Unknown
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Expected O, but got Unknown
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Expected O, but got Unknown
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Expected O, but got Unknown
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		//IL_023e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Expected O, but got Unknown
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c2: Expected O, but got Unknown
		//IL_02e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0418: Unknown result type (might be due to invalid IL or missing references)
		//IL_040a: Unknown result type (might be due to invalid IL or missing references)
		//IL_043d: Unknown result type (might be due to invalid IL or missing references)
		//IL_041d: Unknown result type (might be due to invalid IL or missing references)
		PanelContainer val = new PanelContainer();
		((Control)val).SizeFlagsHorizontal = (SizeFlags)3;
		((Control)val).CustomMinimumSize = new Vector2(320f, 0f);
		StyleBoxFlat val2 = new StyleBoxFlat();
		val2.BgColor = new Color(0.06f, 0.06f, 0.14f, Math.Clamp(DamageMeterSettings.Opacity + 0.02f, 0.78f, 0.93f));
		int num2 = (val2.BorderWidthRight = 1);
		int num4 = (val2.BorderWidthLeft = num2);
		int borderWidthBottom = (val2.BorderWidthTop = num4);
		val2.BorderWidthBottom = borderWidthBottom;
		val2.BorderColor = new Color(1f, 0.84f, 0f, 0.15f);
		num2 = (val2.CornerRadiusBottomRight = 6);
		num4 = (val2.CornerRadiusBottomLeft = num2);
		borderWidthBottom = (val2.CornerRadiusTopRight = num4);
		val2.CornerRadiusTopLeft = borderWidthBottom;
		float contentMarginLeft = (((StyleBox)val2).ContentMarginRight = 8f);
		((StyleBox)val2).ContentMarginLeft = contentMarginLeft;
		((StyleBox)val2).ContentMarginTop = 6f;
		((StyleBox)val2).ContentMarginBottom = 8f;
		((Control)val).AddThemeStyleboxOverride("panel", (StyleBox)(object)val2);
		VBoxContainer val3 = new VBoxContainer();
		((Control)val3).AddThemeConstantOverride("separation", 3);
		_dashboardDetailState.TryGetValue(categoryIndex, out string value);
		bool flag = value != null;
		int catIdx = categoryIndex;
		if (flag)
		{
			Button val4 = new Button();
			val4.Text = BackButtonPrefix + category.GetDetailTitle(value);
			val4.Flat = true;
			((Control)val4).SizeFlagsHorizontal = (SizeFlags)3;
			((Control)val4).AddThemeColorOverride("font_color", GoldColor);
			((Control)val4).AddThemeColorOverride("font_hover_color", new Color(1f, 0.95f, 0.5f, 1f));
			((Control)val4).AddThemeFontSizeOverride("font_size", 13);
			((Control)val4).MouseDefaultCursorShape = (CursorShape)2;
			((BaseButton)val4).Pressed += delegate
			{
				OnDashboardTitleClicked(catIdx);
			};
			((Node)val3).AddChild((Node)(object)val4, false, (InternalMode)0);
		}
		else
		{
			Label val5 = new Label();
			val5.Text = category.Name;
			((Control)val5).AddThemeColorOverride("font_color", GoldColor);
			((Control)val5).AddThemeFontSizeOverride("font_size", 13);
			val5.HorizontalAlignment = (HorizontalAlignment)1;
			((Node)val3).AddChild((Node)(object)val5, false, (InternalMode)0);
		}
		HSeparator val6 = new HSeparator();
		StyleBoxFlat val7 = new StyleBoxFlat
		{
			BgColor = new Color(1f, 0.84f, 0f, 0.15f)
		};
		((Control)val6).AddThemeStyleboxOverride("separator", (StyleBox)(object)val7);
		((Control)val6).AddThemeConstantOverride("separation", 2);
		((Node)val3).AddChild((Node)(object)val6, false, (InternalMode)0);
		List<BarData> list;
		bool flag2;
		if (flag)
		{
			list = category.GetDetailBars(value);
			flag2 = false;
		}
		else
		{
			list = category.GetPlayerBars();
			flag2 = category.HasDetail;
		}
		TrendChartData trendChartData = TryGetDashboardTrend(category, value);
		if (trendChartData != null && trendChartData.Values.Count > 1)
		{
			PanelContainer val8 = CreateTrendPanel(trendChartData);
			((Node)val3).AddChild((Node)(object)val8, false, (InternalMode)0);
		}
		if (list.Count == 0)
		{
			Label val9 = new Label();
			val9.Text = I18n.WaitingForCombat;
			val9.HorizontalAlignment = (HorizontalAlignment)1;
			((Control)val9).AddThemeColorOverride("font_color", DimText);
			((Control)val9).AddThemeFontSizeOverride("font_size", 11);
			((Node)val3).AddChild((Node)(object)val9, false, (InternalMode)0);
		}
		else
		{
			int maxVal = list.Max((BarData b) => b.Value);
			int totalVal = list.Sum((BarData b) => b.Value);
			int num10 = Math.Min(list.Count, DamageMeterSettings.MaxBars);
			for (int i = 0; i < num10; i++)
			{
				BarData barData = list[i];
				string label = ((!flag && flag2) ? $"{i + 1}. {barData.Label}" : barData.Label);
				Color color = ((!flag) ? (flag2 ? GetPlayerColor(barData.Key) : DetailColors[i % DetailColors.Length]) : DetailColors[i % DetailColors.Length]);
				string playerKey = barData.Key;
				Panel val10 = CreateDashboardBar(label, barData.Value, barData.DisplayText, color, maxVal, totalVal, flag2, flag2 ? ((Action)delegate
				{
					OnDashboardBarClicked(catIdx, playerKey);
				}) : null, i == 0);
				((Node)val3).AddChild((Node)(object)val10, false, (InternalMode)0);
			}
		}
		((Node)val).AddChild((Node)(object)val3, false, (InternalMode)0);
		return val;
	}

	private static void OnDashboardBarClicked(int categoryIndex, string playerKey)
	{
		_dashboardDetailState[categoryIndex] = playerKey;
		CanvasLayer? dashboardCanvas = _dashboardCanvas;
		_dashboardCanvas = null;
		_dashboardVisible = false;
		if (dashboardCanvas != null)
		{
			((Node)dashboardCanvas).QueueFree();
		}
		ShowDashboard();
	}

	private static void OnDashboardTitleClicked(int categoryIndex)
	{
		_dashboardDetailState.Remove(categoryIndex);
		CanvasLayer? dashboardCanvas = _dashboardCanvas;
		_dashboardCanvas = null;
		_dashboardVisible = false;
		if (dashboardCanvas != null)
		{
			((Node)dashboardCanvas).QueueFree();
		}
		ShowDashboard();
	}

	private static TrendChartData? TryGetDashboardTrend(IStatCategory category, string? playerKey)
	{
		if (category is ITrendChartCategory trendChartCategory)
		{
			return trendChartCategory.GetDashboardTrend(playerKey);
		}
		return null;
	}

	private static PanelContainer CreateTrendPanel(TrendChartData trendChartData)
	{
		PanelContainer val = new PanelContainer();
		StyleBoxFlat val2 = new StyleBoxFlat();
		val2.BgColor = new Color(0.04f, 0.05f, 0.09f, 0.82f);
		int num = (val2.CornerRadiusBottomRight = 5);
		int num2 = (val2.CornerRadiusBottomLeft = num);
		int cornerRadiusTopLeft = (val2.CornerRadiusTopRight = num2);
		val2.CornerRadiusTopLeft = cornerRadiusTopLeft;
		((StyleBox)val2).ContentMarginLeft = 8f;
		((StyleBox)val2).ContentMarginRight = 8f;
		((StyleBox)val2).ContentMarginTop = 6f;
		((StyleBox)val2).ContentMarginBottom = 6f;
		((Control)val).AddThemeStyleboxOverride("panel", (StyleBox)(object)val2);
		VBoxContainer val3 = new VBoxContainer();
		((Control)val3).AddThemeConstantOverride("separation", 4);
		HBoxContainer val4 = new HBoxContainer();
		Label val5 = new Label();
		val5.Text = trendChartData.Title;
		((Control)val5).AddThemeFontSizeOverride("font_size", 11);
		((Control)val5).AddThemeColorOverride("font_color", TextColor);
		((Control)val5).SizeFlagsHorizontal = (SizeFlags)3;
		((Node)val4).AddChild((Node)(object)val5, false, (InternalMode)0);
		if (!string.IsNullOrWhiteSpace(trendChartData.Summary))
		{
			Label val6 = new Label();
			val6.Text = trendChartData.Summary;
			((Control)val6).AddThemeFontSizeOverride("font_size", 10);
			((Control)val6).AddThemeColorOverride("font_color", SegmentTextColor);
			val6.HorizontalAlignment = (HorizontalAlignment)2;
			((Node)val4).AddChild((Node)(object)val6, false, (InternalMode)0);
		}
		((Node)val3).AddChild((Node)(object)val4, false, (InternalMode)0);
		MiniLineChart val7 = new MiniLineChart();
		((Control)val7).CustomMinimumSize = new Vector2(0f, 68f);
		((Control)val7).SizeFlagsHorizontal = (SizeFlags)3;
		val7.SetSeries(trendChartData.Values, trendChartData.LineColor);
		((Node)val3).AddChild((Node)(object)val7, false, (InternalMode)0);
		((Node)val).AddChild((Node)(object)val3, false, (InternalMode)0);
		return val;
	}

	private static Panel CreateDashboardBar(string label, int value, string? displayText, Color color, int maxVal, int totalVal, bool clickable = false, Action? onClick = null, bool highlight = false)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Expected O, but got Unknown
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Expected O, but got Unknown
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Expected O, but got Unknown
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Expected O, but got Unknown
		//IL_0243: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Expected O, but got Unknown
		Action onClick2 = onClick;
		float anchorRight = ((maxVal > 0) ? Math.Clamp((float)value / (float)maxVal, 0f, 1f) : 0f);
		float value2 = ((totalVal > 0) ? ((float)value / (float)totalVal * 100f) : 0f);
		Panel val = new Panel();
		((Control)val).CustomMinimumSize = new Vector2(0f, 20f);
		((Control)val).SizeFlagsHorizontal = (SizeFlags)3;
		((Control)val).MouseFilter = (MouseFilterEnum)(clickable ? 0 : 2);
		StyleBoxFlat val2 = new StyleBoxFlat();
		val2.BgColor = BarBgColor;
		int num2 = (val2.CornerRadiusBottomRight = 2);
		int num4 = (val2.CornerRadiusBottomLeft = num2);
		int cornerRadiusTopLeft = (val2.CornerRadiusTopRight = num4);
		val2.CornerRadiusTopLeft = cornerRadiusTopLeft;
		if (highlight)
		{
			num2 = (val2.BorderWidthRight = 1);
			num4 = (val2.BorderWidthLeft = num2);
			cornerRadiusTopLeft = (val2.BorderWidthTop = num4);
			val2.BorderWidthBottom = cornerRadiusTopLeft;
			val2.BorderColor = new Color(1f, 0.84f, 0f, 0.22f);
		}
		((Control)val).AddThemeStyleboxOverride("panel", (StyleBox)(object)val2);
		ColorRect val3 = new ColorRect();
		val3.Color = new Color(color.R, color.G, color.B, highlight ? 0.48f : 0.35f);
		((Control)val3).AnchorRight = anchorRight;
		((Control)val3).AnchorBottom = 1f;
		((Control)val3).MouseFilter = (MouseFilterEnum)2;
		((Node)val).AddChild((Node)(object)val3, false, (InternalMode)0);
		Label val4 = new Label();
		val4.Text = "  " + label;
		((Control)val4).AnchorRight = 0.55f;
		((Control)val4).AnchorBottom = 1f;
		val4.VerticalAlignment = (VerticalAlignment)1;
		((Control)val4).AddThemeFontSizeOverride("font_size", 11);
		((Control)val4).AddThemeColorOverride("font_color", highlight ? GoldColor : Colors.White);
		val4.ClipText = true;
		((Control)val4).MouseFilter = (MouseFilterEnum)2;
		((Node)val).AddChild((Node)(object)val4, false, (InternalMode)0);
		Label val5 = new Label();
		val5.Text = displayText ?? $"{value}  ({value2:F1}%)  ";
		((Control)val5).AnchorLeft = 0.5f;
		((Control)val5).AnchorRight = 1f;
		((Control)val5).AnchorBottom = 1f;
		val5.HorizontalAlignment = (HorizontalAlignment)2;
		val5.VerticalAlignment = (VerticalAlignment)1;
		((Control)val5).AddThemeFontSizeOverride("font_size", 11);
		((Control)val5).AddThemeColorOverride("font_color", TextColor);
		val5.ClipText = true;
		((Control)val5).MouseFilter = (MouseFilterEnum)2;
		((Node)val).AddChild((Node)(object)val5, false, (InternalMode)0);
		if (clickable && onClick2 != null)
		{
			((Control)val).GuiInput += delegate(InputEvent @event)
			{
				//IL_0013: Unknown result type (might be due to invalid IL or missing references)
				//IL_001a: Invalid comparison between Unknown and I8
				InputEventMouseButton val6 = (InputEventMouseButton)(object)((@event is InputEventMouseButton) ? @event : null);
				if (val6 != null && val6.Pressed && (long)val6.ButtonIndex == 1)
				{
					onClick2();
				}
			};
			((Control)val).MouseDefaultCursorShape = (CursorShape)2;
		}
		return val;
	}

	private static Texture2D? LoadCharacterIcon(string characterId)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Expected O, but got Unknown
		if (_iconCache.TryGetValue(characterId, out Texture2D value))
		{
			return value;
		}
		Texture2D val = null;
		if (CharacterIconMap.TryGetValue(characterId, out string value2))
		{
			try
			{
				using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DamageMeter.icons." + value2 + ".png");
				if (stream != null)
				{
					byte[] array = new byte[stream.Length];
					stream.ReadExactly(array);
					Image val2 = new Image();
					val2.LoadPngFromBuffer(array);
					val = (Texture2D)(object)ImageTexture.CreateFromImage(val2);
				}
			}
			catch (Exception ex)
			{
				MainFile.Log.Error("Failed to load icon for " + characterId + ": " + ex.Message, 1);
			}
		}
		_iconCache[characterId] = val;
		return val;
	}

	private static Color GetPlayerColor(string playerKey)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		if (CombatDataCollector.Players.TryGetValue(playerKey, out CombatDataCollector.PlayerStats value))
		{
			return value.CharacterColor;
		}
		int num = _playerColorOrder.IndexOf(playerKey);
		if (num < 0)
		{
			_playerColorOrder.Add(playerKey);
			num = _playerColorOrder.Count - 1;
		}
		Color[] array = (Color[])(object)new Color[4]
		{
			new Color(0.95f, 0.55f, 0.15f, 1f),
			new Color(0.3f, 0.6f, 1f, 1f),
			new Color(0.3f, 0.85f, 0.4f, 1f),
			new Color(0.7f, 0.4f, 1f, 1f)
		};
		return array[num % array.Length];
	}

	private static Color GetHudPanelColor()
	{
		return new Color(0.05f, 0.05f, 0.12f, DamageMeterSettings.Opacity);
	}

	private static Color GetHudSummaryColor()
	{
		float num = Math.Clamp(DamageMeterSettings.Opacity - 0.18f, 0.32f, 0.78f);
		return new Color(0.09f, 0.1f, 0.18f, num);
	}

	private static int GetClosestOpacityIndex()
	{
		int result = 0;
		float num = float.MaxValue;
		for (int i = 0; i < OpacityOptions.Length; i++)
		{
			float num2 = Math.Abs(DamageMeterSettings.Opacity - OpacityOptions[i]);
			if (num2 < num)
			{
				num = num2;
				result = i;
			}
		}
		return result;
	}

	private static string GetOpacityButtonText()
	{
		return $"{(int)Math.Round(DamageMeterSettings.Opacity * 100f)}%";
	}

	private static void ApplyHudOpacity()
	{
		if (_panelStyle != null)
		{
			_panelStyle.BgColor = GetHudPanelColor();
		}
		if (_summaryStyle != null)
		{
			_summaryStyle.BgColor = GetHudSummaryColor();
		}
		if (_opacityBtn != null)
		{
			_opacityBtn.Text = GetOpacityButtonText();
			((Control)_opacityBtn).TooltipText = I18n.SettingsOpacity;
		}
		UpdateDashboard();
	}

	private static Button CreateNavButton(string text, int fontSize = 13, Color? color = null, float minWidth = 24f)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Expected O, but got Unknown
		Color val = color ?? GoldColor;
		Button val2 = new Button
		{
			Text = text,
			Flat = true,
			CustomMinimumSize = new Vector2(minWidth, 22f)
		};
		((Control)val2).AddThemeFontSizeOverride("font_size", fontSize);
		((Control)val2).AddThemeColorOverride("font_color", val);
		((Control)val2).AddThemeColorOverride("font_hover_color", new Color(val.R * 1.1f, val.G * 1.1f, val.B * 1.1f, 1f));
		return val2;
	}

	private static void ClearContainer(Node container)
	{
		for (int num = container.GetChildCount(false) - 1; num >= 0; num--)
		{
			Node child = container.GetChild(num, false);
			container.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static void OnPanelGuiInput(InputEvent @event)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Invalid comparison between Unknown and I8
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Invalid comparison between Unknown and I8
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		if (_panel == null)
		{
			return;
		}
		InputEventMouseButton val = (InputEventMouseButton)(object)((@event is InputEventMouseButton) ? @event : null);
		if (val != null && val.Pressed && (long)val.ButtonIndex == 2)
		{
			ShowCategoryMenu();
			return;
		}
		InputEventMouseButton val2 = (InputEventMouseButton)(object)((@event is InputEventMouseButton) ? @event : null);
		if (val2 != null && (long)val2.ButtonIndex == 1)
		{
			if (val2.Pressed)
			{
				_isDragging = true;
				if (_anchored)
				{
					Vector2 globalPosition = ((Control)_panel).GlobalPosition;
					((Control)_panel).AnchorLeft = 0f;
					((Control)_panel).AnchorRight = 0f;
					((Control)_panel).AnchorTop = 0f;
					((Control)_panel).AnchorBottom = 0f;
					((Control)_panel).OffsetLeft = 0f;
					((Control)_panel).OffsetTop = 0f;
					((Control)_panel).OffsetRight = 0f;
					((Control)_panel).OffsetBottom = 0f;
					((Control)_panel).Position = globalPosition;
					_anchored = false;
				}
				_dragOffset = ((Control)_panel).Position - ((InputEventMouse)val2).GlobalPosition;
			}
			else
			{
				_isDragging = false;
				DamageMeterSettings.PanelX = ((Control)_panel).Position.X;
				DamageMeterSettings.PanelY = ((Control)_panel).Position.Y;
				DamageMeterSettings.Save();
			}
		}
		else
		{
			InputEventMouseMotion val3 = (InputEventMouseMotion)(object)((@event is InputEventMouseMotion) ? @event : null);
			if (val3 != null && _isDragging)
			{
				((Control)_panel).Position = ((InputEventMouse)val3).GlobalPosition + _dragOffset;
			}
		}
	}
}

