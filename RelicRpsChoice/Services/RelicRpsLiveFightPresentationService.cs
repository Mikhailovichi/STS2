using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Runs;

namespace RelicRpsChoice.Services;

internal static class RelicRpsLiveFightPresentationService
{
    private sealed class LiveFightState
    {
        public required NTreasureRoomRelicCollection Collection { get; init; }

        public required NHandImageCollection Hands { get; init; }

        public required Control FightBackstop { get; init; }

        public required NTreasureRoomRelicHolder Holder { get; init; }

        public required RelicPickingFight Fight { get; init; }
    }

    private static readonly object SyncRoot = new();

    private static readonly FieldInfo? FightBackstopField =
        AccessTools.Field(typeof(NTreasureRoomRelicCollection), "_fightBackstop");

    private static readonly FieldInfo? HandsField =
        AccessTools.Field(typeof(NTreasureRoomRelicCollection), "_hands");

    private static readonly FieldInfo? HoldersInUseField =
        AccessTools.Field(typeof(NTreasureRoomRelicCollection), "_holdersInUse");

    private static readonly FieldInfo? RunStateField =
        AccessTools.Field(typeof(NTreasureRoomRelicCollection), "_runState");

    private static readonly FieldInfo? RelicPickingTaskCompletionSourceField =
        AccessTools.Field(typeof(NTreasureRoomRelicCollection), "_relicPickingTaskCompletionSource");

    private static readonly HashSet<RelicPickingFight> PresentedFights = [];

    private static NTreasureRoomRelicCollection? _activeCollection;
    private static LiveFightState? _activeFightState;
    private static bool _handsFrozenForAwards;

    public static void RegisterCollection(NTreasureRoomRelicCollection collection)
    {
        lock (SyncRoot)
        {
            _activeCollection = collection;
        }
    }

    public static void UnregisterCollection(NTreasureRoomRelicCollection collection)
    {
        lock (SyncRoot)
        {
            if (ReferenceEquals(_activeCollection, collection))
            {
                _activeCollection = null;
            }

            if (ReferenceEquals(_activeFightState?.Collection, collection))
            {
                _activeFightState = null;
            }
        }
    }

    public static void BeginTreasureSession()
    {
        lock (SyncRoot)
        {
            PresentedFights.Clear();
            _activeFightState = null;
            _handsFrozenForAwards = false;
        }
    }

    public static bool ShouldHandleRelicsAwarded(List<RelicPickingResult> results)
    {
        lock (SyncRoot)
        {
            return results.Any(result =>
                result.type == RelicPickingResultType.FoughtOver &&
                result.fight is not null &&
                PresentedFights.Contains(result.fight));
        }
    }

    public static void HandleRelicsAwarded(NTreasureRoomRelicCollection collection, List<RelicPickingResult> results)
    {
        TaskHelper.RunSafely(AnimateRelicAwardsAsync(collection, results));
    }

    public static async Task TryPresentRoundAsync(
        RelicPickingFight fight,
        RelicModel relic,
        RelicPickingFightRound round,
        int roundIndex,
        IReadOnlyList<Player> eliminatedPlayers,
        Player? winner)
    {
        NTreasureRoomRelicCollection? collection;
        lock (SyncRoot)
        {
            collection = _activeCollection;
        }

        if (collection is null || !GodotObject.IsInstanceValid(collection))
        {
            return;
        }

        try
        {
            var liveFightState = await EnsureLiveFightStateAsync(collection, fight, relic);
            var durationMultiplier = 1.5f / (roundIndex + 1.5f);

            var tweens = new List<Tween>();
            for (var playerIndex = 0; playerIndex < fight.playersInvolved.Count; playerIndex++)
            {
                var move = round.moves[playerIndex];
                if (!move.HasValue)
                {
                    continue;
                }

                var hand = liveFightState.Hands.GetHand(fight.playersInvolved[playerIndex].NetId);
                if (hand is null)
                {
                    continue;
                }

                tweens.Add(hand.DoFightMove(move.Value, 1.5f * durationMultiplier));
            }

            if (tweens.Count > 0)
            {
                await Task.WhenAll(
                    tweens.Select(tween => collection.ToSignal(tween, Tween.SignalName.Finished).ToTask()));
            }

            var roundTasks = new List<Task>();
            foreach (var eliminatedPlayer in eliminatedPlayers)
            {
                var hand = liveFightState.Hands.GetHand(eliminatedPlayer.NetId);
                if (hand is not null)
                {
                    roundTasks.Add(hand.DoLoseShake(Mathf.Max(1f * durationMultiplier, 0.5f)));
                }
            }

            if (winner is not null)
            {
                await Cmd.Wait(0.5f);
                var winnerHand = liveFightState.Hands.GetHand(winner.NetId);
                if (winnerHand is not null)
                {
                    roundTasks.Add(winnerHand.GrabRelic(liveFightState.Holder));
                }
            }

            if (roundTasks.Count == 0)
            {
                await Cmd.Wait(1f * durationMultiplier);
            }
            else
            {
                await Task.WhenAll(roundTasks);
            }

            if (winner is null)
            {
                return;
            }

            foreach (var player in fight.playersInvolved)
            {
                liveFightState.Hands.GetHand(player.NetId)?.SetIsInFight(inFight: false);
            }

            var fadeTween = collection.CreateTween();
            fadeTween.TweenProperty(liveFightState.FightBackstop, "modulate:a", 0f, 0.25);
            await collection.ToSignal(fadeTween, Tween.SignalName.Finished);
            liveFightState.FightBackstop.Visible = false;
            liveFightState.Holder.ZIndex = 0;

            lock (SyncRoot)
            {
                PresentedFights.Add(fight);
                _activeFightState = null;
            }
        }
        catch (Exception exception)
        {
            MainFile.LogError("Present relic RPS live round", exception);
            lock (SyncRoot)
            {
                _activeFightState = null;
            }
        }
    }

