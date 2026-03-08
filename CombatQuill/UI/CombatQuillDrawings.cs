using System.Linq;
using CombatQuill.Networking;
using CombatQuill.Services;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace CombatQuill.UI;

public partial class CombatQuillDrawings : Control
{
    private sealed class DrawingState
    {
        public DrawingMode? OverrideDrawingMode;

        public DrawingMode DrawingMode;

        public ulong PlayerId;

        public Line2D? CurrentLine;

        public required SubViewportContainer DrawSurface;

        public required SubViewport DrawViewport;

        public bool IsDrawing => CurrentLine is not null;

        public DrawingMode CurrentDrawingMode => OverrideDrawingMode ?? DrawingMode;
    }

    private const int MinUpdateMsec = 50;
    private const float MinimumPointDistanceSquared = 4f;
    private const string LineDrawScenePath = "res://scenes/screens/map/map_line_draw.tscn";
    private const string LineEraseScenePath = "res://scenes/screens/map/map_line_erase.tscn";
    private const string DrawingCursorPath = "res://images/packed/common_ui/cursor_quill.png";
    private const string DrawingCursorTiltedPath = "res://images/packed/common_ui/cursor_quill_tilted.png";
    private const string ErasingCursorPath = "res://images/packed/common_ui/cursor_eraser.png";
    private const string ErasingCursorTiltedPath = "res://images/packed/common_ui/cursor_eraser_tilted.png";

    private static readonly Vector2 DrawingCursorHotspot = new(2f, 56f);
    private static readonly Vector2 ErasingCursorHotspot = new(24f, 58f);

    private readonly List<DrawingState> _drawingStates = [];

    private INetGameService? _netService;

    private IPlayerCollection? _playerCollection;

    private PeerInputSynchronizer? _inputSynchronizer;

    private NetScreenType _screenType;

    private PackedScene? _lineDrawScene;

    private PackedScene? _lineEraseScene;

    private NCursorManager? _cursorManager;

    private Material? _eraserMaterial;

    private CombatQuillDrawingMessage? _queuedMessage;

    private ulong _lastMessageMsec;

    private Task? _sendMessageTask;

