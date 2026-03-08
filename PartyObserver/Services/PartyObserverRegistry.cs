using MegaCrit.Sts2.Core.Multiplayer.Game;
using PartyObserver.Networking;

namespace PartyObserver.Services;

internal static class PartyObserverRegistry
{
    private static readonly Dictionary<ulong, PartyObserverChoiceSnapshot> Snapshots = [];

    private static INetGameService? _netService;

    public static event Action<ulong>? SnapshotChanged;

    public static void BindToNetService(INetGameService? netService)
    {
        if (ReferenceEquals(_netService, netService))
        {
            return;
        }

        if (_netService is not null)
        {
            _netService.UnregisterMessageHandler<PartyObserverChoiceSnapshotMessage>(HandleSnapshotMessage);
        }

        Snapshots.Clear();
        _netService = netService;

        if (_netService is not null)
        {
            _netService.RegisterMessageHandler<PartyObserverChoiceSnapshotMessage>(HandleSnapshotMessage);
        }
    }

    public static PartyObserverChoiceSnapshot? GetSnapshot(ulong playerId)
    {
        return Snapshots.TryGetValue(playerId, out var snapshot) ? snapshot.Clone() : null;
    }

    public static void UpdateLocalSnapshot(PartyObserverChoiceSnapshot snapshot, bool broadcast = true)
    {
        if (_netService is null)
        {
            return;
        }

        var localSnapshot = snapshot.Clone();
        Snapshots[_netService.NetId] = localSnapshot;

        if (broadcast && _netService.IsConnected)
        {
            _netService.SendMessage(PartyObserverChoiceSnapshotMessage.Create(localSnapshot));
        }

        SnapshotChanged?.Invoke(_netService.NetId);
    }

    public static void ClearLocalSnapshot(bool broadcast = true)
    {
        if (_netService is null)
        {
            return;
        }

        var localPlayerId = _netService.NetId;
        var removed = Snapshots.Remove(localPlayerId);
        if (broadcast && _netService.IsConnected)
        {
            _netService.SendMessage(PartyObserverChoiceSnapshotMessage.CreateClear());
        }

        if (removed || broadcast)
        {
            SnapshotChanged?.Invoke(localPlayerId);
        }
    }

    private static void HandleSnapshotMessage(PartyObserverChoiceSnapshotMessage message, ulong senderId)
    {
        var snapshot = message.ToSnapshot();
        if (snapshot is null)
        {
            Snapshots.Remove(senderId);
        }
        else
        {
            Snapshots[senderId] = snapshot;
        }

        SnapshotChanged?.Invoke(senderId);
    }
}