    private static async Task<LiveFightState> EnsureLiveFightStateAsync(
        NTreasureRoomRelicCollection collection,
        RelicPickingFight fight,
        RelicModel relic)
    {
        LiveFightState? existingState;
        lock (SyncRoot)
        {
            existingState = _activeFightState;
            if (existingState is not null && ReferenceEquals(existingState.Fight, fight))
            {
                return existingState;
            }
        }

        var newState = CreateLiveFightState(collection, fight, relic);

        var shouldFreezeHands = false;
        lock (SyncRoot)
        {
            _activeFightState = newState;
            if (!_handsFrozenForAwards)
            {
                _handsFrozenForAwards = true;
                shouldFreezeHands = true;
            }
        }

        if (shouldFreezeHands)
        {
            newState.Hands.BeforeRelicsAwarded();
        }

        newState.Holder.AnimateAwayVotes();
        newState.Holder.ZIndex = 1;
        newState.FightBackstop.Visible = true;

        var tween = collection.CreateTween();
        tween.TweenProperty(
                newState.Holder,
                "global_position",
                (newState.FightBackstop.Size - newState.Holder.Size) * 0.5f,
                0.25)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.In);
        tween.TweenProperty(newState.FightBackstop, "modulate:a", 1f, 0.25);
        newState.Hands.BeforeFightStarted(fight.playersInvolved);

        await collection.ToSignal(tween, Tween.SignalName.Finished);
        await Cmd.Wait(1f);