    public override void _Ready()
    {
        _lineDrawScene = PreloadManager.Cache.GetScene(LineDrawScenePath);
        _lineEraseScene = PreloadManager.Cache.GetScene(LineEraseScenePath);
        _cursorManager = NGame.Instance?.CursorManager;

        var eraseProbe = _lineEraseScene.Instantiate<Line2D>();
        _eraserMaterial = eraseProbe.Material;
        eraseProbe.QueueFree();
        UpdateDrawingSurfaceSizes();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            UpdateDrawingSurfaceSizes();
        }
    }

    public override void _ExitTree()
    {
        if (_netService is not null)
        {
            _netService.UnregisterMessageHandler<CombatQuillDrawingMessage>(HandleDrawingMessage);
            _netService.UnregisterMessageHandler<CombatQuillClearDrawingsMessage>(HandleClearDrawingsMessage);
            _netService.UnregisterMessageHandler<CombatQuillModeChangedMessage>(HandleModeChangedMessage);
        }

        if (_inputSynchronizer is not null)
        {
            _inputSynchronizer.ScreenChanged -= OnPlayerScreenChanged;
        }
    }

    public void Initialize(
        INetGameService netService,
        IPlayerCollection playerCollection,
        PeerInputSynchronizer inputSynchronizer,
        NetScreenType screenType)
    {
        _netService = netService;
        _playerCollection = playerCollection;
        _inputSynchronizer = inputSynchronizer;
        _screenType = screenType;

        _netService.RegisterMessageHandler<CombatQuillDrawingMessage>(HandleDrawingMessage);
        _netService.RegisterMessageHandler<CombatQuillClearDrawingsMessage>(HandleClearDrawingsMessage);
        _netService.RegisterMessageHandler<CombatQuillModeChangedMessage>(HandleModeChangedMessage);
        _inputSynchronizer.ScreenChanged += OnPlayerScreenChanged;
    }

    public void BeginLineLocal(Vector2 position, DrawingMode? overrideDrawingMode)
    {
        EnsureInitialized();

        BeginLine(GetDrawingStateForPlayer(_netService!.NetId), position, overrideDrawingMode);
        var ev = new CombatQuillDrawingEvent
        {
            Type = CombatQuillDrawingEventType.BeginLine,
            Position = ToNetPosition(position),
            OverrideDrawingMode = overrideDrawingMode
        };
        QueueOrSendEvent(ev);
    }

    public void UpdateCurrentLinePositionLocal(Vector2 position)
    {
        EnsureInitialized();

        var localState = GetDrawingStateForPlayer(_netService!.NetId);
        UpdateCurrentLinePosition(localState, position);
        var ev = new CombatQuillDrawingEvent
        {
            Type = CombatQuillDrawingEventType.ContinueLine,
            Position = ToNetPosition(position),
            OverrideDrawingMode = localState.OverrideDrawingMode
        };
        QueueOrSendEvent(ev);
    }

    public void StopLineLocal()
    {
        EnsureInitialized();

        StopDrawingLine(GetDrawingStateForPlayer(_netService!.NetId));
        var ev = new CombatQuillDrawingEvent
        {
            Type = CombatQuillDrawingEventType.EndLine
        };
        QueueOrSendEvent(ev);
    }

    public void SetDrawingModeLocal(DrawingMode drawingMode)
    {
        EnsureInitialized();

        SetDrawingMode(GetDrawingStateForPlayer(_netService!.NetId), drawingMode);
        _netService.SendMessage(new CombatQuillModeChangedMessage
        {
            DrawingMode = drawingMode
        });
        UpdateLocalCursor();
    }

    public void ClearDrawnLinesLocal()
    {
        EnsureInitialized();

        ClearAllLinesForPlayer(GetDrawingStateForPlayer(_netService!.NetId));
        UpdateLocalCursor();
        _netService.SendMessage(new CombatQuillClearDrawingsMessage());
    }

    public bool IsLocalDrawing()
    {
        EnsureInitialized();
        return GetDrawingStateForPlayer(_netService!.NetId).IsDrawing;
    }

    public DrawingMode GetLocalDrawingMode(bool useOverride = true)
    {
        EnsureInitialized();
        var localState = GetDrawingStateForPlayer(_netService!.NetId);
        return useOverride ? localState.CurrentDrawingMode : localState.DrawingMode;
    }

    public void RefreshAllLineStyles()
    {
        EnsureInitialized();

        foreach (var state in _drawingStates)
        {
            var player = _playerCollection!.GetPlayer(state.PlayerId);
            if (player is null)
            {
                continue;
            }

            foreach (var line in state.DrawViewport.GetChildren().OfType<Line2D>())
            {
                ApplyStyle(line, player, line.Material == _eraserMaterial);
            }
        }
    }

    private void EnsureInitialized()
    {
        if (_netService is null || _playerCollection is null || _inputSynchronizer is null)
        {
            throw new InvalidOperationException("CombatQuillDrawings.Initialize must be called before use.");
        }
    }

    private void QueueOrSendEvent(CombatQuillDrawingEvent ev)
    {
        _queuedMessage ??= new CombatQuillDrawingMessage();

        if (!_queuedMessage.TryAddEvent(ev))
        {
            _queuedMessage.DrawingMode = GetDrawingStateForPlayer(_netService!.NetId).DrawingMode;
            _netService!.SendMessage(_queuedMessage);
            _queuedMessage = new CombatQuillDrawingMessage();

            if (!_queuedMessage.TryAddEvent(ev))
            {
                throw new InvalidOperationException("CombatQuill event payload is too large to send.");
            }
        }

        TrySendSyncMessage();
        UpdateLocalCursor();
    }

    private Vector2 ToNetPosition(Vector2 position)
    {
        position /= GetDrawingSurfaceSize();
        return position;
    }

    private Vector2 FromNetPosition(Vector2 position)
    {
        position *= GetDrawingSurfaceSize();
        return position;
    }

    private void HandleDrawingMessage(CombatQuillDrawingMessage message, ulong senderId)
    {
        var drawingState = GetDrawingStateForPlayer(senderId);

        foreach (var ev in message.Events)
        {
            switch (ev.Type)
            {
                case CombatQuillDrawingEventType.BeginLine:
                    if (GetDrawingMode(senderId) != DrawingMode.None)
                    {
                        StopDrawingLine(drawingState);
                    }

                    BeginLine(drawingState, FromNetPosition(ev.Position), ev.OverrideDrawingMode);
                    break;
                case CombatQuillDrawingEventType.ContinueLine:
                    if (!drawingState.IsDrawing)
                    {
                        if (message.DrawingMode.HasValue && drawingState.DrawingMode != message.DrawingMode.Value)
                        {
                            SetDrawingMode(drawingState, message.DrawingMode.Value);
                        }

                        BeginLine(drawingState, FromNetPosition(ev.Position), ev.OverrideDrawingMode);
                    }

                    UpdateCurrentLinePosition(drawingState, FromNetPosition(ev.Position));
                    break;
                case CombatQuillDrawingEventType.EndLine:
                    StopDrawingLine(drawingState);
                    break;
            }
        }
    }

    private void HandleClearDrawingsMessage(CombatQuillClearDrawingsMessage _, ulong senderId)
    {
        ClearAllLinesForPlayer(GetDrawingStateForPlayer(senderId));
    }

    private void HandleModeChangedMessage(CombatQuillModeChangedMessage message, ulong senderId)
    {
        SetDrawingMode(GetDrawingStateForPlayer(senderId), message.DrawingMode);
    }

    private void BeginLine(DrawingState state, Vector2 position, DrawingMode? overrideDrawingMode)
    {
        var player = _playerCollection!.GetPlayer(state.PlayerId)
                     ?? throw new InvalidOperationException($"CombatQuill player {state.PlayerId} does not exist.");
        var drawingMode = overrideDrawingMode ?? state.DrawingMode;
        if (drawingMode == DrawingMode.None)
        {
            throw new InvalidOperationException($"Player {state.PlayerId} is not currently in a drawing mode.");
        }

        state.OverrideDrawingMode = overrideDrawingMode;
        state.CurrentLine = CreateLineForPlayer(player, drawingMode == DrawingMode.Erasing);
        state.CurrentLine.AddPoint(position);
        state.CurrentLine.AddPoint(position + new Vector2(0f, 0.5f));
        state.DrawViewport.AddChild(state.CurrentLine);
        NGame.Instance?.RemoteCursorContainer?.DrawingCursorStateChanged(state.PlayerId);
    }

    private Line2D CreateLineForPlayer(Player player, bool isErasing)
    {
        var scene = isErasing ? _lineEraseScene : _lineDrawScene;
        var line = scene!.Instantiate<Line2D>();
        line.ClearPoints();
        line.Position = Vector2.Zero;
        ApplyStyle(line, player, isErasing);
        return line;
    }

    private void ApplyStyle(Line2D line, Player player, bool isErasing)
    {
        var style = CombatQuillStyleRegistry.GetStyle(player);
        if (isErasing)
        {
            line.Width = style.GetEraserWidth();
            return;
        }

        line.Width = style.GetStrokeWidth();
        line.DefaultColor = style.GetStrokeColor(player.Character.MapDrawingColor);
    }

    private void StopDrawingLine(DrawingState state)
    {
        state.OverrideDrawingMode = null;
        state.CurrentLine = null;
        NGame.Instance?.RemoteCursorContainer?.DrawingCursorStateChanged(state.PlayerId);
    }

    private void SetDrawingMode(DrawingState state, DrawingMode drawingMode)
    {
        if (state.DrawingMode == drawingMode)
        {
            return;
        }

        state.DrawingMode = drawingMode;
        NGame.Instance?.RemoteCursorContainer?.DrawingCursorStateChanged(state.PlayerId);
    }

    private void UpdateCurrentLinePosition(DrawingState state, Vector2 position)
    {
        if (state.CurrentLine is null)
        {
            throw new InvalidOperationException($"Player {state.PlayerId} is not drawing a line.");
        }

        var lastPoint = state.CurrentLine.Points[^1];
        if (lastPoint.DistanceSquaredTo(position) < MinimumPointDistanceSquared)
        {
            return;
        }

        state.CurrentLine.AddPoint(position);
    }

    private DrawingState GetDrawingStateForPlayer(ulong playerId)
    {
        var state = _drawingStates.FirstOrDefault(s => s.PlayerId == playerId);
        if (state is not null)
        {
            return state;
        }

        var drawingSurface = new SubViewportContainer
        {
            Name = $"CombatQuillSurface_{playerId}",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Stretch = true
        };
        drawingSurface.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var drawingViewport = new SubViewport
        {
            Name = "DrawViewport",
            TransparentBg = true,
            HandleInputLocally = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible
        };
        drawingViewport.Size = GetDrawingSurfacePixelSize();

        drawingSurface.AddChild(drawingViewport);
        AddChild(drawingSurface);
        state = new DrawingState
        {
            PlayerId = playerId,
            DrawSurface = drawingSurface,
            DrawViewport = drawingViewport
        };
        _drawingStates.Add(state);
        return state;
    }

    private void UpdateDrawingSurfaceSizes()
    {
        var size = GetDrawingSurfacePixelSize();
        foreach (var state in _drawingStates)
        {
            if (state.DrawViewport.Size != size)
            {
                state.DrawViewport.Size = size;
            }
        }
    }

    private DrawingMode GetDrawingMode(ulong playerId)
    {
        return GetDrawingStateForPlayer(playerId).CurrentDrawingMode;
    }

    private void ClearAllLinesForPlayer(DrawingState state)
    {
        foreach (var line in state.DrawViewport.GetChildren().OfType<Line2D>())
        {
            line.QueueFree();
        }

        state.OverrideDrawingMode = null;
        state.CurrentLine = null;
        SetDrawingMode(state, DrawingMode.None);
    }

    private void OnPlayerScreenChanged(ulong playerId, NetScreenType oldScreenType)
    {
        if (_netService is null || _inputSynchronizer is null || playerId == _netService.NetId)
        {
            return;
        }

        var currentScreenType = _inputSynchronizer.GetScreenType(playerId);
        if (oldScreenType != _screenType || currentScreenType == _screenType)
        {
            return;
        }

        var state = GetDrawingStateForPlayer(playerId);
        if (state.IsDrawing)
        {
            StopDrawingLine(state);
        }

        if (state.DrawingMode != DrawingMode.None)
        {
            SetDrawingMode(state, DrawingMode.None);
        }
    }

    private void TrySendSyncMessage()
    {
        if (_sendMessageTask is not null || _netService is null || !_netService.IsConnected)
        {
            return;
        }

        var delayMsec = (int)(_lastMessageMsec + MinUpdateMsec - Time.GetTicksMsec());
        _sendMessageTask = delayMsec <= 0
            ? TaskHelper.RunSafely(SendSyncMessageAfterSmallDelay())
            : TaskHelper.RunSafely(QueueSyncMessage(delayMsec));
    }

    private async Task QueueSyncMessage(int delayMsec)
    {
        await Task.Delay(delayMsec);
        SendSyncMessage();
    }

    private async Task SendSyncMessageAfterSmallDelay()
    {
        await Task.Yield();
        SendSyncMessage();
    }

    private void SendSyncMessage()
    {
        if (_netService is null || !_netService.IsConnected || _queuedMessage is null)
        {
            _sendMessageTask = null;
            return;
        }

        _queuedMessage.DrawingMode = GetDrawingStateForPlayer(_netService.NetId).DrawingMode;
        _netService.SendMessage(_queuedMessage);
        _lastMessageMsec = Time.GetTicksMsec();
        _queuedMessage = null;
        _sendMessageTask = null;
    }

    private void UpdateLocalCursor()
    {
        if (_cursorManager is null || _netService is null)
        {
            return;
        }

        var mode = GetDrawingStateForPlayer(_netService.NetId).CurrentDrawingMode;
        if (mode == DrawingMode.Drawing)
        {
            _cursorManager.OverrideCursor(
                PreloadManager.Cache.GetAsset<Image>(DrawingCursorTiltedPath),
                PreloadManager.Cache.GetAsset<Image>(DrawingCursorPath),
                DrawingCursorHotspot);
            return;
        }

        if (mode == DrawingMode.Erasing)
        {
            _cursorManager.OverrideCursor(
                PreloadManager.Cache.GetAsset<Image>(ErasingCursorTiltedPath),
                PreloadManager.Cache.GetAsset<Image>(ErasingCursorPath),
                ErasingCursorHotspot);
            return;
        }

        _cursorManager.StopOverridingCursor();
    }

    private Vector2 GetDrawingSurfaceSize()
    {
        var size = Size;
        if (size.X <= 1f || size.Y <= 1f)
        {
            size = GetViewport().GetVisibleRect().Size;
        }

        return new Vector2(
            Mathf.Max(size.X, 1f),
            Mathf.Max(size.Y, 1f));
    }

    private Vector2I GetDrawingSurfacePixelSize()
    {
        var size = GetDrawingSurfaceSize();
        return new Vector2I(
            Mathf.Max(1, Mathf.RoundToInt(size.X)),
            Mathf.Max(1, Mathf.RoundToInt(size.Y)));
    }
}
