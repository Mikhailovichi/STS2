using MegaCrit.Sts2.Core.Multiplayer.Game;
using PartyObserver.Networking;

namespace PartyObserver.Services;

internal static class PartyObserverRegistry
{
    private static readonly Dictionary<ulong, PartyObserverChoiceSnapshot> Snapshots = [];
    private static readonly HashSet<ulong> SnapshotCapablePeers = [];

    private static INetGameService? _netService;
    private static bool _supportProbeSent;

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
            _netService.UnregisterMessageHandler<PartyObserverSupportMessage>(HandleSupportMessage);
        }

        Snapshots.Clear();
        SnapshotCapablePeers.Clear();
        _supportProbeSent = false;
        _netService = netService;

        if (_netService is not null)
        {
            _netService.RegisterMessageHandler<PartyObserverChoiceSnapshotMessage>(HandleSnapshotMessage);
            _netService.RegisterMessageHandler<PartyObserverSupportMessage>(HandleSupportMessage);
            MaybeSendSupportProbe();
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

        if (Snapshots.TryGetValue(_netService.NetId, out var existingSnapshot) &&
            SnapshotEquals(existingSnapshot, snapshot))
        {
            return;
        }

        var localSnapshot = snapshot.Clone();
        Snapshots[_netService.NetId] = localSnapshot;

        TryDispatchSnapshotMessage(PartyObserverChoiceSnapshotMessage.Create(localSnapshot), broadcast);

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
        TryDispatchSnapshotMessage(PartyObserverChoiceSnapshotMessage.CreateClear(), broadcast);

        if (removed || broadcast)
        {
            SnapshotChanged?.Invoke(localPlayerId);
        }
    }

    private static void HandleSnapshotMessage(PartyObserverChoiceSnapshotMessage message, ulong senderId)
    {
        SnapshotCapablePeers.Add(senderId);

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

    private static void HandleSupportMessage(PartyObserverSupportMessage message, ulong senderId)
    {
        if (_netService is null || senderId == _netService.NetId)
        {
            return;
        }

        SnapshotCapablePeers.Add(senderId);
        if (_netService.Type == NetGameType.Host && message.Kind == PartyObserverSupportMessageKind.Probe)
        {
            _netService.SendMessage(PartyObserverSupportMessage.CreateAck(), senderId);
            SendCurrentLocalSnapshotToPeer(senderId);
            return;
        }

        if (_netService.Type == NetGameType.Client && message.Kind == PartyObserverSupportMessageKind.Ack)
        {
            TryDispatchSnapshotMessage(
                Snapshots.TryGetValue(_netService.NetId, out var localSnapshot)
                    ? PartyObserverChoiceSnapshotMessage.Create(localSnapshot)
                    : PartyObserverChoiceSnapshotMessage.CreateClear(),
                broadcast: true);
        }
    }

    private static void MaybeSendSupportProbe()
    {
        if (_netService is null || !_netService.IsConnected || _supportProbeSent || _netService.Type != NetGameType.Client)
        {
            return;
        }

        _supportProbeSent = true;
        _netService.SendMessage(PartyObserverSupportMessage.CreateProbe());
    }

    private static void TryDispatchSnapshotMessage(PartyObserverChoiceSnapshotMessage message, bool broadcast)
    {
        if (_netService is null || !broadcast || !_netService.IsConnected)
        {
            return;
        }

        if (_netService.Type == NetGameType.Host && _netService is INetHostGameService hostService)
        {
            foreach (var peer in hostService.ConnectedPeers)
            {
                if (!peer.readyForBroadcasting || !SnapshotCapablePeers.Contains(peer.peerId))
                {
                    continue;
                }

                _netService.SendMessage(message, peer.peerId);
            }

            return;
        }

        if (SnapshotCapablePeers.Count > 0)
        {
            _netService.SendMessage(message);
        }
    }

    private static void SendCurrentLocalSnapshotToPeer(ulong peerId)
    {
        if (_netService is null || !_netService.IsConnected || !Snapshots.TryGetValue(_netService.NetId, out var localSnapshot))
        {
            return;
        }

        _netService.SendMessage(PartyObserverChoiceSnapshotMessage.Create(localSnapshot), peerId);
    }

    private static bool SnapshotEquals(PartyObserverChoiceSnapshot left, PartyObserverChoiceSnapshot right)
    {
        if (left.Kind != right.Kind ||
            left.ScreenLabel != right.ScreenLabel ||
            left.Title != right.Title ||
            left.Description != right.Description ||
            left.Options.Count != right.Options.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Options.Count; i++)
        {
            if (!OptionEquals(left.Options[i], right.Options[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool OptionEquals(PartyObserverChoiceOption left, PartyObserverChoiceOption right)
    {
        return left.Title == right.Title &&
               left.Subtitle == right.Subtitle &&
               left.Description == right.Description &&
               left.Tag == right.Tag &&
               left.ImagePath == right.ImagePath &&
               left.IsDisabled == right.IsDisabled &&
               left.IsProceed == right.IsProceed;
    }
}
