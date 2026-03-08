using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using PartyObserver.Networking;
using PartyObserver.Services;

namespace PartyObserver.UI;

public partial class PartyObserverOverlay : CanvasLayer
{
    private sealed class AnchorBinding
    {
        public required NMultiplayerPlayerState State { get; init; }

        public required Action MouseEntered { get; init; }

        public required Action MouseExited { get; init; }
    }

    private readonly Dictionary<ulong, AnchorBinding> _anchors = [];
    private readonly Dictionary<string, Texture2D?> _textureCache = [];

    private PartyObserverSettings _settings = new();
    private Control? _root;
    private PanelContainer? _hoverPanel;
    private Label? _hoverHeadingLabel;
    private Label? _hoverSummaryLabel;
    private HBoxContainer? _hoverPreviewRow;
    private Label? _hoverHintLabel;
    private PanelContainer? _detailPanel;
    private Label? _detailHeadingLabel;
    private Label? _detailScreenLabel;
    private Label? _detailDescriptionLabel;
    private VBoxContainer? _detailOptionsList;
    private Button? _detailCloseButton;
    private string _languageToken = string.Empty;
    private ulong _hoverPlayerId;
    private ulong _detailPlayerId;

    internal void Initialize(PartyObserverSettings settings)
    {
        _settings = settings;
        _settings.Normalize();
        ApplyPanelOpacity();
    }

    internal void RegisterPlayerState(NMultiplayerPlayerState playerState)
    {
        var localPlayerId = RunManager.Instance.NetService?.NetId;
        if (playerState.Player.NetId == localPlayerId)
        {
            return;
        }

        UnregisterPlayerState(playerState);

        var playerId = playerState.Player.NetId;
        var binding = new AnchorBinding
        {
            State = playerState,
            MouseEntered = () => OnAnchorMouseEntered(playerId),
            MouseExited = () => OnAnchorMouseExited(playerId)
        };

        playerState.Hitbox.MouseEntered += binding.MouseEntered;
        playerState.Hitbox.MouseExited += binding.MouseExited;
        _anchors[playerId] = binding;

        if (_hoverPlayerId == playerId)
        {
            RefreshHoverCard();
        }

        if (_detailPlayerId == playerId)
        {
            RefreshDetailPanel();
        }
    }

    internal void UnregisterPlayerState(NMultiplayerPlayerState playerState)
    {
        var playerId = playerState.Player.NetId;
        if (!_anchors.Remove(playerId, out var binding))
        {
            return;
        }

        if (GodotObject.IsInstanceValid(binding.State) && binding.State.Hitbox is not null)
        {
            binding.State.Hitbox.MouseEntered -= binding.MouseEntered;
            binding.State.Hitbox.MouseExited -= binding.MouseExited;
        }

        if (_hoverPlayerId == playerId)
        {
            _hoverPlayerId = 0;
            if (_hoverPanel is not null)
            {
                _hoverPanel.Visible = false;
            }
        }

        if (_detailPlayerId == playerId)
        {
            HideAllPanels();
        }
    }

    public override void _Ready()
    {
        Layer = 131;
        CreateRoot();
        CreateHoverPanel();
        CreateDetailPanel();
        ApplyPanelOpacity();
        RefreshLocalizedChrome();
        PartyObserverRegistry.SnapshotChanged += OnSnapshotChanged;

        if (RunManager.Instance.InputSynchronizer is not null)
        {
            RunManager.Instance.InputSynchronizer.ScreenChanged += OnScreenChanged;
        }

        SetProcess(true);
    }

    public override void _ExitTree()
    {
        PartyObserverRegistry.SnapshotChanged -= OnSnapshotChanged;

        if (RunManager.Instance.InputSynchronizer is not null)
        {
            RunManager.Instance.InputSynchronizer.ScreenChanged -= OnScreenChanged;
        }

        foreach (var binding in _anchors.Values.ToList())
        {
            if (GodotObject.IsInstanceValid(binding.State) && binding.State.Hitbox is not null)
            {
                binding.State.Hitbox.MouseEntered -= binding.MouseEntered;
                binding.State.Hitbox.MouseExited -= binding.MouseExited;
            }
        }

        _anchors.Clear();
    }

