using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using PartyObserver.Services;

namespace PartyObserver.Runtime;

internal sealed partial class PartyObserverRuntimeBootstrapper : Node
{
    private const string BootstrapperNodeName = "PartyObserverRuntimeBootstrapper";

    private readonly Dictionary<ulong, WeakReference<NMultiplayerPlayerState>> _trackedPlayerStates = [];
    private bool _wasRunInProgress;

    public static void EnsureInstalled()
    {
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root is null)
        {
            GD.PrintErr($"{MainFile.ModId}: could not install runtime bootstrapper because SceneTree is unavailable");
            return;
        }

        if (tree.Root.GetNodeOrNull<PartyObserverRuntimeBootstrapper>(BootstrapperNodeName) is not null)
        {
            return;
        }

        var bootstrapper = new PartyObserverRuntimeBootstrapper
        {
            Name = BootstrapperNodeName
        };

        Callable.From(() => tree.Root.AddChild(bootstrapper)).CallDeferred();
    }

    public override void _Ready()
    {
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _ = delta;
        SyncRuntimeState();
    }

    private void SyncRuntimeState()
    {
        if (!RunManager.Instance.IsInProgress)
        {
            if (_wasRunInProgress)
            {
                PartyObserverService.ClearRunContext();
                _trackedPlayerStates.Clear();
                _wasRunInProgress = false;
            }

            return;
        }

        _wasRunInProgress = true;
        PartyObserverService.InitializeRunContext();

        if (!PartyObserverService.TryEnsureOverlayForCurrentRun())
        {
            return;
        }

        SyncPlayerStates();
    }

    private void SyncPlayerStates()
    {
        if (NRun.Instance is not Node runRoot)
        {
            return;
        }

        var activeStateIds = new HashSet<ulong>();

        foreach (var playerState in EnumeratePlayerStates(runRoot))
        {
            var instanceId = playerState.GetInstanceId();
            activeStateIds.Add(instanceId);

            if (_trackedPlayerStates.ContainsKey(instanceId))
            {
                continue;
            }

            _trackedPlayerStates[instanceId] = new WeakReference<NMultiplayerPlayerState>(playerState);
            PartyObserverService.RegisterPlayerState(playerState);
        }

        foreach (var entry in _trackedPlayerStates.ToList())
        {
            if (activeStateIds.Contains(entry.Key))
            {
                continue;
            }

            if (entry.Value.TryGetTarget(out var playerState) && GodotObject.IsInstanceValid(playerState))
            {
                PartyObserverService.UnregisterPlayerState(playerState);
            }

            _trackedPlayerStates.Remove(entry.Key);
        }
    }

    private static IEnumerable<NMultiplayerPlayerState> EnumeratePlayerStates(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is NMultiplayerPlayerState playerState)
            {
                yield return playerState;
            }

            foreach (var nestedState in EnumeratePlayerStates(child))
            {
                yield return nestedState;
            }
        }
    }
}
