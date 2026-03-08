using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using PartyObserver.UI;

namespace PartyObserver.Services;

internal static class PartyObserverService
{
    public static void InitializeRunContext()
    {
        PartyObserverRegistry.BindToNetService(RunManager.Instance.IsInProgress ? RunManager.Instance.NetService : null);
    }

    public static void PersistSettings(PartyObserverSettings settings)
    {
        PartyObserverSettingsStore.Save(settings);
    }

    public static void AttachOverlay(NCombatUi combatUi)
    {
        _ = combatUi;
        EnsureOverlay();
    }

    public static void AttachOverlay(NRewardsScreen rewardsScreen)
    {
        _ = rewardsScreen;
        EnsureOverlay();
    }

    public static void AttachOverlay(NEventRoom eventRoom)
    {
        _ = eventRoom;
        EnsureOverlay();
    }

    public static void AttachOverlay(NCardRewardSelectionScreen selectionScreen)
    {
        _ = selectionScreen;
        EnsureOverlay();
    }

    public static void RegisterPlayerState(NMultiplayerPlayerState playerState)
    {
        EnsureOverlay()?.RegisterPlayerState(playerState);
    }

    public static void UnregisterPlayerState(NMultiplayerPlayerState playerState)
    {
        TryGetOverlay()?.UnregisterPlayerState(playerState);
    }

    public static void UpdateRewardsSnapshot(NRewardsScreen rewardsScreen)
    {
        var rewards = new List<Reward>();
        var rewardsContainer = rewardsScreen.GetNodeOrNull<Control>("%RewardsContainer");
        if (rewardsContainer is null)
        {
            PartyObserverRegistry.ClearLocalSnapshot();
            return;
        }

        foreach (var child in rewardsContainer.GetChildren())
        {
            switch (child)
            {
                case NRewardButton rewardButton when rewardButton.Reward is not null:
                    rewards.Add(rewardButton.Reward);
                    break;
                case NLinkedRewardSet linkedRewardSet when linkedRewardSet.LinkedRewardSet is not null:
                    rewards.AddRange(linkedRewardSet.LinkedRewardSet.Rewards);
                    break;
            }
        }

        if (rewards.Count == 0)
        {
            PartyObserverRegistry.ClearLocalSnapshot();
            return;
        }

        PartyObserverRegistry.UpdateLocalSnapshot(PartyObserverChoiceSnapshotBuilder.BuildRewardsScreen(rewards));
    }

    private static PartyObserverOverlay? EnsureOverlay()
    {
        var settings = PartyObserverSettingsStore.Load();
        if (!settings.Enabled)
        {
            return null;
        }

        InitializeRunContext();

        var runState = RunManager.Instance.DebugOnlyGetState();
        if (runState is not IPlayerCollection playerCollection || playerCollection.Players.Count() <= 1)
        {
            return null;
        }

        if (NRun.Instance is null)
        {
            return null;
        }

        var overlay = TryGetOverlay();
        if (overlay is not null)
        {
            overlay.Initialize(settings);
            return overlay;
        }

        overlay = new PartyObserverOverlay
        {
            Name = "PartyObserverOverlay"
        };
        overlay.Initialize(settings);
        NRun.Instance.AddChild(overlay);

        GD.Print($"{MainFile.ModId}: attached observer overlay");
        return overlay;
    }

    private static PartyObserverOverlay? TryGetOverlay()
    {
        return NRun.Instance?.GetNodeOrNull<PartyObserverOverlay>("PartyObserverOverlay");
    }
}