    public override void _Process(double delta)
    {
        _ = delta;
        RefreshLocalizedChrome();

        var activePlayerId = _detailPlayerId != 0 ? _detailPlayerId : _hoverPlayerId;
        if (activePlayerId == 0)
        {
            return;
        }

        var activeState = GetPlayerState(activePlayerId);
        if (activeState is null)
        {
            HideAllPanels();
            return;
        }

        if (_hoverPanel?.Visible == true)
        {
            PositionPanel(_hoverPanel, activeState, new Vector2(18f, 0f));
        }

        if (_detailPanel?.Visible == true)
        {
            PositionPanel(_detailPanel, activeState, new Vector2(18f, 0f));
        }

        if ((_hoverPanel?.Visible == true || _detailPanel?.Visible == true) &&
            !ShouldKeepPanelClusterVisible(activeState))
        {
            HideAllPanels();
        }
    }

    private void CreateRoot()
    {
        _root = new Control
        {
            Name = "PartyObserverRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
    }

    private void CreateHoverPanel()
    {
        if (_root is null)
        {
            return;
        }

        _hoverPanel = new PanelContainer
        {
            Name = "PartyObserverHoverCard",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(248f, 0f)
        };
        _hoverPanel.AddThemeStyleboxOverride("panel", CreateCompactPanelStyle());
        _hoverPanel.GuiInput += OnHoverPanelGuiInput;
        _root.AddChild(_hoverPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        _hoverPanel.AddChild(margin);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);

        _hoverHeadingLabel = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _hoverHeadingLabel.AddThemeFontSizeOverride("font_size", 13);
        _hoverHeadingLabel.AddThemeColorOverride("font_color", Colors.White);
        content.AddChild(_hoverHeadingLabel);

        _hoverSummaryLabel = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _hoverSummaryLabel.AddThemeFontSizeOverride("font_size", 11);
        _hoverSummaryLabel.AddThemeColorOverride("font_color", new Color("D7E4F0"));
        content.AddChild(_hoverSummaryLabel);

        _hoverPreviewRow = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _hoverPreviewRow.AddThemeConstantOverride("separation", 6);
        content.AddChild(_hoverPreviewRow);

        _hoverHintLabel = new Label
        {
            Text = PartyObserverText.HoverHint(),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _hoverHintLabel.AddThemeFontSizeOverride("font_size", 10);
        _hoverHintLabel.AddThemeColorOverride("font_color", new Color("8DB1D4"));
        content.AddChild(_hoverHintLabel);
    }

    private void CreateDetailPanel()
    {
        if (_root is null)
        {
            return;
        }

        _detailPanel = new PanelContainer
        {
            Name = "PartyObserverDetailPanel",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(428f, 0f)
        };
        _detailPanel.AddThemeStyleboxOverride("panel", CreateDetailPanelStyle());
        _root.AddChild(_detailPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        _detailPanel.AddChild(margin);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        margin.AddChild(content);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        content.AddChild(header);

        _detailHeadingLabel = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _detailHeadingLabel.AddThemeFontSizeOverride("font_size", 14);
        _detailHeadingLabel.AddThemeColorOverride("font_color", Colors.White);
        header.AddChild(_detailHeadingLabel);

        var closeButton = new Button
        {
            Text = PartyObserverText.Close(),
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        closeButton.Pressed += HideDetailPanel;
        closeButton.AddThemeColorOverride("font_color", new Color("9FC5E9"));
        header.AddChild(closeButton);
        _detailCloseButton = closeButton;

        _detailScreenLabel = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _detailScreenLabel.AddThemeFontSizeOverride("font_size", 11);
        _detailScreenLabel.AddThemeColorOverride("font_color", new Color("9FC0DD"));
        content.AddChild(_detailScreenLabel);

        _detailDescriptionLabel = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _detailDescriptionLabel.AddThemeFontSizeOverride("font_size", 11);
        _detailDescriptionLabel.AddThemeColorOverride("font_color", new Color("D8E6F2"));
        content.AddChild(_detailDescriptionLabel);

        var scrollContainer = new ScrollContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0f, 280f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        content.AddChild(scrollContainer);

        _detailOptionsList = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        _detailOptionsList.AddThemeConstantOverride("separation", 8);
        _detailOptionsList.CustomMinimumSize = new Vector2(392f, 0f);
        scrollContainer.AddChild(_detailOptionsList);
    }

    private void ApplyPanelOpacity()
    {
        if (_hoverPanel is not null)
        {
            _hoverPanel.SelfModulate = new Color(1f, 1f, 1f, _settings.PanelOpacity);
        }

        if (_detailPanel is not null)
        {
            _detailPanel.SelfModulate = new Color(1f, 1f, 1f, _settings.PanelOpacity);
        }
    }

    private void OnAnchorMouseEntered(ulong playerId)
    {
        _hoverPlayerId = playerId;

        if (_detailPlayerId != 0 && _detailPlayerId != playerId)
        {
            _detailPlayerId = 0;
            if (_detailPanel is not null)
            {
                _detailPanel.Visible = false;
            }
        }

        RefreshHoverCard();

        if (_hoverPanel is not null)
        {
            _hoverPanel.Visible = _detailPlayerId != playerId;
        }
    }

    private void OnAnchorMouseExited(ulong playerId)
    {
        _ = playerId;
    }

    private void OnHoverPanelGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButtonEvent &&
            mouseButtonEvent.ButtonIndex == MouseButton.Left &&
            mouseButtonEvent.Pressed &&
            _hoverPlayerId != 0)
        {
            ShowDetailPanel(_hoverPlayerId);
            GetViewport().SetInputAsHandled();
        }
    }

    private void ShowDetailPanel(ulong playerId)
    {
        _hoverPlayerId = playerId;
        _detailPlayerId = playerId;
        RefreshDetailPanel();
        if (_detailPanel is not null)
        {
            _detailPanel.Visible = true;
        }

        if (_hoverPanel is not null && _hoverPlayerId == playerId)
        {
            _hoverPanel.Visible = false;
        }
    }

    private void HideDetailPanel()
    {
        _detailPlayerId = 0;

        if (_detailPanel is not null)
        {
            _detailPanel.Visible = false;
        }

        var hoverState = GetPlayerState(_hoverPlayerId);
        if (_hoverPanel is not null && hoverState is not null)
        {
            _hoverPanel.Visible = ShouldKeepPanelClusterVisible(hoverState);
        }
    }

    private void RefreshHoverCard()
    {
        var playerState = GetPlayerState(_hoverPlayerId);
        if (playerState is null || _hoverHeadingLabel is null || _hoverSummaryLabel is null)
        {
            return;
        }

        var player = playerState.Player;
        var snapshot = PartyObserverRegistry.GetSnapshot(player.NetId);

        _hoverHeadingLabel.Text = BuildPlayerHeading(player);
        _hoverSummaryLabel.Text = BuildHoverSummary(player.NetId, snapshot);
        PopulateHoverPreview(snapshot);
    }

    private void RefreshDetailPanel()
    {
        if (_detailHeadingLabel is null || _detailScreenLabel is null || _detailDescriptionLabel is null || _detailOptionsList is null)
        {
            return;
        }

        var playerState = GetPlayerState(_detailPlayerId);
        if (playerState is null)
        {
            HideDetailPanel();
            return;
        }

        var player = playerState.Player;
        var snapshot = PartyObserverRegistry.GetSnapshot(player.NetId);

        _detailHeadingLabel.Text = BuildPlayerHeading(player);
        _detailScreenLabel.Text = PartyObserverText.FormatCurrentScreen(GetPlayerScreenLabel(player.NetId));

        if (snapshot is null)
        {
            _detailDescriptionLabel.Text = PartyObserverText.NoSnapshot();
            PopulateOptionCards(null);
            return;
        }

        var detailLines = new List<string>();
        var summaryTitle = GetSnapshotSummaryTitle(snapshot);
        if (!string.IsNullOrWhiteSpace(summaryTitle))
        {
            detailLines.Add(summaryTitle);
        }

        var summaryDescription = GetSnapshotSummaryDescription(snapshot);
        if (!string.IsNullOrWhiteSpace(summaryDescription))
        {
            detailLines.Add(summaryDescription);
        }

        _detailDescriptionLabel.Text = detailLines.Count == 0
            ? PartyObserverText.NoExtraDetails()
            : string.Join("\n", detailLines);

        PopulateOptionCards(snapshot);
    }

    private void PopulateHoverPreview(PartyObserverChoiceSnapshot? snapshot)
    {
        if (_hoverPreviewRow is null)
        {
            return;
        }

        foreach (var child in _hoverPreviewRow.GetChildren())
        {
            child.QueueFree();
        }

        if (snapshot is null)
        {
            _hoverPreviewRow.Visible = false;
            return;
        }

        foreach (var option in snapshot.Options.Take(3))
        {
            _hoverPreviewRow.AddChild(CreatePreviewChip(option));
        }

        _hoverPreviewRow.Visible = _hoverPreviewRow.GetChildCount() > 0;
    }

    private void PopulateOptionCards(PartyObserverChoiceSnapshot? snapshot)
    {
        if (_detailOptionsList is null)
        {
            return;
        }

        foreach (var child in _detailOptionsList.GetChildren())
        {
            child.QueueFree();
        }

        if (snapshot is null || snapshot.Options.Count == 0)
        {
            _detailOptionsList.AddChild(CreateEmptyStateLabel(
                snapshot is null
                    ? PartyObserverText.NoSyncedOptions()
                    : PartyObserverText.SnapshotHasNoVisibleOptions()));
            return;
        }

        foreach (var option in snapshot.Options)
        {
            _detailOptionsList.AddChild(CreateOptionCard(option));
        }
    }

    private Control CreatePreviewChip(PartyObserverChoiceOption option)
    {
        var texture = LoadTexture(option.ImagePath);
        if (texture is not null)
        {
            var textureRect = new TextureRect
            {
                Texture = texture,
                CustomMinimumSize = GetPreviewImageSize(option),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };

            var frame = new PanelContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            frame.AddThemeStyleboxOverride("panel", CreatePreviewFrameStyle());
            frame.AddChild(textureRect);
            return frame;
        }

        var fallbackLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(option.Tag) ? "?" : PartyObserverText.LocalizeTag(option.Tag),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        fallbackLabel.AddThemeFontSizeOverride("font_size", 10);
        fallbackLabel.AddThemeColorOverride("font_color", new Color("CBE1F4"));
        return fallbackLabel;
    }

    private Control CreateOptionCard(PartyObserverChoiceOption option)
    {
        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SelfModulate = option.IsDisabled ? new Color(1f, 1f, 1f, 0.65f) : Colors.White
        };
        panel.AddThemeStyleboxOverride("panel", CreateOptionStyle(option));

        var margin = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var row = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride("separation", 10);
        margin.AddChild(row);

        var texture = LoadTexture(option.ImagePath);
        if (texture is not null)
        {
            var textureRect = new TextureRect
            {
                Texture = texture,
                CustomMinimumSize = GetOptionImageSize(option),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            row.AddChild(textureRect);
        }

        var content = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 4);
        row.AddChild(content);

        var titleLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(option.Tag)
                ? option.Title
                : $"{PartyObserverText.LocalizeTag(option.Tag)} - {option.Title}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 12);
        titleLabel.AddThemeColorOverride("font_color", Colors.White);
        content.AddChild(titleLabel);

        if (!string.IsNullOrWhiteSpace(option.Subtitle))
        {
            var subtitleLabel = new Label
            {
                Text = option.Subtitle,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            subtitleLabel.AddThemeFontSizeOverride("font_size", 10);
            subtitleLabel.AddThemeColorOverride("font_color", new Color("9BC0DD"));
            content.AddChild(subtitleLabel);
        }

        if (!string.IsNullOrWhiteSpace(option.Description))
        {
            var descriptionLabel = new Label
            {
                Text = option.Description,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            descriptionLabel.AddThemeFontSizeOverride("font_size", 10);
            descriptionLabel.AddThemeColorOverride("font_color", new Color("D6E4F0"));
            content.AddChild(descriptionLabel);
        }

        return panel;
    }

    private Label CreateEmptyStateLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", new Color("B8CBDE"));
        return label;
    }

    private string BuildHoverSummary(ulong playerId, PartyObserverChoiceSnapshot? snapshot)
    {
        var lines = new List<string>
        {
            PartyObserverText.FormatCurrentScreen(GetPlayerScreenLabel(playerId))
        };

        if (snapshot is null)
        {
            lines.Add(PartyObserverText.NoSyncedChoiceDetails());
            return string.Join("\n", lines);
        }

        var summaryTitle = GetSnapshotSummaryTitle(snapshot);
        if (!string.IsNullOrWhiteSpace(summaryTitle))
        {
            lines.Add(summaryTitle);
        }

        lines.Add(snapshot.Options.Count > 0
            ? PartyObserverText.FormatSyncedOptionsAvailable(snapshot.Options.Count)
            : PartyObserverText.NoVisibleOptionsInSnapshot());

        return string.Join("\n", lines);
    }

    private string BuildPlayerHeading(Player player)
    {
        if (RunManager.Instance.DebugOnlyGetState() is IPlayerCollection playerCollection)
        {
            var slotIndex = playerCollection.GetPlayerSlotIndex(player);
            if (slotIndex >= 0)
            {
                return $"P{slotIndex + 1} {player.Character.Title.GetRawText()}";
            }
        }

        return player.Character.Title.GetRawText();
    }

    private string GetPlayerScreenLabel(ulong playerId)
    {
        var snapshot = PartyObserverRegistry.GetSnapshot(playerId);
        if (snapshot is not null && snapshot.Kind != PartyObserverChoiceSnapshotKind.None)
        {
            return snapshot.Kind.GetDisplayName();
        }

        if (!string.IsNullOrWhiteSpace(snapshot?.ScreenLabel))
        {
            return snapshot.ScreenLabel;
        }

        return RunManager.Instance.InputSynchronizer?.GetScreenType(playerId).GetDisplayName() ?? PartyObserverText.Unknown();
    }

    private NMultiplayerPlayerState? GetPlayerState(ulong playerId)
    {
        if (playerId == 0 || !_anchors.TryGetValue(playerId, out var binding))
        {
            return null;
        }

        if (!GodotObject.IsInstanceValid(binding.State) || !binding.State.IsInsideTree())
        {
            _anchors.Remove(playerId);
            return null;
        }

        return binding.State;
    }

    private Texture2D? LoadTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (_textureCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        Texture2D? texture = null;
        if (ResourceLoader.Exists(path))
        {
            texture = ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
        }

        _textureCache[path] = texture;
        return texture;
    }

    private void RefreshLocalizedChrome()
    {
        var languageToken = PartyObserverText.CurrentLanguageToken();
        if (_languageToken == languageToken)
        {
            return;
        }

        _languageToken = languageToken;

        if (_hoverHintLabel is not null)
        {
            _hoverHintLabel.Text = PartyObserverText.HoverHint();
        }

        if (_detailCloseButton is not null)
        {
            _detailCloseButton.Text = PartyObserverText.Close();
        }

        if (_hoverPlayerId != 0)
        {
            RefreshHoverCard();
        }

        if (_detailPlayerId != 0)
        {
            RefreshDetailPanel();
        }
    }

    private void HideAllPanels()
    {
        _hoverPlayerId = 0;
        _detailPlayerId = 0;

        if (_hoverPanel is not null)
        {
            _hoverPanel.Visible = false;
        }

        if (_detailPanel is not null)
        {
            _detailPanel.Visible = false;
        }
    }

    private void PositionPanel(Control panel, Control anchor, Vector2 offset)
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var preferredPosition = anchor.GlobalPosition + new Vector2(anchor.Size.X + offset.X, offset.Y);
        var clampedX = preferredPosition.X;
        var clampedY = preferredPosition.Y;

        if (clampedX + panel.Size.X > viewportSize.X - 8f)
        {
            clampedX = anchor.GlobalPosition.X - panel.Size.X - 12f;
        }

        clampedX = Mathf.Clamp(clampedX, 8f, Math.Max(8f, viewportSize.X - panel.Size.X - 8f));
        clampedY = Mathf.Clamp(clampedY, 8f, Math.Max(8f, viewportSize.Y - panel.Size.Y - 8f));
        panel.GlobalPosition = new Vector2(clampedX, clampedY);
    }

    private bool ShouldKeepPanelClusterVisible(Control anchor)
    {
        var mousePosition = GetViewport().GetMousePosition();
        var clusterRect = ExpandRect(anchor.GetGlobalRect(), 28f);

        if (_hoverPanel?.Visible == true)
        {
            clusterRect = clusterRect.Merge(ExpandRect(_hoverPanel.GetGlobalRect(), 36f));
        }

        if (_detailPanel?.Visible == true)
        {
            clusterRect = clusterRect.Merge(ExpandRect(_detailPanel.GetGlobalRect(), 40f));
        }

        return clusterRect.HasPoint(mousePosition);
    }

    private static Rect2 ExpandRect(Rect2 rect, float padding)
    {
        var offset = new Vector2(padding, padding);
        return new Rect2(rect.Position - offset, rect.Size + offset * 2f);
    }

    private static string GetSnapshotSummaryTitle(PartyObserverChoiceSnapshot snapshot)
    {
        return snapshot.Kind switch
        {
            PartyObserverChoiceSnapshotKind.Rewards => PartyObserverText.ReviewingRewards(),
            PartyObserverChoiceSnapshotKind.CardRewardSelection => PartyObserverText.ChoosingCard(),
            _ => snapshot.Title
        };
    }

    private static string GetSnapshotSummaryDescription(PartyObserverChoiceSnapshot snapshot)
    {
        return snapshot.Kind switch
        {
            PartyObserverChoiceSnapshotKind.Rewards => PartyObserverText.FormatRewardsCount(snapshot.Options.Count),
            PartyObserverChoiceSnapshotKind.CardRewardSelection => PartyObserverText.FormatCardOptionsCount(snapshot.Options.Count),
            PartyObserverChoiceSnapshotKind.EventChoices => PartyObserverText.FormatEventOptionsCount(snapshot.Options.Count),
            _ => snapshot.Description
        };
    }

    private static Vector2 GetPreviewImageSize(PartyObserverChoiceOption option)
    {
        return PartyObserverText.IsCardTag(option.Tag) ? new Vector2(34f, 46f) : new Vector2(36f, 36f);
    }

    private static Vector2 GetOptionImageSize(PartyObserverChoiceOption option)
    {
        return PartyObserverText.IsCardTag(option.Tag) ? new Vector2(78f, 104f) : new Vector2(56f, 56f);
    }

    private void OnSnapshotChanged(ulong playerId)
    {
        if (_hoverPlayerId == playerId)
        {
            RefreshHoverCard();
        }

        if (_detailPlayerId == playerId)
        {
            RefreshDetailPanel();
        }
    }

    private void OnScreenChanged(ulong playerId, MegaCrit.Sts2.Core.Entities.Multiplayer.NetScreenType _)
    {
        if (_hoverPlayerId == playerId)
        {
            RefreshHoverCard();
        }

        if (_detailPlayerId == playerId)
        {
            RefreshDetailPanel();
        }
    }

    private static StyleBoxFlat CreateCompactPanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.09f, 0.14f, 0.97f),
            BorderColor = new Color(0.34f, 0.49f, 0.63f, 0.82f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ShadowColor = new Color(0f, 0f, 0f, 0.24f),
            ShadowSize = 5
        };
    }

    private static StyleBoxFlat CreateDetailPanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.08f, 0.12f, 0.98f),
            BorderColor = new Color(0.36f, 0.5f, 0.64f, 0.88f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            ShadowColor = new Color(0f, 0f, 0f, 0.28f),
            ShadowSize = 7
        };
    }

    private static StyleBoxFlat CreatePreviewFrameStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.11f, 0.15f, 0.21f, 0.92f),
            BorderColor = new Color(0.33f, 0.45f, 0.58f, 0.7f),
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

    private static StyleBoxFlat CreateOptionStyle(PartyObserverChoiceOption option)
    {
        var borderColor = option.IsDisabled
            ? new Color("A78A8A")
            : PartyObserverText.IsRelicTag(option.Tag)
                ? new Color("E2C26F")
                : PartyObserverText.IsCardTag(option.Tag)
                    ? new Color("6CA7D8")
                    : PartyObserverText.IsProceedTag(option.Tag)
                        ? new Color("B7F0A1")
                        : new Color("6A87A6");

        return new StyleBoxFlat
        {
            BgColor = new Color(0.11f, 0.14f, 0.2f, 0.95f),
            BorderColor = borderColor,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10
        };
    }
}
