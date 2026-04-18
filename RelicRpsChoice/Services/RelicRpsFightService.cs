using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace RelicRpsChoice.Services;

internal static class RelicRpsFightService
{
    internal sealed record UiSnapshot(
        bool IsVisible,
        string Title,
        string Status,
        bool ShowButtons,
        bool CanChoose,
        RelicPickingFightMove? SelectedMove)
    {
        public static UiSnapshot Hidden { get; } = new(false, string.Empty, string.Empty, false, false, null);
    }

    private sealed class RoundPromptState
    {
        public required string Title { get; init; }

        public required string Status { get; set; }

        public required bool ShowButtons { get; init; }

        public required bool CanChoose { get; set; }

        public RelicPickingFightMove? SelectedMove { get; set; }

        public TaskCompletionSource<RelicPickingFightMove>? LocalMoveSource { get; init; }
    }

    private static readonly object SyncRoot = new();

    private static readonly FieldInfo? PlayerCollectionField =
        AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_playerCollection");

    private static readonly FieldInfo? LocalPlayerIdField =
        AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_localPlayerId");

    private static readonly FieldInfo? CurrentRelicsField =
        AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_currentRelics");

    private static readonly FieldInfo? VotesField =
        AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_votes");

    private static readonly FieldInfo? PredictedVoteField =
        AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_predictedVote");

    private static readonly FieldInfo? RngField =
        AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_rng");

