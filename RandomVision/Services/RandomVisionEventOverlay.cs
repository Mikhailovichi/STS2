using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace RandomVision.Services;

internal static class RandomVisionEventOverlay
{
    private const string OverlayLayerName = "RandomVisionEventPreviewLayer";
    private const string OverlayRootName = "RandomVisionEventPreviewRoot";
    private const string OverlayName = "RandomVisionEventPreview";
    private const string MarginName = "Margin";
    private const string RootName = "Root";
    private const string TitleBarName = "TitleBar";
    private const string TitleName = "Title";
    private const string DragHintName = "DragHint";
    private const string CollapseButtonName = "CollapseButton";
    private const string ScrollName = "Scroll";
    private const string ContentName = "Content";
    private const float BaseWidth = 470f;
    private const float BaseHeight = 360f;
    private const float MinWidth = 430f;
    private const float MaxWidth = 860f;
    private const float MinHeight = 320f;
    private const float MaxHeight = 900f;
    private const float WidthRatio = 0.30f;
    private const float HeightRatio = 0.56f;
    private const float DefaultTopOffset = 40f;
    private const float DefaultRightOffset = 24f;
    private const float ViewportPadding = 12f;
    private const float CollapseButtonBaseSize = 28f;
    private const float CollapsedHeightBase = 58f;
    private const float ZoomStep = 0.08f;
    private const float MinZoom = 0.75f;
    private const float MaxZoom = 1.75f;
    private static Vector2? _lastPanelPosition;
    private static float _panelZoom = 1f;

    public static void Remove(NEventLayout layout)
    {
        MainFile.LogInfo("event-overlay remove");
        layout.GetNodeOrNull<Control>(OverlayName)?.QueueFree();
        NEventRoom.Instance?.GetNodeOrNull<CanvasLayer>(OverlayLayerName)?.QueueFree();
    }

    public static void AttachOrRefresh(NEventLayout layout, EventModel eventModel)
    {
        MainFile.LogInfo($"event-overlay attach-start event={eventModel.Id.Entry} type={eventModel.GetType().Name}");
        var preview = RandomVisionPreviewRegistry.BuildEventPreview(eventModel);
        if (preview.Options.Count == 0)
        {
            MainFile.LogInfo($"event-overlay empty-preview event={eventModel.Id.Entry}; removing overlay");
            Remove(layout);
            return;
        }

        var panel = TryGetOverlayPanel(layout) ?? CreateOverlay(layout);
        RefreshOverlay(panel, preview, layout);
        MainFile.LogInfo($"event-overlay attach-done event={eventModel.Id.Entry} options={preview.Options.Count}");
    }

    private static RandomVisionOverlayPanel CreateOverlay(NEventLayout layout)
    {
        MainFile.LogInfo("event-overlay create-panel");
        var host = EnsureOverlayHost(layout);
        var panelSize = ResolvePanelSize(host, layout);
        var panel = new RandomVisionOverlayPanel
        {
            Name = OverlayName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 120,
            ClipContents = true,
            CustomMinimumSize = panelSize
        };
        panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft, keepOffsets: false);
        panel.Size = panelSize;
        panel.Position = ResolveInitialPanelPosition(host, layout, panelSize);
        panel.ConfigureDrag(34f, ViewportPadding, CollapseButtonBaseSize + 8f);
        panel.PositionCommitted += position => _lastPanelPosition = SnapToPixel(position);
        panel.ZoomRequested += delta =>
        {
            if (!TryAdjustPanelZoom(delta))
            {
                return;
            }

            RefreshPanelGeometry(panel, host, layout);
            _lastPanelPosition = SnapToPixel(panel.Position);
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(1f));

        var margin = new MarginContainer
        {
            Name = MarginName,
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect, keepOffsets: false);

        var root = new VBoxContainer
        {
            Name = RootName,
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect, keepOffsets: false);

