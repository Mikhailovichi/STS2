using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Rewards;
using PartyObserver.Networking;

namespace PartyObserver.Services;

internal static class PartyObserverChoiceSnapshotBuilder
{
    private static readonly Regex BbCodePattern = new(@"\[[^\]]+\]", RegexOptions.Compiled);
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);
    private static readonly FieldInfo? RelicRewardRelicField = AccessTools.Field(typeof(RelicReward), "_relic");
    private static readonly FieldInfo? CardRewardCardsField = AccessTools.Field(typeof(CardReward), "_cards");
    private static readonly MethodInfo? RewardIconPathGetter = AccessTools.PropertyGetter(typeof(Reward), "IconPath");

    public static PartyObserverChoiceSnapshot BuildRewardsScreen(IEnumerable<Reward> rewards)
    {
        var rewardList = rewards.ToList();
        var snapshot = new PartyObserverChoiceSnapshot
        {
            Kind = PartyObserverChoiceSnapshotKind.Rewards,
            ScreenLabel = PartyObserverChoiceSnapshotKind.Rewards.GetDisplayName(),
            Title = PartyObserverText.ReviewingRewards(),
            Description = PartyObserverText.FormatRewardsCount(rewardList.Count)
        };

        foreach (var reward in rewardList)
        {
            snapshot.AddOption(BuildRewardOption(reward));
        }

        return snapshot;
    }

    public static PartyObserverChoiceSnapshot BuildCardRewardSelection(
        IReadOnlyList<CardCreationResult> options,
        IReadOnlyList<CardRewardAlternative> extraOptions)
    {
        var snapshot = new PartyObserverChoiceSnapshot
        {
            Kind = PartyObserverChoiceSnapshotKind.CardRewardSelection,
            ScreenLabel = PartyObserverChoiceSnapshotKind.CardRewardSelection.GetDisplayName(),
            Title = PartyObserverText.ChoosingCard(),
            Description = PartyObserverText.FormatCardOptionsCount(options.Count)
        };

        foreach (var option in options)
        {
            snapshot.AddOption(BuildCardOption(option));
        }

        foreach (var extraOption in extraOptions)
        {
            snapshot.AddOption(new PartyObserverChoiceOption
            {
                Title = Sanitize(PartyObserverGameText.ResolveLocString(extraOption.Title)),
                Subtitle = string.IsNullOrWhiteSpace(extraOption.Hotkey)
                    ? PartyObserverText.Action()
                    : $"{PartyObserverText.Action()} - {extraOption.Hotkey}",
                Tag = PartyObserverText.ActionTagKey
            });
        }

        return snapshot;
    }

    public static PartyObserverChoiceSnapshot BuildEventChoices(NEventLayout eventLayout)
    {
        var buttons = eventLayout.OptionButtons.ToList();
        var eventTitle = buttons.FirstOrDefault() is { } firstButton
            ? PartyObserverGameText.ResolveLocString(firstButton.Event.Title)
            : string.Empty;

        var snapshot = new PartyObserverChoiceSnapshot
        {
            Kind = PartyObserverChoiceSnapshotKind.EventChoices,
            ScreenLabel = PartyObserverChoiceSnapshotKind.EventChoices.GetDisplayName(),
            Title = string.IsNullOrWhiteSpace(eventTitle) ? PartyObserverText.Event() : Sanitize(eventTitle),
            Description = PartyObserverText.FormatEventOptionsCount(buttons.Count)
        };

        foreach (var button in buttons)
        {
            snapshot.AddOption(BuildEventOption(button));
        }

        return snapshot;
    }

    private static PartyObserverChoiceOption BuildCardOption(CardCreationResult result)
    {
        var card = result.Card;
        return new PartyObserverChoiceOption
        {
            Title = Sanitize(PartyObserverGameText.ResolveCardTitle(card)),
            Subtitle = BuildCardSubtitle(card, result.HasBeenModified),
            Description = Sanitize(PartyObserverGameText.ResolveCardDescription(card)),
            Tag = PartyObserverText.CardTagKey,
            ImagePath = PartyObserverGameText.ResolveCardImagePath(card)
        };
    }

    private static PartyObserverChoiceOption BuildEventOption(NEventOptionButton button)
    {
        var option = button.Option;
        var title = Sanitize(PartyObserverGameText.ResolveLocString(option.Title));
        var description = Sanitize(PartyObserverGameText.ResolveLocString(option.Description));

        if (string.IsNullOrWhiteSpace(title))
        {
            title = description;
            description = string.Empty;
        }

        return new PartyObserverChoiceOption
        {
            Title = title,
            Subtitle = option.IsLocked
                ? PartyObserverText.Locked()
                : option.IsProceed
                    ? PartyObserverText.Proceed()
                    : PartyObserverText.EventChoice(),
            Description = description,
            Tag = option.IsProceed ? PartyObserverText.ProceedTagKey : PartyObserverText.EventTagKey,
            IsDisabled = option.IsLocked,
            IsProceed = option.IsProceed
        };
    }

    private static PartyObserverChoiceOption BuildRewardOption(Reward reward)
    {
        return reward switch
        {
            RelicReward relicReward => BuildRelicRewardOption(relicReward),
            CardReward cardReward => BuildCardRewardOption(cardReward),
            _ => new PartyObserverChoiceOption
            {
                Title = BuildRewardTitle(reward),
                Subtitle = BuildRewardSubtitle(reward),
                Description = BuildRewardDescription(reward),
                Tag = BuildRewardTag(reward),
                ImagePath = ResolveRewardIconPath(reward)
            }
        };
    }

    private static PartyObserverChoiceOption BuildRelicRewardOption(RelicReward reward)
    {
        var relic = RelicRewardRelicField?.GetValue(reward) as RelicModel;
        return new PartyObserverChoiceOption
        {
            Title = relic is null ? PartyObserverText.RelicReward() : Sanitize(PartyObserverGameText.ResolveRelicTitle(relic)),
            Subtitle = relic is null
                ? PartyObserverText.RelicReward()
                : $"{PartyObserverText.GetRelicRarity(relic.Rarity)} {PartyObserverText.Relic()}",
            Description = relic is null
                ? PartyObserverText.RewardAvailable()
                : Sanitize(PartyObserverGameText.ResolveRelicDescription(relic)),
            Tag = PartyObserverText.RelicTagKey,
            ImagePath = relic is null
                ? ResolveRewardIconPath(reward)
                : PartyObserverGameText.ResolveRelicImagePath(relic)
        };
    }

    private static PartyObserverChoiceOption BuildCardRewardOption(CardReward reward)
    {
        var cardResults = GetCardRewardCards(reward);
        var previewTitles = cardResults
            .Select(result => Sanitize(result.Card.Title))
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Take(3)
            .ToList();

        return new PartyObserverChoiceOption
        {
            Title = PartyObserverText.CardReward(),
            Subtitle = PartyObserverText.FormatCardOptionsCount(cardResults.Count),
            Description = previewTitles.Count == 0
                ? BuildRewardDescription(reward)
                : string.Join(" / ", previewTitles),
            Tag = PartyObserverText.CardTagKey,
            ImagePath = cardResults.FirstOrDefault() is { Card: { } previewCard }
                ? PartyObserverGameText.ResolveCardImagePath(previewCard)
                : ResolveRewardIconPath(reward)
        };
    }

    private static List<CardCreationResult> GetCardRewardCards(CardReward reward)
    {
        return CardRewardCardsField?.GetValue(reward) as List<CardCreationResult> ?? [];
    }

    private static string BuildRewardTitle(Reward reward)
    {
        return BuildRewardTag(reward) switch
        {
            "Gold" => PartyObserverText.GoldReward(),
            "Potion" => PartyObserverText.PotionReward(),
            "Card" => PartyObserverText.CardReward(),
            "Relic" => PartyObserverText.RelicReward(),
            "Action" => PartyObserverText.RewardAction(),
            _ => PartyObserverText.Reward()
        };
    }

    private static string BuildRewardSubtitle(Reward reward)
    {
        return BuildRewardTag(reward) switch
        {
            "Gold" => PartyObserverText.Resource(),
            "Potion" => PartyObserverText.Consumable(),
            "Card" => PartyObserverText.DeckReward(),
            "Relic" => PartyObserverText.PermanentItem(),
            "Action" => PartyObserverText.RewardAction(),
            _ => PartyObserverText.Reward()
        };
    }

    private static string BuildRewardDescription(Reward reward)
    {
        try
        {
            return Sanitize(PartyObserverGameText.ResolveLocString(reward.Description));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildRewardTag(Reward reward)
    {
        return reward switch
        {
            RelicReward => PartyObserverText.RelicTagKey,
            CardReward => PartyObserverText.CardTagKey,
            GoldReward => PartyObserverText.GoldTagKey,
            PotionReward => PartyObserverText.PotionTagKey,
            CardRemovalReward => PartyObserverText.ActionTagKey,
            SpecialCardReward => PartyObserverText.CardTagKey,
            _ => PartyObserverText.RewardTagKey
        };
    }

    private static string ResolveRewardIconPath(Reward reward)
    {
        try
        {
            return RewardIconPathGetter?.Invoke(reward, null) as string ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildCardSubtitle(CardModel card, bool isModified)
    {
        var pieces = new List<string>
        {
            PartyObserverText.GetCardType(card.Type),
            PartyObserverText.GetCardRarity(card.Rarity),
            PartyObserverText.FormatCost(DescribeCardCost(card))
        };

        if (isModified)
        {
            pieces.Add(PartyObserverText.Modified());
        }

        return string.Join(" - ", pieces);
    }

    private static string DescribeCardCost(CardModel card)
    {
        return card.EnergyCost.CostsX ? "X" : card.EnergyCost.GetResolved().ToString();
    }

    private static string Sanitize(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var normalized = rawText
            .Replace('\r', ' ')
            .Replace('\n', ' ');
        normalized = BbCodePattern.Replace(normalized, string.Empty);
        normalized = WhitespacePattern.Replace(normalized, " ");
        return normalized.Trim();
    }
}