    private static readonly FieldInfo? VotesChangedField =
        AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "VotesChanged");

    private static readonly FieldInfo? RelicsAwardedField =
        AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "RelicsAwarded");

    private static readonly MethodInfo? EndRelicVotingMethod =
        AccessTools.Method(typeof(TreasureRoomRelicSynchronizer), "EndRelicVoting");

    private static readonly RelicPickingFightMove[] PossibleMoves = Enum.GetValues<RelicPickingFightMove>();

    private static RoundPromptState? _promptState;
    private static bool _treasureSessionActive;

    public static event Action? FightStateChanged;

    public static bool HasActivePrompt()
    {
        lock (SyncRoot)
        {
            return _promptState is not null;
        }
    }

    public static bool IsTreasureSessionActive()
    {
        lock (SyncRoot)
        {
            return _treasureSessionActive;
        }
    }

    public static void BeginTreasureSession()
    {
        MainFile.Log("[RelicRps] Beginning treasure session.");
        lock (SyncRoot)
        {
            _treasureSessionActive = true;
        }

        RelicRpsLiveFightPresentationService.BeginTreasureSession();
        ResetPromptState();
    }

    public static void ClearActiveFight()
    {
        MainFile.Log("[RelicRps] Clearing active fight state.");
        lock (SyncRoot)
        {
            _treasureSessionActive = false;
        }

        ResetPromptState();
    }

    public static UiSnapshot GetUiSnapshot()
    {
        lock (SyncRoot)
        {
            return BuildUiSnapshotLocked();
        }
    }

    public static bool SubmitLocalMove(RelicPickingFightMove move)
    {
        lock (SyncRoot)
        {
            if (_promptState?.LocalMoveSource is null || _promptState.LocalMoveSource.Task.IsCompleted)
            {
                return false;
            }

            _promptState.SelectedMove = move;
            _promptState.CanChoose = false;
            _promptState.Status = "Choice locked. Waiting for other players.";
            _promptState.LocalMoveSource.TrySetResult(move);
        }

        MainFile.Log($"[RelicRps] Local move submitted: {MoveDisplayName(move)}.");
        RaiseFightStateChanged();
        return true;
    }

    public static async Task HandlePickRelicActionAsync(
        PickRelicAction action,
        TreasureRoomRelicSynchronizer synchronizer,
        Player player,
        int relicIndex)
    {
        try
        {
            await HandlePickRelicInternalAsync(action, synchronizer, player, relicIndex);
        }
        catch (Exception exception)
        {
            ClearActiveFight();
            MainFile.LogError("Handle pick relic action", exception);
            throw;
        }
    }

    private static async Task HandlePickRelicInternalAsync(
        PickRelicAction _,
        TreasureRoomRelicSynchronizer synchronizer,
        Player player,
        int relicIndex)
    {
        var currentRelics = GetCurrentRelics(synchronizer);
        if (currentRelics is null)
        {
            MainFile.Log("[RelicRps] Attempted to pick relic while relic picking was inactive.");
            return;
        }

        if (relicIndex < 0 || relicIndex >= currentRelics.Count)
        {
            throw new IndexOutOfRangeException(
                $"Attempted to pick relic at index {relicIndex}, but there are only {currentRelics.Count} relics.");
        }

        var playerCollection = GetPlayerCollection(synchronizer);
        var votes = GetVotes(synchronizer);
        votes[playerCollection.GetPlayerSlotIndex(player)] = relicIndex;
        MainFile.Log($"[RelicRps] Vote registered. Player={player.NetId}, RelicIndex={relicIndex}.");
        InvokeVotesChanged(synchronizer);

        if (!votes.All(static vote => vote.HasValue))
        {
            return;
        }

        ResolvePredictedVote(synchronizer);
        await AwardRelicsAsync(synchronizer);
        EndRelicVoting(synchronizer);
        ClearActiveFight();
    }

    private static async Task AwardRelicsAsync(TreasureRoomRelicSynchronizer synchronizer)
    {
        var currentRelics = GetCurrentRelics(synchronizer);
        if (currentRelics is null)
        {
            return;
        }

        var playerCollection = GetPlayerCollection(synchronizer);
        var players = playerCollection.Players;
        var votes = GetVotes(synchronizer);
        var rng = GetRng(synchronizer);

        var groupedVotes = new Dictionary<int, List<Player>>();
        for (var relicIndex = 0; relicIndex < currentRelics.Count; relicIndex++)
        {
            groupedVotes[relicIndex] = [];
        }

        for (var playerIndex = 0; playerIndex < votes.Count; playerIndex++)
        {
            var pickedIndex = votes[playerIndex];
            if (!pickedIndex.HasValue)
            {
                continue;
            }

            groupedVotes[pickedIndex.Value].Add(players[playerIndex]);
        }

        MainFile.Log(
            "[RelicRps] Resolving treasure votes: " +
            string.Join(
                "; ",
                groupedVotes
                    .OrderBy(static pair => pair.Key)
                    .Select(pair => $"Relic[{pair.Key}]<-[{string.Join(",", pair.Value.Select(static p => p.NetId))}]")));

        var results = new List<RelicPickingResult>();
        var unclaimedRelics = new List<RelicModel>();

        for (var relicIndex = 0; relicIndex < currentRelics.Count; relicIndex++)
        {
            var relic = currentRelics[relicIndex];
            var voters = groupedVotes[relicIndex];

            if (voters.Count == 0)
            {
                unclaimedRelics.Add(relic);
                continue;
            }

            if (voters.Count == 1)
            {
                results.Add(new RelicPickingResult
                {
                    type = RelicPickingResultType.OnlyOnePlayerVoted,
                    relic = relic,
                    player = voters[0]
                });
                continue;
            }

            results.Add(await ResolveFightAsync(synchronizer, voters, relic));
        }

        var playersWithoutRelics = players
            .Where(player => results.All(result => result.player != player))
            .ToList();

        unclaimedRelics.StableShuffle(rng);
        for (var index = 0; index < Math.Min(unclaimedRelics.Count, playersWithoutRelics.Count); index++)
        {
            results.Add(new RelicPickingResult
            {
                type = RelicPickingResultType.ConsolationPrize,
                player = playersWithoutRelics[index],
                relic = unclaimedRelics[index]
            });
        }

        InvokeRelicsAwarded(synchronizer, results);
        MainFile.Log(
            "[RelicRps] Relic awards resolved: " +
            string.Join(
                "; ",
                results.Select(result => $"{result.relic.Id.Entry}->{result.player?.NetId.ToString() ?? "none"}({result.type})")));
    }

    private static async Task<RelicPickingResult> ResolveFightAsync(
        TreasureRoomRelicSynchronizer synchronizer,
        List<Player> players,
        RelicModel relic)
    {
        var fight = new RelicPickingFight();
        fight.playersInvolved.AddRange(players);

        var activePlayers = new HashSet<Player>(players);
        var roundIndex = 0;

        while (activePlayers.Count > 1)
        {
            var orderedActivePlayers = players.Where(activePlayers.Contains).ToList();
            MainFile.Log(
                $"[RelicRps] Starting fight round {roundIndex + 1} for relic {relic.Id.Entry}. " +
                $"Active players: {string.Join(",", orderedActivePlayers.Select(static p => p.NetId))}.");

            var movesByPlayer = await GatherRoundMovesAsync(
                synchronizer,
                players,
                orderedActivePlayers,
                roundIndex,
                relic);

            var round = new RelicPickingFightRound();
            foreach (var player in players)
            {
                round.moves.Add(activePlayers.Contains(player) ? movesByPlayer[player.NetId] : null);
            }

            fight.rounds.Add(round);

            var distinctMoves = movesByPlayer.Values.Distinct().ToList();
            List<Player> eliminatedPlayers = [];
            RelicPickingFightMove? losingMove = null;
            MainFile.Log(
                "[RelicRps] Round moves: " +
                string.Join(", ", movesByPlayer.Select(pair => $"{pair.Key}:{MoveDisplayName(pair.Value)}")));
            if (distinctMoves.Count == 2)
            {
                losingMove = GetLosingMove(distinctMoves[0], distinctMoves[1]);
                foreach (var activePlayer in orderedActivePlayers)
                {
                    if (movesByPlayer[activePlayer.NetId] == losingMove.Value)
                    {
                        eliminatedPlayers.Add(activePlayer);
                    }
                }
            }

            var roundWinner = activePlayers.Count - eliminatedPlayers.Count == 1
                ? orderedActivePlayers.First(activePlayer => !eliminatedPlayers.Contains(activePlayer))
                : null;

            await RelicRpsLiveFightPresentationService.TryPresentRoundAsync(
                fight,
                relic,
                round,
                roundIndex,
                eliminatedPlayers,
                roundWinner);

            if (losingMove.HasValue)
            {
                foreach (var eliminatedPlayer in eliminatedPlayers)
                {
                    activePlayers.Remove(eliminatedPlayer);
                }

                MainFile.Log(
                    $"[RelicRps] Losing move: {MoveDisplayName(losingMove.Value)}. " +
                    $"Eliminated: {string.Join(",", eliminatedPlayers.Select(static player => player.NetId))}.");
            }
            else
            {
                MainFile.Log("[RelicRps] Round tied. No elimination this round.");
            }

            roundIndex++;
        }

        var winner = activePlayers.First();
        MainFile.Log($"[RelicRps] Fight winner for relic {relic.Id.Entry}: {winner.NetId}.");

        return new RelicPickingResult
        {
            type = RelicPickingResultType.FoughtOver,
            player = winner,
            relic = relic,
            fight = fight
        };
    }

    private static async Task<IReadOnlyDictionary<ulong, RelicPickingFightMove>> GatherRoundMovesAsync(
        TreasureRoomRelicSynchronizer synchronizer,
        IReadOnlyList<Player> allFightPlayers,
        IReadOnlyList<Player> orderedActivePlayers,
        int roundIndex,
        RelicModel relic)
    {
        if (orderedActivePlayers.Count == 0)
        {
            return new Dictionary<ulong, RelicPickingFightMove>();
        }

        var stableActivePlayers = OrderPlayersForChoiceIds(synchronizer, orderedActivePlayers);

        try
        {
            ShowRoundPrompt(allFightPlayers, stableActivePlayers, roundIndex, relic);

            // Reserve choice IDs in a deterministic slot order to avoid cross-client drift.
            var choiceIds = stableActivePlayers.ToDictionary(
                static player => player.NetId,
                static player => RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(player));

            var moveTasks = stableActivePlayers
                .Select(async player => new KeyValuePair<ulong, RelicPickingFightMove>(
                    player.NetId,
                    await GetMoveForPlayerAsync(synchronizer, player, choiceIds[player.NetId])))
                .ToArray();

            var resolvedMoves = await Task.WhenAll(moveTasks);
            MainFile.Log(
                $"[RelicRps] Player choice phase complete for round {roundIndex + 1}, relic {relic.Id.Entry}.");
            return resolvedMoves.ToDictionary(static pair => pair.Key, static pair => pair.Value);
        }
        finally
        {
            ResetPromptState();
        }
    }

    private static IReadOnlyList<Player> OrderPlayersForChoiceIds(
        TreasureRoomRelicSynchronizer synchronizer,
        IReadOnlyList<Player> players)
    {
        var playerCollection = GetPlayerCollection(synchronizer);
        return players
            .OrderBy(player => playerCollection.GetPlayerSlotIndex(player))
            .ThenBy(static player => player.NetId)
            .ToList();
    }

    private static async Task<RelicPickingFightMove> GetMoveForPlayerAsync(
        TreasureRoomRelicSynchronizer synchronizer,
        Player player,
        uint choiceId)
    {
        if (RunManager.Instance.IsSinglePlayerOrFakeMultiplayer)
        {
            if (ShouldSelectLocally(player))
            {
                MainFile.Log($"[RelicRps] Waiting for local move from player {player.NetId}, choiceId={choiceId}.");
                return await GetAndSyncLocalMoveAsync(player, choiceId);
            }

            var simulatedMove = GetRng(synchronizer).NextItem(PossibleMoves);
            MainFile.Log(
                $"[RelicRps] Fake multiplayer auto-move for player {player.NetId}: {MoveDisplayName(simulatedMove)}.");
            return simulatedMove;
        }

        if (ShouldSelectLocally(player))
        {
            MainFile.Log($"[RelicRps] Waiting for local move from player {player.NetId}, choiceId={choiceId}.");
            return await GetAndSyncLocalMoveAsync(player, choiceId);
        }

        MainFile.Log($"[RelicRps] Waiting for remote move from player {player.NetId}, choiceId={choiceId}.");
        var result = await RunManager.Instance.PlayerChoiceSynchronizer.WaitForRemoteChoice(player, choiceId);
        if (!TryMoveFromChoiceResult(result, out var remoteMove))
        {
            remoteMove = GetRng(synchronizer).NextItem(PossibleMoves);
            MainFile.Log(
                $"[RelicRps] Invalid remote move payload from player {player.NetId}, choiceId={choiceId}. " +
                $"Falling back to {MoveDisplayName(remoteMove)}.");
        }

        MainFile.Log($"[RelicRps] Remote move received from player {player.NetId}: {MoveDisplayName(remoteMove)}.");
        return remoteMove;
    }

    private static async Task<RelicPickingFightMove> GetAndSyncLocalMoveAsync(Player player, uint choiceId)
    {
        var move = await WaitForLocalMoveAsync();
        RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(
            player,
            choiceId,
            PlayerChoiceResult.FromIndex((int)move));
        MainFile.Log(
            $"[RelicRps] Synced local move for player {player.NetId}, choiceId={choiceId}: {MoveDisplayName(move)}.");
        return move;
    }

    private static async Task<RelicPickingFightMove> WaitForLocalMoveAsync()
    {
        Task<RelicPickingFightMove> waitTask;
        lock (SyncRoot)
        {
            waitTask = _promptState?.LocalMoveSource?.Task
                ?? throw new InvalidOperationException("Local relic RPS move was requested without an active prompt.");
        }

        return await waitTask;
    }

    private static void ShowRoundPrompt(
        IReadOnlyList<Player> allFightPlayers,
        IReadOnlyList<Player> orderedActivePlayers,
        int roundIndex,
        RelicModel relic)
    {
        var localPlayerId = LocalContext.NetId;
        var localInFight = localPlayerId.HasValue &&
                           allFightPlayers.Any(player => player.NetId == localPlayerId.Value);
        var localCanChoose = localPlayerId.HasValue &&
                             orderedActivePlayers.Any(player => player.NetId == localPlayerId.Value) &&
                             ShouldSelectLocally(orderedActivePlayers.First(player => player.NetId == localPlayerId.Value));

        var title = $"{relic.Title.GetFormattedText()}  Round {roundIndex + 1}";
        var status = localCanChoose
            ? "Choose Rock / Paper / Scissors."
            : (localInFight ? "You are out this round. Waiting for result." : "Other players are resolving this relic.");

        lock (SyncRoot)
        {
            _promptState = new RoundPromptState
            {
                Title = title,
                Status = status,
                ShowButtons = localInFight,
                CanChoose = localCanChoose,
                SelectedMove = null,
                LocalMoveSource = localCanChoose
                    ? new TaskCompletionSource<RelicPickingFightMove>(TaskCreationOptions.RunContinuationsAsynchronously)
                    : null
            };
        }

        MainFile.Log(
            $"[RelicRps] Prompt shown for relic {relic.Id.Entry}, round {roundIndex + 1}. " +
            $"All players={string.Join(",", allFightPlayers.Select(static p => p.NetId))}; " +
            $"Active players={string.Join(",", orderedActivePlayers.Select(static p => p.NetId))}; " +
            $"LocalCanChoose={localCanChoose}.");
        RaiseFightStateChanged();
    }

    private static void ResetPromptState()
    {
        lock (SyncRoot)
        {
            _promptState?.LocalMoveSource?.TrySetCanceled();
            _promptState = null;
        }

        MainFile.Log("[RelicRps] Prompt state reset.");
        RaiseFightStateChanged();
    }

    private static UiSnapshot BuildUiSnapshotLocked()
    {
        if (_promptState is null)
        {
            return UiSnapshot.Hidden;
        }

        return new UiSnapshot(
            true,
            _promptState.Title,
            _promptState.Status,
            _promptState.ShowButtons,
            _promptState.CanChoose,
            _promptState.SelectedMove);
    }

    private static void ResolvePredictedVote(TreasureRoomRelicSynchronizer synchronizer)
    {
        var predictedVote = GetPredictedVote(synchronizer);
        if (!predictedVote.HasValue)
        {
            return;
        }

        var playerCollection = GetPlayerCollection(synchronizer);
        var localPlayer = playerCollection.GetPlayer(GetLocalPlayerId(synchronizer))
            ?? throw new InvalidOperationException("Failed to resolve local player for relic voting.");
        var localVote = GetVotes(synchronizer)[playerCollection.GetPlayerSlotIndex(localPlayer)];

        SetPredictedVote(synchronizer, null);
        if (localVote != predictedVote)
        {
            InvokeVotesChanged(synchronizer);
        }
    }

    private static bool ShouldSelectLocally(Player player)
    {
        return LocalContext.IsMe(player) && RunManager.Instance.NetService.Type != NetGameType.Replay;
    }

    private static bool TryMoveFromChoiceResult(PlayerChoiceResult result, out RelicPickingFightMove move)
    {
        var index = result.AsIndex();
        if (index < 0 || index >= PossibleMoves.Length)
        {
            move = default;
            return false;
        }

        move = (RelicPickingFightMove)index;
        return true;
    }

    private static IPlayerCollection GetPlayerCollection(TreasureRoomRelicSynchronizer synchronizer)
    {
        if (PlayerCollectionField is null)
        {
            throw new InvalidOperationException("TreasureRoomRelicSynchronizer._playerCollection was not found.");
        }

        return (IPlayerCollection)(PlayerCollectionField.GetValue(synchronizer)
            ?? throw new InvalidOperationException("TreasureRoomRelicSynchronizer._playerCollection was null."));
    }

    private static ulong GetLocalPlayerId(TreasureRoomRelicSynchronizer synchronizer)
    {
        if (LocalPlayerIdField is null)
        {
            throw new InvalidOperationException("TreasureRoomRelicSynchronizer._localPlayerId was not found.");
        }

        return (ulong)(LocalPlayerIdField.GetValue(synchronizer)
            ?? throw new InvalidOperationException("TreasureRoomRelicSynchronizer._localPlayerId was null."));
    }

    private static List<RelicModel>? GetCurrentRelics(TreasureRoomRelicSynchronizer synchronizer)
    {
        if (CurrentRelicsField is null)
        {
            throw new InvalidOperationException("TreasureRoomRelicSynchronizer._currentRelics was not found.");
        }

        return CurrentRelicsField.GetValue(synchronizer) as List<RelicModel>;
    }

    private static List<int?> GetVotes(TreasureRoomRelicSynchronizer synchronizer)
    {
        if (VotesField is null)
        {
            throw new InvalidOperationException("TreasureRoomRelicSynchronizer._votes was not found.");
        }

        return (List<int?>)(VotesField.GetValue(synchronizer)
            ?? throw new InvalidOperationException("TreasureRoomRelicSynchronizer._votes was null."));
    }

    private static int? GetPredictedVote(TreasureRoomRelicSynchronizer synchronizer)
    {
        if (PredictedVoteField is null)
        {
            throw new InvalidOperationException("TreasureRoomRelicSynchronizer._predictedVote was not found.");
        }

        return (int?)PredictedVoteField.GetValue(synchronizer);
    }

    private static void SetPredictedVote(TreasureRoomRelicSynchronizer synchronizer, int? value)
    {
        if (PredictedVoteField is null)
        {
            throw new InvalidOperationException("TreasureRoomRelicSynchronizer._predictedVote was not found.");
        }

        PredictedVoteField.SetValue(synchronizer, value);
    }

    private static Rng GetRng(TreasureRoomRelicSynchronizer synchronizer)
    {
        if (RngField is null)
        {
            throw new InvalidOperationException("TreasureRoomRelicSynchronizer._rng was not found.");
        }

        return (Rng)(RngField.GetValue(synchronizer)
            ?? throw new InvalidOperationException("TreasureRoomRelicSynchronizer._rng was null."));
    }

    private static void InvokeVotesChanged(TreasureRoomRelicSynchronizer synchronizer)
    {
        if (VotesChangedField is null)
        {
            throw new InvalidOperationException("TreasureRoomRelicSynchronizer.VotesChanged was not found.");
        }

        (VotesChangedField.GetValue(synchronizer) as Action)?.Invoke();
    }

    private static void InvokeRelicsAwarded(
        TreasureRoomRelicSynchronizer synchronizer,
        List<RelicPickingResult> results)
    {
        if (RelicsAwardedField is null)
        {
            throw new InvalidOperationException("TreasureRoomRelicSynchronizer.RelicsAwarded was not found.");
        }

        (RelicsAwardedField.GetValue(synchronizer) as Action<List<RelicPickingResult>>)?.Invoke(results);
    }

    private static void EndRelicVoting(TreasureRoomRelicSynchronizer synchronizer)
    {
        EndRelicVotingMethod?.Invoke(synchronizer, null);
    }

    private static RelicPickingFightMove GetLosingMove(RelicPickingFightMove move1, RelicPickingFightMove move2)
    {
        return ((int)(move1 + 1) % 3 == (int)move2) ? move1 : move2;
    }

    private static string MoveDisplayName(RelicPickingFightMove move)
    {
        return move switch
        {
            RelicPickingFightMove.Rock => "Rock",
            RelicPickingFightMove.Paper => "Paper",
            RelicPickingFightMove.Scissors => "Scissors",
            _ => move.ToString()
        };
    }

    private static void RaiseFightStateChanged()
    {
        FightStateChanged?.Invoke();
    }
}