        var titleBar = CreateTitleBar();
        titleBar.Name = TitleBarName;

        var scroll = new ScrollContainer
        {
            Name = ScrollName,
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipContents = true,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };

        var content = new VBoxContainer
        {
            Name = ContentName,
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 8);

        scroll.AddChild(content);
        root.AddChild(titleBar);
        root.AddChild(scroll);
        margin.AddChild(root);
        panel.AddChild(margin);
        host.AddChild(panel);

        var collapseButton = panel.GetNode<Button>($"{MarginName}/{RootName}/{TitleBarName}/{CollapseButtonName}");
        collapseButton.Pressed += () =>
        {
            panel.SetCollapsed(!panel.IsCollapsed);
            RefreshPanelGeometry(panel, host, layout);
        };

        host.Resized += () =>
        {
            if (!GodotObject.IsInstanceValid(panel) || !GodotObject.IsInstanceValid(layout))
            {
                return;
            }

            RefreshPanelGeometry(panel, host, layout);
        };

        RefreshPanelGeometry(panel, host, layout);
        return panel;
    }

    private static RandomVisionOverlayPanel? TryGetOverlayPanel(NEventLayout layout)
    {
        var roomPanel = NEventRoom.Instance?
            .GetNodeOrNull<CanvasLayer>(OverlayLayerName)?
            .GetNodeOrNull<Control>(OverlayRootName)?
            .GetNodeOrNull<RandomVisionOverlayPanel>(OverlayName);
        if (roomPanel is not null)
        {
            return roomPanel;
        }

        return layout.GetNodeOrNull<RandomVisionOverlayPanel>(OverlayName);
    }

    private static Control EnsureOverlayHost(NEventLayout layout)
    {
        var room = NEventRoom.Instance;
        if (room is null)
        {
            return layout;
        }

        var layer = room.GetNodeOrNull<CanvasLayer>(OverlayLayerName);
        if (layer is null)
        {
            layer = new CanvasLayer
            {
                Name = OverlayLayerName,
                Layer = 130
            };
            room.AddChild(layer);
        }

        var root = layer.GetNodeOrNull<Control>(OverlayRootName);
        if (root is not null)
        {
            return root;
        }

        root = new Control
        {
            Name = OverlayRootName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);
        return root;
    }

    private static Vector2 ResolveHostSize(Control host, NEventLayout layout)
    {
        if (host.Size != Vector2.Zero)
        {
            return host.Size;
        }

        if (layout.Size != Vector2.Zero)
        {
            return layout.Size;
        }

        return layout.GetViewportRect().Size;
    }

    private static Vector2 ResolvePanelSize(Control host, NEventLayout layout)
    {
        var hostSize = ResolveHostSize(host, layout);
        var availableWidth = Mathf.Max(320f, hostSize.X - (ViewportPadding * 2f));
        var availableHeight = Mathf.Max(260f, hostSize.Y - (ViewportPadding * 2f));

        var zoom = _panelZoom;
        var scaledMinWidth = MinWidth * Mathf.Min(zoom, 1f);
        var scaledMaxWidth = MaxWidth * Mathf.Max(zoom, 1f);
        var scaledMinHeight = MinHeight * Mathf.Min(zoom, 1f);
        var scaledMaxHeight = MaxHeight * Mathf.Max(zoom, 1f);

        var targetWidth = Mathf.Clamp(hostSize.X * WidthRatio * zoom, scaledMinWidth, scaledMaxWidth);
        var targetHeight = Mathf.Clamp(hostSize.Y * HeightRatio * zoom, scaledMinHeight, scaledMaxHeight);

        return SnapToPixel(new Vector2(
            Mathf.Min(targetWidth, availableWidth),
            Mathf.Min(targetHeight, availableHeight)));
    }

    private static bool TryAdjustPanelZoom(float delta)
    {
        var next = Mathf.Clamp(_panelZoom + (delta * ZoomStep), MinZoom, MaxZoom);
        if (Mathf.IsEqualApprox(next, _panelZoom))
        {
            return false;
        }

        _panelZoom = next;
        return true;
    }

    private static Vector2 ResolveInitialPanelPosition(Control host, NEventLayout layout, Vector2 panelSize)
    {
        var hostSize = ResolveHostSize(host, layout);
        if (_lastPanelPosition is { } savedPosition)
        {
            return ClampPanelPosition(savedPosition, hostSize, panelSize);
        }

        return ClampPanelPosition(new Vector2(
            Mathf.Max(ViewportPadding, hostSize.X - panelSize.X - DefaultRightOffset),
            DefaultTopOffset), hostSize, panelSize);
    }

    private static Vector2 ClampPanelPosition(Vector2 position, Vector2 hostSize, Vector2 panelSize)
    {
        var maxX = Mathf.Max(ViewportPadding, hostSize.X - panelSize.X - ViewportPadding);
        var maxY = Mathf.Max(ViewportPadding, hostSize.Y - panelSize.Y - ViewportPadding);
        return SnapToPixel(new Vector2(
            Mathf.Clamp(position.X, ViewportPadding, maxX),
            Mathf.Clamp(position.Y, ViewportPadding, maxY)));
    }

    private static void RefreshPanelGeometry(RandomVisionOverlayPanel panel, Control host, NEventLayout layout)
    {
        var expandedPanelSize = ResolvePanelSize(host, layout);
        var uiScale = ResolveUiScale(expandedPanelSize);
        var panelSize = panel.IsCollapsed
            ? new Vector2(expandedPanelSize.X, ScaleFloat(CollapsedHeightBase, uiScale))
            : expandedPanelSize;

        panel.CustomMinimumSize = panelSize;
        panel.Size = panelSize;
        panel.Position = ClampPanelPosition(panel.Position, ResolveHostSize(host, layout), panelSize);

        panel.ConfigureDrag(
            ScaleFloat(34f, uiScale),
            ViewportPadding,
            ScaleFloat(CollapseButtonBaseSize, uiScale) + ScaleFloat(8f, uiScale));
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(uiScale));

        var scroll = panel.GetNode<ScrollContainer>($"{MarginName}/{RootName}/{ScrollName}");
        scroll.Visible = !panel.IsCollapsed;
        UpdateCollapseButtonVisuals(panel, uiScale);
    }

    private static float ResolveUiScale(Vector2 panelSize)
    {
        var widthScale = panelSize.X / BaseWidth;
        var heightScale = panelSize.Y / BaseHeight;
        return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.92f, 1.55f);
    }

    private static int ScaleInt(float value, float uiScale, int min = 1)
    {
        return Mathf.Max(min, Mathf.RoundToInt(value * uiScale));
    }

    private static float ScaleFloat(float value, float uiScale)
    {
        return Mathf.Max(1f, value * uiScale);
    }

    private static Vector2 SnapToPixel(Vector2 position)
    {
        return new Vector2(Mathf.Round(position.X), Mathf.Round(position.Y));
    }

    private static HBoxContainer CreateTitleBar()
    {
        var titleBar = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(0f, 28f)
        };
        titleBar.AddThemeConstantOverride("separation", 8);

        var title = new Label
        {
            Name = TitleName,
            Text = RandomVisionI18n.Pick("Fate Preview", "\u547d\u8fd0\u9884\u89c8"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        title.AddThemeColorOverride("font_color", new Color("E8C56A"));
        title.AddThemeFontSizeOverride("font_size", 17);

        var dragHint = new Label
        {
            Name = DragHintName,
            Text = RandomVisionI18n.Pick("Drag | Ctrl+Wheel / +/-", "\u62d6\u52a8 | Ctrl+\u6eda\u8f6e / +/-"),
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        dragHint.AddThemeColorOverride("font_color", new Color("8FB8D8"));
        dragHint.AddThemeFontSizeOverride("font_size", 11);

        var collapseButton = new Button
        {
            Name = CollapseButtonName,
            Text = "-",
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = RandomVisionI18n.Pick("Minimize preview", "最小化预览")
        };
        collapseButton.AddThemeColorOverride("font_color", new Color("D7E4F0"));
        collapseButton.AddThemeFontSizeOverride("font_size", 14);
        collapseButton.AddThemeStyleboxOverride("normal", CreateChipStyle(0.18f, 1f));
        collapseButton.AddThemeStyleboxOverride("hover", CreateChipStyle(0.3f, 1f));
        collapseButton.AddThemeStyleboxOverride("pressed", CreateChipStyle(0.42f, 1f));
        collapseButton.AddThemeStyleboxOverride("focus", CreateChipStyle(0.3f, 1f));

        titleBar.AddChild(title);
        titleBar.AddChild(dragHint);
        titleBar.AddChild(collapseButton);
        return titleBar;
    }

    private static void RefreshOverlay(RandomVisionOverlayPanel panel, EventPreviewResult preview, NEventLayout layout)
    {
        MainFile.LogInfo($"event-overlay render title=\"{preview.EventTitle}\" options={preview.Options.Count}");
        if (panel.GetParent() is Control host)
        {
            RefreshPanelGeometry(panel, host, layout);
        }

        var uiScale = ResolveUiScale(panel.Size);
        ApplyResponsiveMetrics(panel, uiScale);

        var title = panel.GetNode<Label>($"{MarginName}/{RootName}/{TitleBarName}/{TitleName}");
        title.Text = string.IsNullOrWhiteSpace(preview.EventTitle)
            ? RandomVisionI18n.Pick("Fate Preview", "\u547d\u8fd0\u9884\u89c8")
            : $"{RandomVisionI18n.Pick("Fate Preview", "\u547d\u8fd0\u9884\u89c8")}  {preview.EventTitle}";

        var content = panel.GetNode<VBoxContainer>($"{MarginName}/{RootName}/{ScrollName}/{ContentName}");
        foreach (var child in content.GetChildren().OfType<Node>().ToArray())
        {
            child.Free();
        }

        foreach (var option in preview.Options)
        {
            content.AddChild(CreateOptionBlock(option, uiScale));
        }
    }

    private static void ApplyResponsiveMetrics(RandomVisionOverlayPanel panel, float uiScale)
    {
        var margin = panel.GetNode<MarginContainer>(MarginName);
        var root = panel.GetNode<VBoxContainer>($"{MarginName}/{RootName}");
        var titleBar = panel.GetNode<HBoxContainer>($"{MarginName}/{RootName}/{TitleBarName}");
        var title = panel.GetNode<Label>($"{MarginName}/{RootName}/{TitleBarName}/{TitleName}");
        var dragHint = panel.GetNode<Label>($"{MarginName}/{RootName}/{TitleBarName}/{DragHintName}");
        var collapseButton = panel.GetNode<Button>($"{MarginName}/{RootName}/{TitleBarName}/{CollapseButtonName}");
        var content = panel.GetNode<VBoxContainer>($"{MarginName}/{RootName}/{ScrollName}/{ContentName}");

        margin.AddThemeConstantOverride("margin_left", ScaleInt(14f, uiScale));
        margin.AddThemeConstantOverride("margin_top", ScaleInt(12f, uiScale));
        margin.AddThemeConstantOverride("margin_right", ScaleInt(14f, uiScale));
        margin.AddThemeConstantOverride("margin_bottom", ScaleInt(12f, uiScale));

        root.AddThemeConstantOverride("separation", ScaleInt(10f, uiScale));
        titleBar.CustomMinimumSize = new Vector2(0f, ScaleFloat(28f, uiScale));
        titleBar.AddThemeConstantOverride("separation", ScaleInt(8f, uiScale));
        title.AddThemeFontSizeOverride("font_size", ScaleInt(17f, uiScale, 12));
        dragHint.AddThemeFontSizeOverride("font_size", ScaleInt(11f, uiScale, 9));
        collapseButton.CustomMinimumSize = new Vector2(
            ScaleFloat(CollapseButtonBaseSize, uiScale),
            ScaleFloat(CollapseButtonBaseSize - 4f, uiScale));
        collapseButton.AddThemeFontSizeOverride("font_size", ScaleInt(14f, uiScale, 11));
        content.AddThemeConstantOverride("separation", ScaleInt(8f, uiScale));
    }

    private static void UpdateCollapseButtonVisuals(RandomVisionOverlayPanel panel, float uiScale)
    {
        var collapseButton = panel.GetNode<Button>($"{MarginName}/{RootName}/{TitleBarName}/{CollapseButtonName}");
        collapseButton.Text = panel.IsCollapsed ? "+" : "-";
        collapseButton.TooltipText = panel.IsCollapsed
            ? RandomVisionI18n.Pick("Expand preview", "展开预览")
            : RandomVisionI18n.Pick("Minimize preview", "最小化预览");
        collapseButton.CustomMinimumSize = new Vector2(
            ScaleFloat(CollapseButtonBaseSize, uiScale),
            ScaleFloat(CollapseButtonBaseSize - 4f, uiScale));
    }

    private static PanelContainer CreateOptionBlock(EventOptionPreview option, float uiScale)
    {
        var block = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipContents = true
        };
        block.AddThemeStyleboxOverride("panel", CreateOptionStyle(uiScale));

        var margin = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        margin.AddThemeConstantOverride("margin_left", ScaleInt(10f, uiScale));
        margin.AddThemeConstantOverride("margin_top", ScaleInt(9f, uiScale));
        margin.AddThemeConstantOverride("margin_right", ScaleInt(10f, uiScale));
        margin.AddThemeConstantOverride("margin_bottom", ScaleInt(9f, uiScale));

        var root = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", ScaleInt(6f, uiScale));

        var header = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        header.AddThemeConstantOverride("separation", ScaleInt(8f, uiScale));

        var title = new Label
        {
            Text = option.Title,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        title.AddThemeColorOverride("font_color", new Color("F5F0DE"));
        title.AddThemeFontSizeOverride("font_size", ScaleInt(14f, uiScale, 11));

        var status = new Label
        {
            Text = CoverageText(option.Coverage),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            CustomMinimumSize = new Vector2(ScaleFloat(74f, uiScale), 0f),
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        status.AddThemeColorOverride("font_color", CoverageColor(option.Coverage));
        status.AddThemeFontSizeOverride("font_size", ScaleInt(12f, uiScale, 10));

        header.AddChild(title);
        header.AddChild(status);
        root.AddChild(header);

        foreach (var line in option.Lines)
        {
            root.AddChild(CreateBodyLabel(line, uiScale));
        }

        if (option.Entities.Count > 0)
        {
            root.AddChild(CreateEntitySection(option.Entities, uiScale));
        }

        margin.AddChild(root);
        block.AddChild(margin);
        return block;
    }

    private static Control CreateEntitySection(IReadOnlyList<EventPreviewEntity> entities, float uiScale)
    {
        var root = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", ScaleInt(4f, uiScale));

        var hint = new Label
        {
            Text = RandomVisionI18n.Pick("Click to inspect details", "\u53ef\u70b9\u51fb\u67e5\u770b\u8be6\u60c5"),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeColorOverride("font_color", new Color("8DB1D4"));
        hint.AddThemeFontSizeOverride("font_size", ScaleInt(10f, uiScale, 9));
        root.AddChild(hint);

        foreach (var entity in entities)
        {
            root.AddChild(CreateEntityChip(entity, uiScale));
        }

        return root;
    }

    private static Button CreateEntityChip(EventPreviewEntity entity, float uiScale)
    {
        var button = new Button
        {
            Text = entity.Label,
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = HorizontalAlignment.Left,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        button.AddThemeFontSizeOverride("font_size", ScaleInt(11f, uiScale, 9));
        button.AddThemeColorOverride("font_color", new Color("BEE3FF"));
        button.AddThemeStyleboxOverride("normal", CreateChipStyle(0.2f, uiScale));
        button.AddThemeStyleboxOverride("hover", CreateChipStyle(0.34f, uiScale));
        button.AddThemeStyleboxOverride("pressed", CreateChipStyle(0.42f, uiScale));
        button.AddThemeStyleboxOverride("focus", CreateChipStyle(0.34f, uiScale));

        button.MouseEntered += () => ShowEntityHover(button, entity);
        button.MouseExited += () => NHoverTipSet.Remove(button);
        button.Pressed += () => ShowEntityHover(button, entity);

        return button;
    }

    private static void ShowEntityHover(Control owner, EventPreviewEntity entity)
    {
        if (entity.HoverTips.Count == 0)
        {
            return;
        }

        NHoverTipSet.Remove(owner);
        NHoverTipSet.CreateAndShow(owner, entity.HoverTips, HoverTip.GetHoverTipAlignment(owner));
    }

    private static Label CreateBodyLabel(string text, float uiScale)
    {
        var label = new Label
        {
            Text = $"\u2022 {text}",
            AutowrapMode = TextServer.AutowrapMode.Arbitrary,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeColorOverride("font_color", new Color("E8EEF8"));
        label.AddThemeFontSizeOverride("font_size", ScaleInt(13f, uiScale, 10));
        return label;
    }

    private static string CoverageText(PreviewCoverage coverage)
    {
        return coverage switch
        {
            PreviewCoverage.Complete => RandomVisionI18n.Pick("Complete", "\u5b8c\u6574"),
            PreviewCoverage.PartialNeedsInput => RandomVisionI18n.Pick("Partial", "\u90e8\u5206"),
            _ => RandomVisionI18n.Pick("Visible", "\u5df2\u516c\u5f00")
        };
    }

    private static Color CoverageColor(PreviewCoverage coverage)
    {
        return coverage switch
        {
            PreviewCoverage.Complete => new Color("8ED6A6"),
            PreviewCoverage.PartialNeedsInput => new Color("F2C66D"),
            _ => new Color("8FB8D8")
        };
    }

    private static StyleBoxFlat CreatePanelStyle(float uiScale)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.08f, 0.11f, 0.93f),
            BorderColor = new Color("B8924C")
        };
        style.SetBorderWidthAll(ScaleInt(2f, uiScale));
        style.SetCornerRadiusAll(ScaleInt(10f, uiScale));
        return style;
    }

    private static StyleBoxFlat CreateOptionStyle(float uiScale)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.13f, 0.18f, 0.88f),
            BorderColor = new Color(0.33f, 0.4f, 0.49f, 0.75f)
        };
        style.SetBorderWidthAll(ScaleInt(1f, uiScale));
        style.SetCornerRadiusAll(ScaleInt(8f, uiScale));
        return style;
    }

    private static StyleBoxFlat CreateChipStyle(float alpha, float uiScale)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.23f, 0.31f, alpha),
            BorderColor = new Color("5B86A6")
        };
        style.SetBorderWidthAll(ScaleInt(1f, uiScale));
        style.SetCornerRadiusAll(ScaleInt(6f, uiScale));
        style.ContentMarginLeft = ScaleFloat(8f, uiScale);
        style.ContentMarginRight = ScaleFloat(8f, uiScale);
        style.ContentMarginTop = ScaleFloat(4f, uiScale);
        style.ContentMarginBottom = ScaleFloat(4f, uiScale);
        return style;
    }
}