        return newState;
    }

    private static LiveFightState CreateLiveFightState(
        NTreasureRoomRelicCollection collection,
        RelicPickingFight fight,
        RelicModel relic)
    {
        var hands = GetHands(collection);
        var fightBackstop = GetFightBackstop(collection);
        var holder = GetHoldersInUse(collection)
            .First(candidate => candidate.Relic.Model == relic);

        return new LiveFightState
        {
            Collection = collection,
            Hands = hands,
            FightBackstop = fightBackstop,
            Holder = holder,
            Fight = fight
        };
    }

    private static async Task AnimateRelicAwardsAsync(
        NTreasureRoomRelicCollection collection,
        List<RelicPickingResult> results)
    {
        try
        {
            var holdersInUse = GetHoldersInUse(collection);
            var hands = GetHands(collection);
            var fightBackstop = GetFightBackstop(collection);
            var runState = GetRunState(collection);

            for (var holderIndex = 0; holderIndex < holdersInUse.Count; holderIndex++)
            {
                holdersInUse[holderIndex].SetFocusMode(Control.FocusModeEnum.None);
            }

            hands.BeforeRelicsAwarded();

            var tasksToWait = new List<Task>();
            RelicPickingResultType? lastType = null;
            results.Sort(static (left, right) => left.type.CompareTo(right.type));

            foreach (var result in results)
            {
                var holder = holdersInUse.First(candidate => candidate.Relic.Model == result.relic);
                holder.AnimateAwayVotes();

                if (lastType.HasValue && result.type != lastType)
                {
                    await Cmd.Wait(0.5f);
                }

                if (result.type == RelicPickingResultType.FoughtOver)
                {
                    if (!WasPresentedLive(result.fight))
                    {
                        holder.ZIndex = 1;
                        fightBackstop.Visible = true;

                        var tween = collection.CreateTween();
                        tween.TweenProperty(holder, "global_position", (fightBackstop.Size - holder.Size) * 0.5f, 0.25)
                            .SetTrans(Tween.TransitionType.Back)
                            .SetEase(Tween.EaseType.In);
                        tween.TweenProperty(fightBackstop, "modulate:a", 1f, 0.25);

                        if (result.fight is not null)
                        {
                            hands.BeforeFightStarted(result.fight.playersInvolved);
                        }

                        await collection.ToSignal(tween, Tween.SignalName.Finished);
                        await Cmd.Wait(1f);
                        await hands.DoFight(result, holder);

                        tween = collection.CreateTween();
                        tween.TweenProperty(fightBackstop, "modulate:a", 0f, 0.25);
                        await collection.ToSignal(tween, Tween.SignalName.Finished);
                        fightBackstop.Visible = false;
                        holder.ZIndex = 0;
                    }
                }
                else
                {
                    var player = result.player;
                    if (player is null)
                    {
                        continue;
                    }

                    var hand = hands.GetHand(player.NetId);
                    if (hand is not null)
                    {
                        tasksToWait.Add(TaskHelper.RunSafely(hand.GrabRelic(holder)));
                        await Cmd.Wait(0.25f);
                    }
                }

                lastType = result.type;
            }

            await Task.WhenAll(tasksToWait);
            AnimateHandsAway(hands, runState);

            foreach (var result in results)
            {
                var player = result.player;
                if (player is null)
                {
                    continue;
                }

                var holder = holdersInUse.First(candidate => candidate.Relic.Model == result.relic);
                var relic = result.relic.ToMutable();
                _ = TaskHelper.RunSafely(RelicCmd.Obtain(relic, player));

                var runNode = NRun.Instance;
                if (runNode is not null && LocalContext.IsMe(player))
                {
                    runNode.GlobalUi.RelicInventory.AnimateRelic(relic, holder.GlobalPosition, holder.Scale);
                }

                if (runState.Players.Count == 1)
                {
                    holder.Visible = false;
                }

                foreach (var otherPlayer in player.RunState.Players)
                {
                    if (otherPlayer != player)
                    {
                        otherPlayer.RelicGrabBag.MoveToFallback(result.relic);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            MainFile.LogError("Animate relic awards with live fight presentation", exception);
            throw;
        }
        finally
        {
            CompleteRelicPickingTask(collection);

            lock (SyncRoot)
            {
                PresentedFights.Clear();
                _activeFightState = null;
                _handsFrozenForAwards = false;
            }
        }
    }

    private static bool WasPresentedLive(RelicPickingFight? fight)
    {
        if (fight is null)
        {
            return false;
        }

        lock (SyncRoot)
        {
            return PresentedFights.Contains(fight);
        }
    }

    private static NHandImageCollection GetHands(NTreasureRoomRelicCollection collection)
    {
        if (HandsField is null)
        {
            throw new InvalidOperationException("NTreasureRoomRelicCollection._hands was not found.");
        }

        return (NHandImageCollection)(HandsField.GetValue(collection)
            ?? throw new InvalidOperationException("NTreasureRoomRelicCollection._hands was null."));
    }

    private static Control GetFightBackstop(NTreasureRoomRelicCollection collection)
    {
        if (FightBackstopField is null)
        {
            throw new InvalidOperationException("NTreasureRoomRelicCollection._fightBackstop was not found.");
        }

        return (Control)(FightBackstopField.GetValue(collection)
            ?? throw new InvalidOperationException("NTreasureRoomRelicCollection._fightBackstop was null."));
    }

    private static List<NTreasureRoomRelicHolder> GetHoldersInUse(NTreasureRoomRelicCollection collection)
    {
        if (HoldersInUseField is null)
        {
            throw new InvalidOperationException("NTreasureRoomRelicCollection._holdersInUse was not found.");
        }

        return (List<NTreasureRoomRelicHolder>)(HoldersInUseField.GetValue(collection)
            ?? throw new InvalidOperationException("NTreasureRoomRelicCollection._holdersInUse was null."));
    }

    private static IRunState GetRunState(NTreasureRoomRelicCollection collection)
    {
        if (RunStateField is null)
        {
            throw new InvalidOperationException("NTreasureRoomRelicCollection._runState was not found.");
        }

        return (IRunState)(RunStateField.GetValue(collection)
            ?? throw new InvalidOperationException("NTreasureRoomRelicCollection._runState was null."));
    }

    private static void AnimateHandsAway(NHandImageCollection hands, IRunState runState)
    {
        foreach (var player in runState.Players)
        {
            hands.GetHand(player.NetId)?.AnimateAway();
        }
    }

    private static void CompleteRelicPickingTask(NTreasureRoomRelicCollection collection)
    {
        if (RelicPickingTaskCompletionSourceField?.GetValue(collection) is not TaskCompletionSource completionSource)
        {
            return;
        }

        if (!completionSource.Task.IsCompleted)
        {
            completionSource.SetResult();
        }
    }
}
