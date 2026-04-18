using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Factories;

namespace RandomVision.Services;

internal static class RandomVisionPreviewRegistry
{
    private static readonly decimal UpgradedCardOddScaling =
        AscensionHelper.GetValueIfAscension(AscensionLevel.Scarcity, 0.125m, 0.25m);

    private sealed class RewardPreviewState
    {
        public RewardPreviewState(Player player)
        {
            RewardRng = CloneRng(player.PlayerRng.Rewards);
            CardRarityOdds = new CardRarityOdds(player.PlayerOdds.CardRarity.CurrentValue, RewardRng);
        }

        public Rng RewardRng { get; }

        public CardRarityOdds CardRarityOdds { get; }
    }

    private static readonly AccessTools.FieldRef<SlipperyBridge, CardModel> SlipperyBridgeCardRef =
        AccessTools.FieldRefAccess<SlipperyBridge, CardModel>("_randomCardToLose");

    private static readonly AccessTools.FieldRef<StoneOfAllTime, PotionModel> StonePotionRef =
        AccessTools.FieldRefAccess<StoneOfAllTime, PotionModel>("_drinkAndLiftPotion");

    private static readonly AccessTools.FieldRef<WelcomeToWongos, RelicModel> WongosFeaturedItemRef =
        AccessTools.FieldRefAccess<WelcomeToWongos, RelicModel>("_featuredItem");

    private static readonly AccessTools.FieldRef<TheFutureOfPotions, Dictionary<PotionModel, CardType>?> FutureOfPotionsCardTypesRef =
        AccessTools.FieldRefAccess<TheFutureOfPotions, Dictionary<PotionModel, CardType>?>("_cardTypes");

    public static EventPreviewResult BuildEventPreview(EventModel eventModel)
    {
        var optionPreviews = BuildBaselineOptionPreviews(eventModel);

        try
        {
            ApplyEventSpecificPreview(eventModel, optionPreviews);
        }
        catch (Exception ex)
        {
            MainFile.LogError($"Failed to enrich preview for {eventModel.Id.Entry}", ex);
        }

        foreach (var optionPreview in optionPreviews)
        {
            FinalizePreview(optionPreview);
        }

        var eventTitle = RandomVisionGameText.ResolveLocString(eventModel.Title, eventModel.DynamicVars);
        return new EventPreviewResult(eventTitle, optionPreviews);
    }

    private static List<EventOptionPreview> BuildBaselineOptionPreviews(EventModel eventModel)
    {
        return eventModel.CurrentOptions
            .Select(option => BuildBaselineOptionPreview(eventModel, option))
            .ToList();
    }

    private static EventOptionPreview BuildBaselineOptionPreview(EventModel eventModel, EventOption option)
    {
        var title = RandomVisionGameText.ResolveLocString(option.Title, eventModel.DynamicVars);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = option.TextKey;
        }

        var preview = new EventOptionPreview(option, title, PreviewCoverage.AlreadyVisible);
        if (option.IsLocked)
        {
            AddLine(preview.Lines, "当前不可选。");
            return preview;
        }

        foreach (var hoverSummary in RandomVisionGameText.SummarizeHoverTips(option.HoverTips))
        {
            AddLine(preview.Lines, hoverSummary);
        }
        AddEntities(preview.Entities, RandomVisionGameText.ExtractPreviewEntities(option.HoverTips));

        if (preview.Lines.Count == 0)
        {
            var description = RandomVisionGameText.ResolveLocString(option.Description, eventModel.DynamicVars);
            if (!string.IsNullOrWhiteSpace(description) &&
                !string.Equals(description, preview.Title, StringComparison.OrdinalIgnoreCase))
            {
                AddLine(preview.Lines, description);
            }
        }

        return preview;
    }

    private static void ApplyEventSpecificPreview(EventModel eventModel, IList<EventOptionPreview> previews)
    {
        switch (eventModel)
        {
            case Neow neow:
                ApplyNeowPreview(neow, previews);
                break;
            case Trial trial:
                ApplyTrialPreview(trial, previews);
                break;
            case SlipperyBridge bridge:
                ApplySlipperyBridgePreview(bridge, previews);
                break;
            case DollRoom dollRoom:
                ApplyDollRoomPreview(dollRoom, previews);
                break;
            case Reflections reflections:
                ApplyReflectionsPreview(reflections, previews);
                break;
            case DoorsOfLightAndDark doors:
                ApplyDoorsPreview(doors, previews);
                break;
            case TrashHeap trashHeap:
                ApplyTrashHeapPreview(trashHeap, previews);
                break;
            case WelcomeToWongos wongos:
                ApplyWongosPreview(wongos, previews);
                break;
            case StoneOfAllTime stone:
                ApplyStonePreview(stone, previews);
                break;
            case RanwidTheElder ranwid:
                ApplyRanwidPreview(ranwid, previews);
                break;
            case BattlewornDummy dummy:
                ApplyBattlewornDummyPreview(dummy, previews);
                break;
            case ThisOrThat thisOrThat:
                ApplyThisOrThatPreview(thisOrThat, previews);
                break;
            case AromaOfChaos aromaOfChaos:
                ApplyAromaOfChaosPreview(aromaOfChaos, previews);
                break;
            case MorphicGrove morphicGrove:
                ApplyMorphicGrovePreview(morphicGrove, previews);
                break;
            case RelicTrader relicTrader:
                ApplyRelicTraderPreview(relicTrader, previews);
                break;
            case Wellspring wellspring:
                ApplyWellspringPreview(wellspring, previews);
                break;
            case WhisperingHollow whisperingHollow:
                ApplyWhisperingHollowPreview(whisperingHollow, previews);
                break;
            case SunkenTreasury sunkenTreasury:
                ApplySunkenTreasuryPreview(sunkenTreasury, previews);
                break;
            case Amalgamator amalgamator:
                ApplyAmalgamatorPreview(amalgamator, previews);
                break;
            case DenseVegetation denseVegetation:
                ApplyDenseVegetationPreview(denseVegetation, previews);
                break;
            case FieldOfManSizedHoles fieldOfManSizedHoles:
                ApplyFieldOfManSizedHolesPreview(fieldOfManSizedHoles, previews);
                break;
            case SapphireSeed sapphireSeed:
                ApplySapphireSeedPreview(sapphireSeed, previews);
                break;
            case Symbiote symbiote:
                ApplySymbiotePreview(symbiote, previews);
                break;
            case ZenWeaver zenWeaver:
                ApplyZenWeaverPreview(zenWeaver, previews);
                break;
            case MegaCrit.Sts2.Core.Models.Events.LostWisp lostWisp:
                ApplyLostWispPreview(lostWisp, previews);
                break;
            case LuminousChoir choir:
                ApplyLuminousChoirPreview(choir, previews);
                break;
            case BrainLeech brainLeech:
                ApplyBrainLeechPreview(brainLeech, previews);
                break;
            case InfestedAutomaton infestedAutomaton:
                ApplyInfestedAutomatonPreview(infestedAutomaton, previews);
                break;
            case RoomFullOfCheese roomFullOfCheese:
                ApplyRoomFullOfCheesePreview(roomFullOfCheese, previews);
                break;
            case PunchOff punchOff:
                ApplyPunchOffPreview(punchOff, previews);
                break;
            case TheFutureOfPotions theFutureOfPotions:
                ApplyTheFutureOfPotionsPreview(theFutureOfPotions, previews);
                break;
            case ColorfulPhilosophers colorfulPhilosophers:
                ApplyColorfulPhilosophersPreview(colorfulPhilosophers, previews);
                break;
            case ColossalFlower colossalFlower:
                ApplyColossalFlowerPreview(colossalFlower, previews);
                break;
            case DrowningBeacon drowningBeacon:
                ApplyDrowningBeaconPreview(drowningBeacon, previews);
                break;
            case EndlessConveyor endlessConveyor:
                ApplyEndlessConveyorPreview(endlessConveyor, previews);
                break;
            case GraveOfTheForgotten graveOfTheForgotten:
                ApplyGraveOfTheForgottenPreview(graveOfTheForgotten, previews);
                break;
            case HungryForMushrooms hungryForMushrooms:
                ApplyHungryForMushroomsPreview(hungryForMushrooms, previews);
                break;
            case JungleMazeAdventure jungleMazeAdventure:
                ApplyJungleMazeAdventurePreview(jungleMazeAdventure, previews);
                break;
            case PotionCourier potionCourier:
                ApplyPotionCourierPreview(potionCourier, previews);
                break;
            case RoundTeaParty roundTeaParty:
                ApplyRoundTeaPartyPreview(roundTeaParty, previews);
                break;
            case SpiralingWhirlpool spiralingWhirlpool:
                ApplySpiralingWhirlpoolPreview(spiralingWhirlpool, previews);
                break;
            case SunkenStatue sunkenStatue:
                ApplySunkenStatuePreview(sunkenStatue, previews);
                break;
            case TeaMaster teaMaster:
                ApplyTeaMasterPreview(teaMaster, previews);
                break;
            case TheLegendsWereTrue theLegendsWereTrue:
                ApplyTheLegendsWereTruePreview(theLegendsWereTrue, previews);
                break;
            case TinkerTime tinkerTime:
                ApplyTinkerTimePreview(tinkerTime, previews);
                break;
            case UnrestSite unrestSite:
                ApplyUnrestSitePreview(unrestSite, previews);
                break;
            case WarHistorianRepy warHistorianRepy:
                ApplyWarHistorianRepyPreview(warHistorianRepy, previews);
                break;
            case WaterloggedScriptorium waterloggedScriptorium:
                ApplyWaterloggedScriptoriumPreview(waterloggedScriptorium, previews);
                break;
            case WoodCarvings woodCarvings:
                ApplyWoodCarvingsPreview(woodCarvings, previews);
                break;
        }
    }

    private static void ApplyNeowPreview(Neow neow, IList<EventOptionPreview> previews)
    {
        if (neow.Owner is null)
        {
            return;
        }

        var handledAny = false;
        foreach (var optionPreview in previews.Where(optionPreview => !optionPreview.SourceOption.IsLocked))
        {
            switch (optionPreview.SourceOption.Relic)
            {
                case LargeCapsule:
                {
                    var capsuleRelics = PeekNextRelics(neow.Owner, 2).ToList();
                    optionPreview.Coverage = PreviewCoverage.Complete;

                    if (capsuleRelics.Count == 0)
                    {
                        AddLine(optionPreview.Lines, "巨大扭蛋会再随机获得 2 件遗物。");
                    }
                    else
                    {
                        AddLine(optionPreview.Lines, $"巨大扭蛋会再获得 {JoinRelics(capsuleRelics)}。");
                        AddEntities(optionPreview.Entities, CreateRelicEntities(capsuleRelics));
                    }

                    handledAny = true;
                    break;
                }
                case ArcaneScroll:
                {
                    var options = new CardCreationOptions(
                            new[] { neow.Owner.Character.CardPool },
                            CardCreationSource.Other,
                            CardRarityOddsType.Uniform,
                            card => card.Rarity == CardRarity.Rare)
                        .WithFlags(CardCreationFlags.NoUpgradeRoll);
                    var cards = PeekRewardCards(neow.Owner, options, 1).ToList();
                    optionPreview.Coverage = PreviewCoverage.Complete;

                    if (cards.Count == 0)
                    {
                        AddLine(optionPreview.Lines, "奥数卷轴会再获得 1 张稀有牌。");
                    }
                    else
                    {
                        AddLine(optionPreview.Lines, $"奥数卷轴会再获得 {JoinCards(cards)}。");
                        AddEntities(optionPreview.Entities, CreateCardEntities(cards));
                    }

                    handledAny = true;
                    break;
                }
                case LeadPaperweight:
                {
                    var options = new CardCreationOptions(
                        new[] { ModelDb.CardPool<ColorlessCardPool>() },
                        CardCreationSource.Other,
                        CardRarityOddsType.RegularEncounter);
                    var cards = PeekRewardCards(neow.Owner, options, 2).ToList();
                    optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;

                    if (cards.Count == 0)
                    {
                        AddLine(optionPreview.Lines, "铅制镇纸会出现 2 张无色牌供你选择，可跳过。");
                    }
                    else
                    {
                        AddLine(optionPreview.Lines, $"铅制镇纸会出现 {JoinCards(cards)} 供你 2 选 1，可跳过。");
                        AddEntities(optionPreview.Entities, CreateCardEntities(cards));
                    }

                    handledAny = true;
                    break;
                }
                case SmallCapsule:
                {
                    var relic = PeekNextRelics(neow.Owner, 1).FirstOrDefault();
                    optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;

                    if (relic is null)
                    {
                        AddLine(optionPreview.Lines, "小扭蛋会出现 1 件遗物供你选择。");
                    }
                    else
                    {
                        AddLine(optionPreview.Lines, $"小扭蛋会出现 {RelicTitle(relic)}。");
                        AddEntities(optionPreview.Entities, CreateRelicEntities(new[] { relic }));
                    }

                    handledAny = true;
                    break;
                }
                case MassiveScroll:
                {
                    var customCardPool = ModelDb.CardPool<ColorlessCardPool>()
                        .GetUnlockedCards(neow.Owner.RunState.UnlockState, neow.Owner.RunState.CardMultiplayerConstraint)
                        .Concat(neow.Owner.Character.CardPool.GetUnlockedCards(neow.Owner.RunState.UnlockState, neow.Owner.RunState.CardMultiplayerConstraint))
                        .Where(card => card.MultiplayerConstraint == CardMultiplayerConstraint.MultiplayerOnly);
                    var options = new CardCreationOptions(customCardPool, CardCreationSource.Other, CardRarityOddsType.RegularEncounter);
                    var cards = PeekRewardCards(neow.Owner, options, 3).ToList();
                    optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;

                    if (cards.Count == 0)
                    {
                        AddLine(optionPreview.Lines, "巨型卷轴会出现 3 张多人专属牌供你选择，可跳过。");
                    }
                    else
                    {
                        AddLine(optionPreview.Lines, $"巨型卷轴会出现 {JoinCards(cards)} 供你 3 选 1，可跳过。");
                        AddEntities(optionPreview.Entities, CreateCardEntities(cards));
                    }

                    handledAny = true;
                    break;
                }
                case LostCoffer:
                {
                    var options = new CardCreationOptions(
                        new[] { neow.Owner.Character.CardPool },
                        CardCreationSource.Other,
                        CardRarityOddsType.RegularEncounter);
                    var cards = PeekRewardCards(neow.Owner, options, 3).ToList();
                    var potion = PeekSharedRewardPotion(neow.Owner);
                    optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;

                    if (cards.Count == 0)
                    {
                        AddLine(optionPreview.Lines, "失落宝箱会出现 1 组 3 张牌奖励。");
                    }
                    else
                    {
                        AddLine(optionPreview.Lines, $"失落宝箱会出现 {JoinCards(cards)} 供你 3 选 1。");
                        AddEntities(optionPreview.Entities, CreateCardEntities(cards));
                    }

                    AddLine(optionPreview.Lines, potion is null ? "同时会再获得 1 瓶药水。" : $"同时会再获得 {PotionTitle(potion)}。");
                    if (potion is not null)
                    {
                        AddEntities(optionPreview.Entities, RandomVisionGameText.ExtractPreviewEntities(potion.HoverTips));
                    }

                    handledAny = true;
                    break;
                }
                case LeafyPoultice:
                {
                    var transformedCards = PredictLeafyPoulticeTransformCards(neow.Owner);
                    optionPreview.Coverage = PreviewCoverage.Complete;

                    if (transformedCards.Count == 0)
                    {
                        AddLine(optionPreview.Lines, "当前没有可被树叶药膏变化的基础打击或基础防御。");
                    }
                    else
                    {
                        foreach (var transformedCard in transformedCards)
                        {
                            AddLine(optionPreview.Lines,
                                $"{CardTitle(transformedCard.Original)} 会变成 {CardTitle(transformedCard.Transformed)}。");
                        }

                        AddEntities(optionPreview.Entities, CreateCardEntities(
                            transformedCards.Select(result => result.Transformed)));
                    }

                    handledAny = true;
                    break;
                }
                case NewLeaf:
                {
                    optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;
                    foreach (var line in BuildTransformSelectionPreview(neow.Owner, neow.Owner.RunState.Rng.Niche, 1))
                    {
                        AddLine(optionPreview.Lines, line);
                    }

                    handledAny = true;
                    break;
                }
                case ScrollBoxes:
                {
                    var bundles = PeekScrollBoxesBundles(neow.Owner);
                    optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;
                    AddLine(optionPreview.Lines, $"卷轴盒会先失去全部金币（当前 {neow.Owner.Gold}）。");

                    if (bundles.Count == 0)
                    {
                        AddLine(optionPreview.Lines, "之后会出现 2 组随机卡牌 bundle 供你选择。");
                    }
                    else
                    {
                        for (var bundleIndex = 0; bundleIndex < bundles.Count; bundleIndex++)
                        {
                            AddLine(optionPreview.Lines, $"Bundle {bundleIndex + 1}：{JoinCards(bundles[bundleIndex])}。");
                        }

                        AddEntities(optionPreview.Entities, CreateCardEntities(bundles.SelectMany(bundle => bundle).ToList()));
                    }

                    handledAny = true;
                    break;
                }
            }
        }

        if (handledAny)
        {
            return;
        }

        var largeCapsulePreview = previews.FirstOrDefault(optionPreview =>
            !optionPreview.SourceOption.IsLocked &&
            optionPreview.SourceOption.Relic is LargeCapsule);
        if (largeCapsulePreview is null)
        {
            return;
        }

        var relics = PeekNextRelics(neow.Owner, 2).ToList();
        largeCapsulePreview.Coverage = PreviewCoverage.Complete;

        if (relics.Count == 0)
        {
            AddLine(largeCapsulePreview.Lines, "巨大扭蛋会再随机获得 2 件遗物。");
            return;
        }

        AddLine(largeCapsulePreview.Lines, $"巨大扭蛋会再获得 {JoinRelics(relics)}。");
        AddEntities(largeCapsulePreview.Entities, CreateRelicEntities(relics));
    }

    private static void ApplyTrialPreview(Trial trial, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "DOUBLE_DOWN", out _))
        {
            var acceptLines = BuildTrialAcceptLines(CloneRng(trial.Rng).NextInt(3));
            SetPreview(previews, "ACCEPT", PreviewCoverage.Complete, acceptLines);
            SetPreview(previews, "DOUBLE_DOWN", PreviewCoverage.Complete, "直接弃掉本次 run。");
            return;
        }

        if (TryGetPreviewByTextKey(previews, "INITIAL.options.ACCEPT", out _))
        {
            var acceptLines = BuildTrialAcceptLines(CloneRng(trial.Rng).NextInt(3));
            SetPreview(previews, "INITIAL.options.ACCEPT", PreviewCoverage.Complete, acceptLines);
            SetPreview(previews, "INITIAL.options.REJECT", PreviewCoverage.Complete,
                "会先进入拒绝页。",
                "下一步可重新接受审判，或双倍下注直接弃局。");
            return;
        }

        if (TryGetPreviewByTextKey(previews, "MERCHANT.options.GUILTY", out _))
        {
            var merchantRelics = PeekNextRelics(trial.Owner!, 2).ToList();
            SetPreview(previews, "MERCHANT.options.GUILTY", PreviewCoverage.Complete,
                "获得诅咒遗憾。",
                merchantRelics.Count == 0 ? "再获得 2 件遗物。" : $"再获得 {JoinRelics(merchantRelics)}。");
            SetPreview(previews, "MERCHANT.options.INNOCENT", PreviewCoverage.PartialNeedsInput,
                "获得诅咒羞耻。",
                "还需要再选择 2 张牌进行升级。");
            return;
        }

        if (TryGetPreviewByTextKey(previews, "NOBLE.options.GUILTY", out _))
        {
            SetPreview(previews, "NOBLE.options.GUILTY", PreviewCoverage.Complete, "回复 10 点生命。");
            SetPreview(previews, "NOBLE.options.INNOCENT", PreviewCoverage.Complete,
                "获得诅咒遗憾。",
                "再获得 300 金币。");
            return;
        }

        if (TryGetPreviewByTextKey(previews, "NONDESCRIPT.options.GUILTY", out _))
        {
            SetPreview(previews, "NONDESCRIPT.options.GUILTY", PreviewCoverage.PartialNeedsInput,
                "获得诅咒怀疑。",
                "之后会出现 2 组选卡奖励。");
            var innocentLines = new List<string>
            {
                "获得诅咒怀疑。"
            };
            innocentLines.AddRange(BuildTransformSelectionPreview(trial.Owner!, trial.Owner!.RunState.Rng.Niche, 2));
            SetPreview(previews, "NONDESCRIPT.options.INNOCENT", PreviewCoverage.PartialNeedsInput, innocentLines);
        }
    }

    private static IReadOnlyList<(CardModel Original, CardModel Transformed)> PredictLeafyPoulticeTransformCards(Player player)
    {
        var basics = PileType.Deck.GetPile(player).Cards
            .Where(card => card.Rarity == CardRarity.Basic)
            .ToList();
        var strike = basics.FirstOrDefault(card => card.Tags.Contains(CardTag.Strike));
        var defend = basics.FirstOrDefault(card => card.Tags.Contains(CardTag.Defend));
        var rng = CloneRng(player.PlayerRng.Transformations);
        var results = new List<(CardModel Original, CardModel Transformed)>();

        if (strike is not null)
        {
            results.Add((strike, CardFactory.CreateRandomCardForTransform(strike, isInCombat: false, rng)));
        }

        if (defend is not null)
        {
            results.Add((defend, CardFactory.CreateRandomCardForTransform(defend, isInCombat: false, rng)));
        }

        return results;
    }

    private static string[] BuildTrialAcceptLines(int branch)
    {
        return branch switch
        {
            0 => new[]
            {
                "会进入商人审判。",
                "后续有罪：获得遗憾，并拿到 2 件遗物。",
                "后续无罪：获得羞耻，并升级 2 张牌。"
            },
            1 => new[]
            {
                "会进入贵族审判。",
                "后续有罪：回复 10 点生命。",
                "后续无罪：获得遗憾，并得到 300 金币。"
            },
            _ => new[]
            {
                "会进入无名者审判。",
                "后续有罪：获得怀疑，并出现 2 组选卡奖励。",
                "后续无罪：获得怀疑，并转化 2 张牌。"
            }
        };
    }

    private static void ApplySlipperyBridgePreview(SlipperyBridge bridge, IList<EventOptionPreview> previews)
    {
        if (!TryGetPreviewByTextKey(previews, "OVERCOME", out _))
        {
            return;
        }

        var currentCard = SlipperyBridgeCardRef(bridge);
        if (currentCard is not null)
        {
            SetPreview(previews, "OVERCOME", PreviewCoverage.Complete,
                $"立刻跨桥 -> 失去 {CardTitle(currentCard)}。");
        }

        if (!TryGetPreviewByTextKey(previews, "HOLD_ON", out _))
        {
            return;
        }

        var lines = new List<string>
        {
            $"先承受 {bridge.DynamicVars["HpLoss"].IntValue} 点伤害。"
        };

        var nextCard = PredictNextSlipperyBridgeCard(bridge, currentCard);
        if (nextCard is not null)
        {
            lines.Add($"下一轮会改为失去 {CardTitle(nextCard)}。");
        }

        lines.Add("之后仍可继续坚持，或直接跨桥。");
        SetPreview(previews, "HOLD_ON", PreviewCoverage.Complete, lines);
    }

    private static void ApplyDollRoomPreview(DollRoom dollRoom, IList<EventOptionPreview> previews)
    {
        if (!TryGetPreviewByTextKey(previews, "INITIAL.options.RANDOM", out _))
        {
            return;
        }

        var randomChoice = CloneRng(dollRoom.Owner!.RunState.Rng.Niche).NextItem(GetDollChoices());
        if (randomChoice is not null)
        {
            SetPreview(previews, "INITIAL.options.RANDOM", PreviewCoverage.Complete,
                $"会直接获得 {RelicTitle(randomChoice)}。");
        }

        var takeSomeTimeChoices = GetDollChoices()
            .ToList()
            .StableShuffle(CloneRng(dollRoom.Rng))
            .Take(2)
            .Select(RelicTitle)
            .ToList();
        SetPreview(previews, "INITIAL.options.TAKE_SOME_TIME", PreviewCoverage.Complete,
            $"先失去 {dollRoom.DynamicVars["TakeTimeHpLoss"].IntValue} 点生命。",
            $"下一页会出现 2 个确定选项：{JoinTitles(takeSomeTimeChoices)}。");

        var examineChoices = GetDollChoices()
            .ToList()
            .StableShuffle(CloneRng(dollRoom.Rng))
            .Select(RelicTitle)
            .ToList();
        SetPreview(previews, "INITIAL.options.EXAMINE", PreviewCoverage.Complete,
            $"先失去 {dollRoom.DynamicVars["ExamineHpLoss"].IntValue} 点生命。",
            $"下一页会按顺序出现全部娃娃：{JoinTitles(examineChoices)}。");
    }

    private static void ApplyReflectionsPreview(Reflections reflections, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "TOUCH_A_MIRROR", out _))
        {
            SetPreview(previews, "TOUCH_A_MIRROR", PreviewCoverage.Complete, BuildReflectionsLines(reflections));
        }

        if (TryGetPreviewByTextKey(previews, "SHATTER", out _))
        {
            SetPreview(previews, "SHATTER", PreviewCoverage.Complete,
                "复制整副牌。",
                $"再加入 {CardTitle(ModelDb.Card<BadLuck>())}。");
        }
    }

    private static void ApplyDoorsPreview(DoorsOfLightAndDark doors, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "LIGHT", out _))
        {
            var cards = doors.Owner!.Deck.Cards
                .Where(card => card?.IsUpgradable ?? false)
                .ToList();
            var upgraded = cards
                .StableShuffle(CloneRng(doors.Owner.RunState.Rng.Niche))
                .Take(doors.DynamicVars.Cards.IntValue)
                .ToList();

            if (upgraded.Count > 0)
            {
                SetPreview(previews, "LIGHT", PreviewCoverage.Complete,
                    $"升级 {JoinCards(upgraded)}。");
            }
            else
            {
                SetPreview(previews, "LIGHT", PreviewCoverage.Complete, "当前没有可升级的牌。");
            }
        }

        if (TryGetPreviewByTextKey(previews, "DARK", out _))
        {
            SetPreview(previews, "DARK", PreviewCoverage.PartialNeedsInput,
                "还需要再选择 1 张牌移除。");
        }
    }

    private static void ApplyTrashHeapPreview(TrashHeap trashHeap, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "DIVE_IN", out _))
        {
            var diveRelic = CloneRng(trashHeap.Rng).NextItem(new RelicModel[]
            {
                ModelDb.Relic<DarkstonePeriapt>(),
                ModelDb.Relic<DreamCatcher>(),
                ModelDb.Relic<HandDrill>(),
                ModelDb.Relic<MawBank>(),
                ModelDb.Relic<TheBoot>()
            });

            if (diveRelic is not null)
            {
                SetPreview(previews, "DIVE_IN", PreviewCoverage.Complete,
                    $"先失去 {trashHeap.DynamicVars.HpLoss.IntValue} 点生命。",
                    $"再获得 {RelicTitle(diveRelic)}。");
            }
        }

        if (TryGetPreviewByTextKey(previews, "GRAB", out _))
        {
            var grabCard = CloneRng(trashHeap.Rng).NextItem(new CardModel[]
            {
                ModelDb.Card<Caltrops>(),
                ModelDb.Card<Clash>(),
                ModelDb.Card<Distraction>(),
                ModelDb.Card<DualWield>(),
                ModelDb.Card<Entrench>(),
                ModelDb.Card<HelloWorld>(),
                ModelDb.Card<Outmaneuver>(),
                ModelDb.Card<Rebound>(),
                ModelDb.Card<RipAndTear>(),
                ModelDb.Card<Stack>()
            });

            if (grabCard is not null)
            {
                SetPreview(previews, "GRAB", PreviewCoverage.Complete,
                    $"获得 {trashHeap.DynamicVars.Gold.IntValue} 金币。",
                    $"再拿到 {CardTitle(grabCard)}。");
            }
        }
    }

    private static void ApplyWongosPreview(WelcomeToWongos wongos, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "BARGAIN_BIN", out var bargainBinPreview) && !bargainBinPreview.SourceOption.IsLocked)
        {
            var commonRelic = PeekNextRelics(wongos.Owner!, 1, RelicRarity.Common).FirstOrDefault();
            if (commonRelic is not null)
            {
                SetPreview(previews, "BARGAIN_BIN", PreviewCoverage.Complete,
                    $"花费 {wongos.DynamicVars["BargainBinCost"].IntValue} 金币。",
                    $"会拿到 {RelicTitle(commonRelic)}。");
            }
        }

        if (TryGetPreviewByTextKey(previews, "FEATURED_ITEM", out var featuredPreview) && !featuredPreview.SourceOption.IsLocked)
        {
            var featuredItem = WongosFeaturedItemRef(wongos);
            if (featuredItem is not null)
            {
                SetPreview(previews, "FEATURED_ITEM", PreviewCoverage.Complete,
                    $"花费 {wongos.DynamicVars["FeaturedItemCost"].IntValue} 金币。",
                    $"会拿到 {RelicTitle(featuredItem)}。");
            }
        }

        if (TryGetPreviewByTextKey(previews, "MYSTERY_BOX", out var mysteryPreview) && !mysteryPreview.SourceOption.IsLocked)
        {
            SetPreview(previews, "MYSTERY_BOX", PreviewCoverage.Complete,
                $"花费 {wongos.DynamicVars["MysteryBoxCost"].IntValue} 金币。",
                $"会拿到 {RelicTitle(ModelDb.Relic<WongosMysteryTicket>())}。");
        }

        if (TryGetPreviewByTextKey(previews, "LEAVE", out _))
        {
            var downgraded = CloneRng(wongos.Rng).NextItem(wongos.Owner!.Deck.Cards.Where(card => card.IsUpgraded));
            SetPreview(previews, "LEAVE", PreviewCoverage.Complete,
                downgraded is null
                    ? "离开时不会降级任何牌。"
                    : $"离开时会降级 {CardTitle(downgraded)}。");
        }
    }

    private static void ApplyStonePreview(StoneOfAllTime stone, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "LIFT", out var liftPreview) && !liftPreview.SourceOption.IsLocked)
        {
            var potion = StonePotionRef(stone);
            if (potion is not null)
            {
                SetPreview(previews, "LIFT", PreviewCoverage.Complete,
                    $"喝掉 {PotionTitle(potion)}。",
                    $"再增加 {stone.DynamicVars["DrinkMaxHpGain"].IntValue} 点最大生命。");
            }
        }

        if (TryGetPreviewByTextKey(previews, "PUSH", out var pushPreview) && !pushPreview.SourceOption.IsLocked)
        {
            SetPreview(previews, "PUSH", PreviewCoverage.PartialNeedsInput,
                $"先失去 {stone.DynamicVars["PushHpLoss"].IntValue} 点生命。",
                $"还需要再选择 1 张牌，施加 {stone.DynamicVars["PushVigorousAmount"].IntValue} 层 Vigorous。");
        }
    }

    private static void ApplyRanwidPreview(RanwidTheElder ranwid, IList<EventOptionPreview> previews)
    {
        var rewardRelic = PeekNextRelics(ranwid.Owner!, 1).FirstOrDefault();
        var rewardRelics = PeekNextRelics(ranwid.Owner!, 2).ToList();

        if (TryGetStringVar(ranwid, "Potion", out var potionName))
        {
            SetPreview(previews, "INITIAL.options.POTION", PreviewCoverage.Complete,
                $"交出 {potionName}。",
                rewardRelic is null ? "随后获得 1 件遗物。" : $"随后获得 {RelicTitle(rewardRelic)}。");
        }

        if (TryGetPreviewByTextKey(previews, "INITIAL.options.GOLD", out _))
        {
            SetPreview(previews, "INITIAL.options.GOLD", PreviewCoverage.Complete,
                $"花费 {ranwid.DynamicVars.Gold.IntValue} 金币。",
                rewardRelic is null ? "随后获得 1 件遗物。" : $"随后获得 {RelicTitle(rewardRelic)}。");
        }

        if (TryGetStringVar(ranwid, "Relic", out var relicName))
        {
            SetPreview(previews, "INITIAL.options.RELIC", PreviewCoverage.Complete,
                $"交出 {relicName}。",
                rewardRelics.Count == 0 ? "随后获得 2 件遗物。" : $"随后获得 {JoinRelics(rewardRelics)}。");
        }
    }

    private static void ApplyBattlewornDummyPreview(BattlewornDummy dummy, IList<EventOptionPreview> previews)
    {
        var owner = dummy.Owner!;

        if (TryGetPreviewByTextKey(previews, "SETTING_1", out _))
        {
            var potion = PeekSharedRewardPotion(owner);
            SetPreview(previews, "SETTING_1", PreviewCoverage.PartialNeedsInput,
                potion is null
                    ? "战斗胜利后会获得 1 瓶药水；超时则无奖励。"
                    : $"战斗胜利后会获得 {PotionTitle(potion)}；超时则无奖励。");
        }

        if (TryGetPreviewByTextKey(previews, "SETTING_2", out _))
        {
            var upgraded = owner.Deck.Cards
                .Where(card => card?.IsUpgradable ?? false)
                .ToList()
                .StableShuffle(CloneRng(owner.RunState.Rng.Niche))
                .Take(2)
                .ToList();
            SetPreview(previews, "SETTING_2", PreviewCoverage.PartialNeedsInput,
                upgraded.Count == 0
                    ? "战斗胜利后不会升级任何牌；超时则无奖励。"
                    : $"战斗胜利后会升级 {JoinCards(upgraded)}；超时则无奖励。");
        }

        if (TryGetPreviewByTextKey(previews, "SETTING_3", out _))
        {
            var relic = PeekNextRelics(owner, 1).FirstOrDefault();
            SetPreview(previews, "SETTING_3", PreviewCoverage.PartialNeedsInput,
                relic is null
                    ? "战斗胜利后会获得下一件遗物；超时则无奖励。"
                    : $"战斗胜利后会获得 {RelicTitle(relic)}；超时则无奖励。");
        }
    }

    private static void ApplyThisOrThatPreview(ThisOrThat thisOrThat, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "PLAIN", out _))
        {
            SetPreview(previews, "PLAIN", PreviewCoverage.Complete,
                $"失去 {thisOrThat.DynamicVars.HpLoss.IntValue} 点生命。",
                $"再获得 {thisOrThat.DynamicVars.Gold.IntValue} 金币。");
        }

        if (TryGetPreviewByTextKey(previews, "ORNATE", out _))
        {
            var relic = PeekNextRelics(thisOrThat.Owner!, 1).FirstOrDefault();
            if (relic is not null)
            {
                SetPreview(previews, "ORNATE", PreviewCoverage.Complete,
                    $"获得 {RelicTitle(relic)}。",
                    $"再加入 {CardTitle(ModelDb.Card<Clumsy>())}。");
            }
        }
    }

    private static void ApplyAromaOfChaosPreview(AromaOfChaos aromaOfChaos, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "LET_GO", out _))
        {
            SetPreview(previews, "LET_GO", PreviewCoverage.PartialNeedsInput,
                BuildTransformSelectionPreview(aromaOfChaos.Owner!, aromaOfChaos.Rng, 1));
        }

        if (TryGetPreviewByTextKey(previews, "MAINTAIN_CONTROL", out _))
        {
            SetPreview(previews, "MAINTAIN_CONTROL", PreviewCoverage.PartialNeedsInput,
                "还需要先选择 1 张牌。",
                "选定后会升级该牌。");
        }
    }

    private static void ApplyMorphicGrovePreview(MorphicGrove morphicGrove, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "GROUP", out _))
        {
            var lines = new List<string>
            {
                $"先失去 {morphicGrove.DynamicVars.Gold.IntValue} 金币。"
            };
            lines.AddRange(BuildTransformSelectionPreview(morphicGrove.Owner!, morphicGrove.Owner!.RunState.Rng.Niche, 2));
            SetPreview(previews, "GROUP", PreviewCoverage.PartialNeedsInput, lines);
        }

        if (TryGetPreviewByTextKey(previews, "LONER", out _))
        {
            SetPreview(previews, "LONER", PreviewCoverage.Complete,
                $"增加 {morphicGrove.DynamicVars.MaxHp.IntValue} 点最大生命。");
        }
    }

    private static void ApplyRelicTraderPreview(RelicTrader relicTrader, IList<EventOptionPreview> previews)
    {
        ApplyRelicTraderOptionPreview(relicTrader, previews, "TOP", "TopRelicOwned", "TopRelicNew");
        ApplyRelicTraderOptionPreview(relicTrader, previews, "MIDDLE", "MiddleRelicOwned", "MiddleRelicNew");
        ApplyRelicTraderOptionPreview(relicTrader, previews, "BOTTOM", "BottomRelicOwned", "BottomRelicNew");

        if (TryGetPreviewByTextKey(previews, "PROCEED", out _))
        {
            SetPreview(previews, "PROCEED", PreviewCoverage.Complete, "当前没有可交易的遗物。");
        }
    }

    private static void ApplyRelicTraderOptionPreview(RelicTrader relicTrader, IList<EventOptionPreview> previews, string textKeySnippet, string ownedKey, string newKey)
    {
        if (!TryGetPreviewByTextKey(previews, textKeySnippet, out _))
        {
            return;
        }

        if (TryGetStringVar(relicTrader, ownedKey, out var ownedRelic) &&
            TryGetStringVar(relicTrader, newKey, out var newRelic))
        {
            SetPreview(previews, textKeySnippet, PreviewCoverage.Complete,
                $"交出 {ownedRelic}。",
                $"换成 {newRelic}。");
        }
    }

    private static void ApplyWellspringPreview(Wellspring wellspring, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "BOTTLE", out _))
        {
            var potion = PeekSharedRewardPotion(wellspring.Owner!);
            SetPreview(previews, "BOTTLE", PreviewCoverage.Complete,
                potion is null ? "会获得 1 瓶药水。" : $"会获得 {PotionTitle(potion)}。");
        }

        if (TryGetPreviewByTextKey(previews, "BATHE", out _))
        {
            SetPreview(previews, "BATHE", PreviewCoverage.PartialNeedsInput,
                "还需要先选择 1 张牌移除。",
                $"之后会加入 {wellspring.DynamicVars["BatheCurses"].IntValue} 张 {CardTitle(ModelDb.Card<Guilty>())}。");
        }
    }

    private static void ApplyWhisperingHollowPreview(WhisperingHollow whisperingHollow, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "GOLD", out _))
        {
            SetPreview(previews, "GOLD", PreviewCoverage.Complete,
                $"花费 {whisperingHollow.DynamicVars.Gold.IntValue} 金币。",
                "之后会获得 2 瓶药水。");
        }

        if (TryGetPreviewByTextKey(previews, "HUG", out _))
        {
            var lines = new List<string>
            {
                $"先失去 {whisperingHollow.DynamicVars.HpLoss.IntValue} 点生命。"
            };
            lines.AddRange(BuildTransformSelectionPreview(whisperingHollow.Owner!, whisperingHollow.Rng, 1));
            SetPreview(previews, "HUG", PreviewCoverage.PartialNeedsInput, lines);
        }
    }

    private static void ApplySunkenTreasuryPreview(SunkenTreasury sunkenTreasury, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "FIRST_CHEST", out _))
        {
            SetPreview(previews, "FIRST_CHEST", PreviewCoverage.Complete,
                $"获得 {sunkenTreasury.DynamicVars["SmallChestGold"].IntValue} 金币。");
        }

        if (TryGetPreviewByTextKey(previews, "SECOND_CHEST", out _))
        {
            SetPreview(previews, "SECOND_CHEST", PreviewCoverage.Complete,
                $"获得 {sunkenTreasury.DynamicVars["LargeChestGold"].IntValue} 金币。",
                $"再加入 {CardTitle(ModelDb.Card<Greed>())}。");
        }
    }

    private static void ApplyAmalgamatorPreview(Amalgamator amalgamator, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "COMBINE_STRIKES", out _))
        {
            SetPreview(previews, "COMBINE_STRIKES", PreviewCoverage.PartialNeedsInput,
                "还需要先选择 2 张可移除的基础攻击牌。",
                $"之后会加入 {CardTitle(ModelDb.Card<UltimateStrike>())}。");
        }

        if (TryGetPreviewByTextKey(previews, "COMBINE_DEFENDS", out _))
        {
            SetPreview(previews, "COMBINE_DEFENDS", PreviewCoverage.PartialNeedsInput,
                "还需要先选择 2 张可移除的基础防御牌。",
                $"之后会加入 {CardTitle(ModelDb.Card<UltimateDefend>())}。");
        }
    }

    private static void ApplyDenseVegetationPreview(DenseVegetation denseVegetation, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "TRUDGE_ON", out _))
        {
            SetPreview(previews, "TRUDGE_ON", PreviewCoverage.PartialNeedsInput,
                "还需要先选择 1 张牌移除。",
                $"之后再失去 {denseVegetation.DynamicVars.HpLoss.IntValue} 点生命。");
        }

        if (TryGetPreviewByTextKey(previews, "INITIAL.options.REST", out _))
        {
            SetPreview(previews, "INITIAL.options.REST", PreviewCoverage.Complete,
                $"先回复 {denseVegetation.DynamicVars.Heal.IntValue} 点生命。",
                "然后会进入下一页，且只能选择战斗。");
            return;
        }

        if (TryGetPreviewByTextKey(previews, "REST.options.FIGHT", out _))
        {
            SetPreview(previews, "REST.options.FIGHT", PreviewCoverage.Complete, "会直接进入战斗。");
        }
    }

    private static void ApplyFieldOfManSizedHolesPreview(FieldOfManSizedHoles fieldOfManSizedHoles, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "RESIST", out _))
        {
            SetPreview(previews, "RESIST", PreviewCoverage.PartialNeedsInput,
                $"还需要先选择 {fieldOfManSizedHoles.DynamicVars.Cards.IntValue} 张牌移除。",
                $"之后会加入 {CardTitle(ModelDb.Card<Normality>())}。");
        }

        if (TryGetPreviewByTextKey(previews, "ENTER_YOUR_HOLE", out _))
        {
            SetPreview(previews, "ENTER_YOUR_HOLE", PreviewCoverage.PartialNeedsInput,
                "还需要先选择 1 张牌。",
                $"选定后会附加 {RandomVisionGameText.ResolveModelTitle(ModelDb.Enchantment<PerfectFit>())}。");
        }
    }

    private static void ApplySapphireSeedPreview(SapphireSeed sapphireSeed, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "EAT", out _))
        {
            SetPreview(previews, "EAT", PreviewCoverage.PartialNeedsInput,
                $"先回复 {sapphireSeed.DynamicVars.Heal.IntValue} 点生命。",
                "还需要再选择 1 张牌进行升级。");
        }

        if (TryGetPreviewByTextKey(previews, "PLANT", out _))
        {
            var enchantmentName = TryGetStringVar(sapphireSeed, "Enchantment", out var sapphireEnchantment)
                ? sapphireEnchantment
                : RandomVisionGameText.ResolveModelTitle(ModelDb.Enchantment<Sown>());
            SetPreview(previews, "PLANT", PreviewCoverage.PartialNeedsInput,
                "还需要先选择 1 张可附魔的牌。",
                $"选定后会附加 {enchantmentName}。");
        }
    }

    private static void ApplySymbiotePreview(Symbiote symbiote, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "APPROACH", out var approachPreview) && !approachPreview.SourceOption.IsLocked)
        {
            var enchantmentName = TryGetStringVar(symbiote, "Enchantment", out var symbioteEnchantment)
                ? symbioteEnchantment
                : RandomVisionGameText.ResolveModelTitle(ModelDb.Enchantment<Corrupted>());
            SetPreview(previews, "APPROACH", PreviewCoverage.PartialNeedsInput,
                "还需要先选择 1 张牌。",
                $"选定后会附加 {enchantmentName}。");
        }

        if (TryGetPreviewByTextKey(previews, "KILL_WITH_FIRE", out _))
        {
            SetPreview(previews, "KILL_WITH_FIRE", PreviewCoverage.PartialNeedsInput,
                BuildTransformSelectionPreview(symbiote.Owner!, symbiote.Rng, symbiote.DynamicVars.Cards.IntValue));
        }
    }

    private static void ApplyZenWeaverPreview(ZenWeaver zenWeaver, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "BREATHING_TECHNIQUES", out _))
        {
            SetPreview(previews, "BREATHING_TECHNIQUES", PreviewCoverage.Complete,
                $"花费 {zenWeaver.DynamicVars["BreathingTechniquesCost"].IntValue} 金币。",
                $"会加入 2 张 {CardTitle(ModelDb.Card<Enlightenment>())}。");
        }

        if (TryGetPreviewByTextKey(previews, "EMOTIONAL_AWARENESS", out var emotionalPreview) && !emotionalPreview.SourceOption.IsLocked)
        {
            SetPreview(previews, "EMOTIONAL_AWARENESS", PreviewCoverage.PartialNeedsInput,
                $"花费 {zenWeaver.DynamicVars["EmotionalAwarenessCost"].IntValue} 金币。",
                "还需要先选择 1 张牌移除。");
        }

        if (TryGetPreviewByTextKey(previews, "ARACHNID_ACUPUNCTURE", out var arachnidPreview) && !arachnidPreview.SourceOption.IsLocked)
        {
            SetPreview(previews, "ARACHNID_ACUPUNCTURE", PreviewCoverage.PartialNeedsInput,
                $"花费 {zenWeaver.DynamicVars["ArachnidAcupunctureCost"].IntValue} 金币。",
                "还需要先选择 2 张牌移除。");
        }
    }

    private static void ApplyLostWispPreview(MegaCrit.Sts2.Core.Models.Events.LostWisp lostWisp, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "CLAIM", out _))
        {
            SetPreview(previews, "CLAIM", PreviewCoverage.Complete,
                $"获得 {RelicTitle(ModelDb.Relic<MegaCrit.Sts2.Core.Models.Relics.LostWisp>())}。",
                $"再加入 {CardTitle(ModelDb.Card<Decay>())}。");
        }

        if (TryGetPreviewByTextKey(previews, "SEARCH", out _))
        {
            SetPreview(previews, "SEARCH", PreviewCoverage.Complete,
                $"获得 {lostWisp.DynamicVars.Gold.IntValue} 金币。");
        }
    }

    private static void ApplyLuminousChoirPreview(LuminousChoir choir, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "REACH_INTO_THE_FLESH", out _))
        {
            SetPreview(previews, "REACH_INTO_THE_FLESH", PreviewCoverage.PartialNeedsInput,
                "还需要再选择 2 张牌移除。",
                $"之后会加入 {CardTitle(ModelDb.Card<SporeMind>())}。");
        }

        if (TryGetPreviewByTextKey(previews, "OFFER_TRIBUTE", out var tributePreview) && !tributePreview.SourceOption.IsLocked)
        {
            var relic = PeekNextRelics(choir.Owner!, 1).FirstOrDefault();
            if (relic is not null)
            {
                SetPreview(previews, "OFFER_TRIBUTE", PreviewCoverage.Complete,
                    $"花费 {choir.DynamicVars.Gold.IntValue} 金币。",
                    $"会拿到 {RelicTitle(relic)}。");
            }
        }
    }

    private static void ApplyBrainLeechPreview(BrainLeech brainLeech, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "SHARE_KNOWLEDGE", out _))
        {
            var owner = brainLeech.Owner!;
            var options = CardCreationOptions.ForNonCombatWithDefaultOdds(new[] { owner.Character.CardPool });
            var cards = PeekRewardCards(owner, options, brainLeech.DynamicVars["FromCardChoiceCount"].IntValue);

            var lines = new List<string>
            {
                $"会出现 {brainLeech.DynamicVars["FromCardChoiceCount"].IntValue} 张牌供你选 1 张。"
            };
            if (cards.Count > 0)
            {
                lines.Add($"实际候选：{JoinCards(cards)}。");
            }

            SetPreview(previews, "SHARE_KNOWLEDGE", PreviewCoverage.PartialNeedsInput, lines);
            SetEntities(previews, "SHARE_KNOWLEDGE", CreateCardEntities(cards));
        }

        if (TryGetPreviewByTextKey(previews, "RIP", out _))
        {
            var owner = brainLeech.Owner!;
            var options = CardCreationOptions.ForNonCombatWithDefaultOdds(new[] { ModelDb.CardPool<ColorlessCardPool>() });
            var cards = PeekRewardCards(owner, options, 3);

            var lines = new List<string>
            {
                $"先失去 {brainLeech.DynamicVars["RipHpLoss"].IntValue} 点生命。"
            };
            if (cards.Count > 0)
            {
                lines.Add($"之后会出现 3 张无色牌：{JoinCards(cards)}。");
            }
            else
            {
                lines.Add("之后会出现 3 张无色牌供你选择。");
            }

            SetPreview(previews, "RIP", PreviewCoverage.PartialNeedsInput, lines);
            SetEntities(previews, "RIP", CreateCardEntities(cards));
        }
    }

    private static void ApplyInfestedAutomatonPreview(InfestedAutomaton infestedAutomaton, IList<EventOptionPreview> previews)
    {
        var owner = infestedAutomaton.Owner!;

        if (TryGetPreviewByTextKey(previews, "STUDY", out _))
        {
            var options = CardCreationOptions.ForNonCombatWithDefaultOdds(
                new[] { owner.Character.CardPool },
                card => card.Type == CardType.Power);
            var cards = PeekRewardCards(owner, options, 1);
            if (cards.Count > 0)
            {
                SetPreview(previews, "STUDY", PreviewCoverage.Complete, $"会获得 {CardTitle(cards[0])}。");
                SetEntities(previews, "STUDY", CreateCardEntities(cards));
            }
        }

        if (TryGetPreviewByTextKey(previews, "TOUCH_CORE", out _))
        {
            var options = CardCreationOptions.ForNonCombatWithDefaultOdds(
                new[] { owner.Character.CardPool },
                card =>
                {
                    var energyCost = card.EnergyCost;
                    return energyCost is not null && energyCost.Canonical == 0 && !energyCost.CostsX;
                });
            var cards = PeekRewardCards(owner, options, 1);
            if (cards.Count > 0)
            {
                SetPreview(previews, "TOUCH_CORE", PreviewCoverage.Complete, $"会获得 {CardTitle(cards[0])}。");
                SetEntities(previews, "TOUCH_CORE", CreateCardEntities(cards));
            }
        }
    }

    private static void ApplyRoomFullOfCheesePreview(RoomFullOfCheese roomFullOfCheese, IList<EventOptionPreview> previews)
    {
        if (!TryGetPreviewByTextKey(previews, "GORGE", out _))
        {
            return;
        }

        var owner = roomFullOfCheese.Owner!;
        var options = CardCreationOptions
            .ForNonCombatWithUniformOdds(
                new[] { owner.Character.CardPool },
                card => card.Rarity == CardRarity.Common)
            .WithFlags(CardCreationFlags.NoRarityModification);
        var cards = PeekRewardCards(owner, options, 8);

        var lines = new List<string> { "会出现 8 张普通牌供你选 2 张。" };
        if (cards.Count > 0)
        {
            lines.Add($"实际候选：{JoinCards(cards)}。");
        }

        SetPreview(previews, "GORGE", PreviewCoverage.PartialNeedsInput, lines);
        SetEntities(previews, "GORGE", CreateCardEntities(cards));
    }

    private static void ApplyPunchOffPreview(PunchOff punchOff, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "NAB", out var nabPreview) && !nabPreview.SourceOption.IsLocked)
        {
            var relic = PeekNextRelics(punchOff.Owner!, 1).FirstOrDefault();
            SetPreview(previews, "NAB", PreviewCoverage.Complete,
                $"会加入 {CardTitle(ModelDb.Card<Injury>())}。",
                relic is null ? "然后获得 1 件遗物。" : $"然后获得 {RelicTitle(relic)}。");
            SetEntities(previews, "NAB", CreateCardEntities(new[] { ModelDb.Card<Injury>() }));
            if (relic is not null)
            {
                AddEntities(nabPreview.Entities, CreateRelicEntities(new[] { relic }));
            }
        }

        if (TryGetPreviewByTextKey(previews, "I_CAN_TAKE_THEM", out var takeThemPreview) && !takeThemPreview.SourceOption.IsLocked)
        {
            takeThemPreview.Coverage = PreviewCoverage.PartialNeedsInput;
            takeThemPreview.Lines.Clear();
            AddLine(takeThemPreview.Lines, "会先进入下一页。");
            AddLine(takeThemPreview.Lines, "下一页可选择开打；若战斗胜利，奖励包含 1 件遗物和 1 瓶药水。");
        }
    }

    private static void ApplyTheFutureOfPotionsPreview(TheFutureOfPotions theFutureOfPotions, IList<EventOptionPreview> previews)
    {
        var owner = theFutureOfPotions.Owner;
        if (owner is null)
        {
            return;
        }

        foreach (var preview in previews)
        {
            if (preview.SourceOption.IsLocked || !TryGetPotionFromOption(preview.SourceOption, out var potion))
            {
                continue;
            }

            if (!TryGetFuturePotionCardType(theFutureOfPotions, potion, preview.SourceOption, out var cardType))
            {
                continue;
            }

            var targetRarity = GetFuturePotionCardRarity(potion);
            var options = CardCreationOptions.ForNonCombatWithUniformOdds(
                    new[] { owner.Character.CardPool },
                    card => card.Rarity == targetRarity && card.Type == cardType)
                .WithFlags(CardCreationFlags.NoRarityModification);
            var cards = PeekRewardCards(owner, options, 3);
            if (cards.Count == 0)
            {
                continue;
            }

            var rarityText = targetRarity.ToLocString().GetFormattedText();
            var typeText = cardType.ToLocString().GetFormattedText();

            preview.Coverage = PreviewCoverage.PartialNeedsInput;
            preview.Lines.Clear();
            AddLine(preview.Lines, $"会先弃掉 {PotionTitle(potion)}。");
            AddLine(preview.Lines, $"会出现 3 张 {rarityText}{typeText} 候选牌，选择前会统一升级。");
            AddLine(preview.Lines, $"实际候选：{JoinCards(cards)}。");

            preview.Entities.Clear();
            AddEntities(preview.Entities, RandomVisionGameText.ExtractPreviewEntities(potion.HoverTips));
            AddEntities(preview.Entities, CreateCardEntities(cards));
        }
    }

    private static void ApplyColorfulPhilosophersPreview(ColorfulPhilosophers colorfulPhilosophers, IList<EventOptionPreview> previews)
    {
        foreach (var preview in previews)
        {
            var pool = GetColorfulPhilosophersPool(preview.SourceOption.TextKey);
            if (pool is null)
            {
                continue;
            }

            var state = new RewardPreviewState(colorfulPhilosophers.Owner!);
            var commonOptions = new CardCreationOptions(new[] { pool }, CardCreationSource.Other, CardRarityOddsType.Uniform, card => card.Rarity == CardRarity.Common)
                .WithFlags(CardCreationFlags.NoRarityModification);
            var uncommonOptions = new CardCreationOptions(new[] { pool }, CardCreationSource.Other, CardRarityOddsType.Uniform, card => card.Rarity == CardRarity.Uncommon)
                .WithFlags(CardCreationFlags.NoRarityModification);
            var rareOptions = new CardCreationOptions(new[] { pool }, CardCreationSource.Other, CardRarityOddsType.Uniform, card => card.Rarity == CardRarity.Rare)
                .WithFlags(CardCreationFlags.NoRarityModification);

            var commonCards = PeekRewardCards(colorfulPhilosophers.Owner!, state, commonOptions, colorfulPhilosophers.DynamicVars.Cards.IntValue);
            var uncommonCards = PeekRewardCards(colorfulPhilosophers.Owner!, state, uncommonOptions, colorfulPhilosophers.DynamicVars.Cards.IntValue);
            var rareCards = PeekRewardCards(colorfulPhilosophers.Owner!, state, rareOptions, colorfulPhilosophers.DynamicVars.Cards.IntValue);

            var lines = new List<string>();
            if (commonCards.Count > 0)
            {
                lines.Add($"普通候选：{JoinCards(commonCards)}。");
            }
            if (uncommonCards.Count > 0)
            {
                lines.Add($"非凡候选：{JoinCards(uncommonCards)}。");
            }
            if (rareCards.Count > 0)
            {
                lines.Add($"稀有候选：{JoinCards(rareCards)}。");
            }

            if (lines.Count > 0)
            {
                preview.Coverage = PreviewCoverage.PartialNeedsInput;
                preview.Lines.Clear();
                foreach (var line in lines)
                {
                    AddLine(preview.Lines, line);
                }
                preview.Entities.Clear();
                AddEntities(preview.Entities, CreateCardEntities(commonCards.Concat(uncommonCards).Concat(rareCards).ToList()));
            }
        }
    }

    private static CardPoolModel? GetColorfulPhilosophersPool(string textKey)
    {
        if (textKey.Contains("NECROBINDER", StringComparison.OrdinalIgnoreCase) ||
            textKey.Contains("WHITE", StringComparison.OrdinalIgnoreCase))
        {
            return ModelDb.CardPool<NecrobinderCardPool>();
        }
        if (textKey.Contains("IRONCLAD", StringComparison.OrdinalIgnoreCase) ||
            textKey.Contains("RED", StringComparison.OrdinalIgnoreCase))
        {
            return ModelDb.CardPool<IroncladCardPool>();
        }
        if (textKey.Contains("REGENT", StringComparison.OrdinalIgnoreCase) ||
            textKey.Contains("PURPLE", StringComparison.OrdinalIgnoreCase))
        {
            return ModelDb.CardPool<RegentCardPool>();
        }
        if (textKey.Contains("SILENT", StringComparison.OrdinalIgnoreCase) ||
            textKey.Contains("GREEN", StringComparison.OrdinalIgnoreCase))
        {
            return ModelDb.CardPool<SilentCardPool>();
        }
        if (textKey.Contains("DEFECT", StringComparison.OrdinalIgnoreCase) ||
            textKey.Contains("BLUE", StringComparison.OrdinalIgnoreCase))
        {
            return ModelDb.CardPool<DefectCardPool>();
        }

        return null;
    }

    private static void ApplyColossalFlowerPreview(ColossalFlower colossalFlower, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "EXTRACT_CURRENT_PRIZE", out _))
        {
            SetPreview(previews, "EXTRACT_CURRENT_PRIZE", PreviewCoverage.Complete,
                $"直接获得 {GetColossalFlowerGold(colossalFlower)} 金币。");
        }

        if (TryGetPreviewByTextKey(previews, "REACH_DEEPER", out _))
        {
            SetPreview(previews, "REACH_DEEPER", PreviewCoverage.Complete,
                $"先失去 {GetColossalFlowerDamage(colossalFlower)} 点生命。",
                "然后会进入下一层奖励页。");
        }

        if (TryGetPreviewByTextKey(previews, "EXTRACT_INSTEAD", out _))
        {
            SetPreview(previews, "EXTRACT_INSTEAD", PreviewCoverage.Complete,
                $"直接获得 {GetColossalFlowerGold(colossalFlower)} 金币。");
        }

        if (TryGetPreviewByTextKey(previews, "POLLINOUS_CORE", out _))
        {
            SetPreview(previews, "POLLINOUS_CORE", PreviewCoverage.Complete,
                $"先失去 {GetColossalFlowerDamage(colossalFlower)} 点生命。",
                $"然后获得 {RelicTitle(ModelDb.Relic<PollinousCore>())}。");
        }
    }

    private static int GetColossalFlowerGold(ColossalFlower colossalFlower)
    {
        return previewsafeGetNumber(colossalFlower, "_numberOfDigs") switch
        {
            0 => 35,
            1 => 75,
            _ => 135
        };
    }

    private static int GetColossalFlowerDamage(ColossalFlower colossalFlower)
    {
        return previewsafeGetNumber(colossalFlower, "_numberOfDigs") switch
        {
            0 => 5,
            1 => 6,
            _ => 7
        };
    }

    private static int previewsafeGetNumber<T>(T instance, string fieldName)
    {
        var field = AccessTools.Field(typeof(T), fieldName);
        return field is null ? 0 : (int)(field.GetValue(instance) ?? 0);
    }

    private static void ApplyDrowningBeaconPreview(DrowningBeacon drowningBeacon, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "BOTTLE", out _))
        {
            SetPreview(previews, "BOTTLE", PreviewCoverage.Complete,
                $"会获得 {PotionTitle(ModelDb.Potion<GlowwaterPotion>())}。");
        }

        if (TryGetPreviewByTextKey(previews, "CLIMB", out _))
        {
            SetPreview(previews, "CLIMB", PreviewCoverage.Complete,
                $"失去 {drowningBeacon.DynamicVars.HpLoss.IntValue} 点最大生命。",
                $"然后获得 {RelicTitle(ModelDb.Relic<FresnelLens>())}。");
        }
    }

    private static void ApplyEndlessConveyorPreview(EndlessConveyor endlessConveyor, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "OBSERVE_CHEF", out _))
        {
            var upgraded = CloneRng(endlessConveyor.Rng).NextItem(endlessConveyor.Owner!.Deck.Cards.Where(card => card.IsUpgradable));
            SetPreview(previews, "OBSERVE_CHEF", PreviewCoverage.Complete,
                upgraded is null ? "不会升级任何牌。" : $"会升级 {CardTitle(upgraded)}。");
        }

        if (TryGetStringVar(endlessConveyor, "CurrentDishTitle", out var currentDishTitle))
        {
            if (TryDescribeEndlessDish(endlessConveyor, currentDishTitle, out var dishLines))
            {
                var key = currentDishTitle;
                if (TryGetPreviewByTextKey(previews, "LOCKED", out var lockedPreview) && lockedPreview.SourceOption.IsLocked)
                {
                    key = "LOCKED";
                }

                SetPreview(previews, key, PreviewCoverage.PartialNeedsInput, dishLines);
            }
        }
    }

    private static bool TryDescribeEndlessDish(EndlessConveyor endlessConveyor, string currentDishTitle, out IReadOnlyList<string> lines)
    {
        var owner = endlessConveyor.Owner!;
        lines = Array.Empty<string>();
        var cardRewardState = new RewardPreviewState(owner);

        if (currentDishTitle == RandomVisionGameText.ResolveLocString(new LocString("events", "ENDLESS_CONVEYOR.DISHES.CLAM_ROLL.title")))
        {
            lines = new[] { $"当前料理：回复 {endlessConveyor.DynamicVars["ClamRollHeal"].IntValue} 点生命。", $"会花费 {endlessConveyor.DynamicVars.Gold.IntValue} 金币。" };
            return true;
        }
        if (currentDishTitle == RandomVisionGameText.ResolveLocString(new LocString("events", "ENDLESS_CONVEYOR.DISHES.CAVIAR.title")))
        {
            lines = new[] { $"当前料理：增加 {endlessConveyor.DynamicVars["CaviarMaxHp"].IntValue} 点最大生命。", $"会花费 {endlessConveyor.DynamicVars.Gold.IntValue} 金币。" };
            return true;
        }
        if (currentDishTitle == RandomVisionGameText.ResolveLocString(new LocString("events", "ENDLESS_CONVEYOR.DISHES.SUSPICIOUS_CONDIMENT.title")))
        {
            var potion = PeekSharedRewardPotion(owner);
            lines = new[] { potion is null ? "当前料理：获得 1 瓶药水。" : $"当前料理：获得 {PotionTitle(potion)}。", $"会花费 {endlessConveyor.DynamicVars.Gold.IntValue} 金币。" };
            return true;
        }
        if (currentDishTitle == RandomVisionGameText.ResolveLocString(new LocString("events", "ENDLESS_CONVEYOR.DISHES.JELLY_LIVER.title")))
        {
            lines = BuildTransformSelectionPreview(owner, endlessConveyor.Rng, 1)
                .Prepend($"会花费 {endlessConveyor.DynamicVars.Gold.IntValue} 金币。")
                .ToList();
            return true;
        }
        if (currentDishTitle == RandomVisionGameText.ResolveLocString(new LocString("events", "ENDLESS_CONVEYOR.DISHES.SEAPUNK_SALAD.title")))
        {
            lines = new[] { $"当前料理：加入 {CardTitle(ModelDb.Card<FeedingFrenzy>())}。", "每第 5 次抓取都会出现。" };
            return true;
        }
        if (currentDishTitle == RandomVisionGameText.ResolveLocString(new LocString("events", "ENDLESS_CONVEYOR.DISHES.FRIED_EEL.title")))
        {
            var eelCards = PeekRewardCards(owner, cardRewardState, CardCreationOptions.ForNonCombatWithDefaultOdds(new[] { ModelDb.CardPool<ColorlessCardPool>() }), 1);
            lines = new[] { eelCards.Count > 0 ? $"当前料理：加入 {CardTitle(eelCards[0])}。" : "当前料理：加入 1 张无色牌。", $"会花费 {endlessConveyor.DynamicVars.Gold.IntValue} 金币。" };
            return true;
        }
        if (currentDishTitle == RandomVisionGameText.ResolveLocString(new LocString("events", "ENDLESS_CONVEYOR.DISHES.GOLDEN_FYSH.title")))
        {
            lines = new[] { $"当前料理：获得 {endlessConveyor.DynamicVars["GoldenFyshGold"].IntValue} 金币。", "这次不会花钱。" };
            return true;
        }
        if (currentDishTitle == RandomVisionGameText.ResolveLocString(new LocString("events", "ENDLESS_CONVEYOR.DISHES.SPICY_SNAPPY.title")))
        {
            var upgraded = CloneRng(endlessConveyor.Rng).NextItem(owner.Deck.Cards.Where(card => card.IsUpgradable));
            lines = new[] { upgraded is null ? "当前料理：不会升级任何牌。" : $"当前料理：升级 {CardTitle(upgraded)}。", $"会花费 {endlessConveyor.DynamicVars.Gold.IntValue} 金币。" };
            return true;
        }

        return false;
    }

    private static void ApplyGraveOfTheForgottenPreview(GraveOfTheForgotten graveOfTheForgotten, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "CONFRONT", out var confrontPreview) && !confrontPreview.SourceOption.IsLocked)
        {
            SetPreview(previews, "CONFRONT", PreviewCoverage.PartialNeedsInput,
                $"会先加入 {CardTitle(ModelDb.Card<Decay>())}。",
                $"然后还需要选择 1 张牌，附加 {RandomVisionGameText.ResolveModelTitle(ModelDb.Enchantment<SoulsPower>())}。");
            SetEntities(previews, "CONFRONT", CreateCardEntities(new[] { ModelDb.Card<Decay>() }));
        }

        if (TryGetPreviewByTextKey(previews, "ACCEPT", out _))
        {
            SetPreview(previews, "ACCEPT", PreviewCoverage.Complete,
                $"会获得 {RelicTitle(ModelDb.Relic<ForgottenSoul>())}。");
            SetEntities(previews, "ACCEPT", CreateRelicEntities(new[] { ModelDb.Relic<ForgottenSoul>() }));
        }
    }

    private static void ApplyHungryForMushroomsPreview(HungryForMushrooms hungryForMushrooms, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "BIG_MUSHROOM", out _))
        {
            SetPreview(previews, "BIG_MUSHROOM", PreviewCoverage.Complete, $"会获得 {RelicTitle(ModelDb.Relic<BigMushroom>())}。");
            SetEntities(previews, "BIG_MUSHROOM", CreateRelicEntities(new[] { ModelDb.Relic<BigMushroom>() }));
        }

        if (TryGetPreviewByTextKey(previews, "FRAGRANT_MUSHROOM", out _))
        {
            SetPreview(previews, "FRAGRANT_MUSHROOM", PreviewCoverage.Complete,
                "会失去 15 点生命。",
                $"然后获得 {RelicTitle(ModelDb.Relic<FragrantMushroom>())}。");
            SetEntities(previews, "FRAGRANT_MUSHROOM", CreateRelicEntities(new[] { ModelDb.Relic<FragrantMushroom>() }));
        }
    }

    private static void ApplyJungleMazeAdventurePreview(JungleMazeAdventure jungleMazeAdventure, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "SOLO_QUEST", out _))
        {
            SetPreview(previews, "SOLO_QUEST", PreviewCoverage.Complete,
                $"失去 {jungleMazeAdventure.DynamicVars["SoloHp"].IntValue} 点生命。",
                $"获得 {jungleMazeAdventure.DynamicVars["SoloGold"].IntValue} 金币。");
        }

        if (TryGetPreviewByTextKey(previews, "JOIN_FORCES", out _))
        {
            SetPreview(previews, "JOIN_FORCES", PreviewCoverage.Complete,
                $"获得 {jungleMazeAdventure.DynamicVars["JoinForcesGold"].IntValue} 金币。");
        }
    }

    private static void ApplyPotionCourierPreview(PotionCourier potionCourier, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "GRAB_POTIONS", out _))
        {
            SetPreview(previews, "GRAB_POTIONS", PreviewCoverage.Complete,
                $"会出现 {potionCourier.DynamicVars["FoulPotions"].IntValue} 瓶 {PotionTitle(ModelDb.Potion<FoulPotion>())}。");
        }

        if (TryGetPreviewByTextKey(previews, "RANSACK", out _))
        {
            var potion = PeekPotionByRarity(potionCourier.Owner!, PotionRarity.Uncommon);
            SetPreview(previews, "RANSACK", PreviewCoverage.Complete,
                potion is null ? "会获得 1 瓶非凡药水。" : $"会获得 {PotionTitle(potion)}。");
        }
    }

    private static void ApplyRoundTeaPartyPreview(RoundTeaParty roundTeaParty, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "ENJOY_TEA", out _))
        {
            SetPreview(previews, "ENJOY_TEA", PreviewCoverage.Complete,
                $"会获得 {RelicTitle(ModelDb.Relic<RoyalPoison>())}。",
                "并回满生命。");
            SetEntities(previews, "ENJOY_TEA", CreateRelicEntities(new[] { ModelDb.Relic<RoyalPoison>() }));
        }

        if (TryGetPreviewByTextKey(previews, "PICK_FIGHT", out _))
        {
            SetPreview(previews, "PICK_FIGHT", PreviewCoverage.Complete,
                "会先进入下一页。",
                "下一页只能继续打架。");
        }

        if (TryGetPreviewByTextKey(previews, "CONTINUE_FIGHT", out _))
        {
            var relic = PeekNextRelics(roundTeaParty.Owner!, 1).FirstOrDefault();
            if (relic is not null)
            {
                SetEntities(previews, "CONTINUE_FIGHT", CreateRelicEntities(new[] { relic }));
            }
            SetPreview(previews, "CONTINUE_FIGHT", PreviewCoverage.Complete,
                $"失去 {roundTeaParty.DynamicVars.Damage.IntValue} 点生命。",
                relic is null ? "然后获得下一件遗物。" : $"然后获得 {RelicTitle(relic)}。");
        }
    }

    private static void ApplySpiralingWhirlpoolPreview(SpiralingWhirlpool spiralingWhirlpool, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "OBSERVE", out _))
        {
            SetPreview(previews, "OBSERVE", PreviewCoverage.PartialNeedsInput,
                "还需要先选择 1 张牌。",
                $"选定后会附加 {RandomVisionGameText.ResolveModelTitle(ModelDb.Enchantment<Spiral>())}。");
        }

        if (TryGetPreviewByTextKey(previews, "DRINK", out _))
        {
            SetPreview(previews, "DRINK", PreviewCoverage.Complete,
                $"回复 {spiralingWhirlpool.DynamicVars.Heal.IntValue} 点生命。");
        }
    }

    private static void ApplySunkenStatuePreview(SunkenStatue sunkenStatue, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "GRAB_SWORD", out _))
        {
            SetPreview(previews, "GRAB_SWORD", PreviewCoverage.Complete,
                $"会获得 {RelicTitle(ModelDb.Relic<SwordOfStone>())}。");
            SetEntities(previews, "GRAB_SWORD", CreateRelicEntities(new[] { ModelDb.Relic<SwordOfStone>() }));
        }

        if (TryGetPreviewByTextKey(previews, "DIVE_INTO_WATER", out _))
        {
            SetPreview(previews, "DIVE_INTO_WATER", PreviewCoverage.Complete,
                $"获得 {sunkenStatue.DynamicVars.Gold.IntValue} 金币。",
                $"再失去 {sunkenStatue.DynamicVars["HpLoss"].IntValue} 点生命。");
        }
    }

    private static void ApplyTeaMasterPreview(TeaMaster teaMaster, IList<EventOptionPreview> previews)
    {
        SetTeaPreview(previews, "BONE_TEA", PreviewCoverage.Complete,
            $"花费 {teaMaster.DynamicVars["BoneTeaCost"].IntValue} 金币。",
            $"获得 {RelicTitle(ModelDb.Relic<BoneTea>())}。");
        SetEntities(previews, "BONE_TEA", CreateRelicEntities(new[] { ModelDb.Relic<BoneTea>() }));
        SetTeaPreview(previews, "EMBER_TEA", PreviewCoverage.Complete,
            $"花费 {teaMaster.DynamicVars["EmberTeaCost"].IntValue} 金币。",
            $"获得 {RelicTitle(ModelDb.Relic<EmberTea>())}。");
        SetEntities(previews, "EMBER_TEA", CreateRelicEntities(new[] { ModelDb.Relic<EmberTea>() }));
        SetTeaPreview(previews, "TEA_OF_DISCOURTESY", PreviewCoverage.Complete,
            $"获得 {RelicTitle(ModelDb.Relic<TeaOfDiscourtesy>())}。");
        SetEntities(previews, "TEA_OF_DISCOURTESY", CreateRelicEntities(new[] { ModelDb.Relic<TeaOfDiscourtesy>() }));
    }

    private static void SetTeaPreview(IList<EventOptionPreview> previews, string key, PreviewCoverage coverage, params string[] lines)
    {
        if (TryGetPreviewByTextKey(previews, key, out var preview) && !preview.SourceOption.IsLocked)
        {
            SetPreview(previews, key, coverage, lines);
        }
    }

    private static void ApplyTheLegendsWereTruePreview(TheLegendsWereTrue theLegendsWereTrue, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "NAB_THE_MAP", out _))
        {
            SetPreview(previews, "NAB_THE_MAP", PreviewCoverage.Complete,
                $"会加入 {CardTitle(ModelDb.Card<SpoilsMap>())}。");
            SetEntities(previews, "NAB_THE_MAP", CreateCardEntities(new[] { ModelDb.Card<SpoilsMap>() }));
        }

        if (TryGetPreviewByTextKey(previews, "SLOWLY_FIND_AN_EXIT", out _))
        {
            var potion = PeekSharedRewardPotion(theLegendsWereTrue.Owner!);
            SetPreview(previews, "SLOWLY_FIND_AN_EXIT", PreviewCoverage.Complete,
                $"失去 {theLegendsWereTrue.DynamicVars.Damage.IntValue} 点生命。",
                potion is null ? "然后获得 1 瓶药水。" : $"然后获得 {PotionTitle(potion)}。");
            if (potion is not null)
            {
                SetEntities(previews, "SLOWLY_FIND_AN_EXIT", CreatePotionEntities(new[] { potion }));
            }
        }
    }

    private static void ApplyTinkerTimePreview(TinkerTime tinkerTime, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "INITIAL.options.CHOOSE_CARD_TYPE", out _))
        {
            var nextTypes = new[] { CardType.Attack, CardType.Skill, CardType.Power }
                .ToList()
                .TakeRandom(2, CloneRng(tinkerTime.Rng))
                .Select(type => type.ToLocString().GetFormattedText())
                .ToList();
            SetPreview(previews, "INITIAL.options.CHOOSE_CARD_TYPE", PreviewCoverage.Complete,
                $"下一页会出现 2 个类型选项：{JoinTitles(nextTypes)}。");
        }
    }

    private static void ApplyUnrestSitePreview(UnrestSite unrestSite, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "REST", out _))
        {
            SetPreview(previews, "REST", PreviewCoverage.Complete,
                $"回复 {unrestSite.DynamicVars.Heal.IntValue} 点生命。",
                $"再加入 {CardTitle(ModelDb.Card<PoorSleep>())}。");
        }

        if (TryGetPreviewByTextKey(previews, "KILL", out _))
        {
            var relic = PeekNextRelics(unrestSite.Owner!, 1).FirstOrDefault();
            SetPreview(previews, "KILL", PreviewCoverage.Complete,
                $"失去 {unrestSite.DynamicVars["MaxHpLoss"].IntValue} 点最大生命。",
                relic is null ? "然后获得下一件遗物。" : $"然后获得 {RelicTitle(relic)}。");
        }
    }

    private static void ApplyWarHistorianRepyPreview(WarHistorianRepy warHistorianRepy, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "UNLOCK_CAGE", out _))
        {
            SetPreview(previews, "UNLOCK_CAGE", PreviewCoverage.Complete,
                $"移除所有 {CardTitle(ModelDb.Card<LanternKey>())}。",
                $"然后获得 {RelicTitle(ModelDb.Relic<HistoryCourse>())}。");
            SetEntities(previews, "UNLOCK_CAGE",
                CreateCardEntities(new[] { ModelDb.Card<LanternKey>() })
                    .Concat(CreateRelicEntities(new[] { ModelDb.Relic<HistoryCourse>() }))
                    .ToList());
        }

        if (TryGetPreviewByTextKey(previews, "UNLOCK_CHEST", out _))
        {
            var relics = PeekNextRelics(warHistorianRepy.Owner!, 2).ToList();
            SetPreview(previews, "UNLOCK_CHEST", PreviewCoverage.PartialNeedsInput,
                $"移除所有 {CardTitle(ModelDb.Card<LanternKey>())}。",
                relics.Count == 0 ? "然后会给 2 瓶药水和 2 件遗物。" : $"然后会给 2 瓶药水和 {JoinRelics(relics)}。");
            var entities = CreateCardEntities(new[] { ModelDb.Card<LanternKey>() }).ToList();
            AddEntities(entities, CreateRelicEntities(relics));
            SetEntities(previews, "UNLOCK_CHEST", entities);
        }
    }

    private static void ApplyWaterloggedScriptoriumPreview(WaterloggedScriptorium waterloggedScriptorium, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "BLOODY_INK", out _))
        {
            SetPreview(previews, "BLOODY_INK", PreviewCoverage.Complete,
                $"增加 {waterloggedScriptorium.DynamicVars.MaxHp.IntValue} 点最大生命。");
        }

        if (TryGetPreviewByTextKey(previews, "TENTACLE_QUILL", out var tentaclePreview) && !tentaclePreview.SourceOption.IsLocked)
        {
            SetPreview(previews, "TENTACLE_QUILL", PreviewCoverage.PartialNeedsInput,
                $"花费 {waterloggedScriptorium.DynamicVars.Gold.IntValue} 金币。",
                "还需要选择 1 张牌，附加 Steady。");
        }

        if (TryGetPreviewByTextKey(previews, "PRICKLY_SPONGE", out var pricklyPreview) && !pricklyPreview.SourceOption.IsLocked)
        {
            SetPreview(previews, "PRICKLY_SPONGE", PreviewCoverage.PartialNeedsInput,
                $"花费 {waterloggedScriptorium.DynamicVars["PricklySpongeGold"].IntValue} 金币。",
                $"还需要选择 {waterloggedScriptorium.DynamicVars.Cards.IntValue} 张牌，附加 Steady。");
        }
    }

    private static void ApplyWoodCarvingsPreview(WoodCarvings woodCarvings, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "BIRD", out _))
        {
            SetPreview(previews, "BIRD", PreviewCoverage.PartialNeedsInput,
                "还需要选择 1 张可转化的基础牌。",
                $"选定后会直接变成 {CardTitle(ModelDb.Card<Peck>())}。");
        }

        if (TryGetPreviewByTextKey(previews, "SNAKE", out var snakePreview) && !snakePreview.SourceOption.IsLocked)
        {
            SetPreview(previews, "SNAKE", PreviewCoverage.PartialNeedsInput,
                "还需要选择 1 张牌。",
                $"选定后会附加 {RandomVisionGameText.ResolveModelTitle(ModelDb.Enchantment<Slither>())}。");
        }

        if (TryGetPreviewByTextKey(previews, "TORUS", out _))
        {
            SetPreview(previews, "TORUS", PreviewCoverage.PartialNeedsInput,
                "还需要选择 1 张可转化的基础牌。",
                $"选定后会直接变成 {CardTitle(ModelDb.Card<ToricToughness>())}。");
        }
    }

    private static IReadOnlyList<string> BuildReflectionsLines(Reflections reflections)
    {
        var lines = new List<string>();
        var rng = CloneRng(reflections.Rng);
        var deck = reflections.Owner!.Deck.Cards.ToList();
        var upgradedCards = deck.Where(card => card.IsUpgraded).ToList();
        var downgraded = new List<CardModel>();

        for (var index = 0; index < 2; index++)
        {
            if (upgradedCards.Count == 0)
            {
                break;
            }

            var picked = rng.NextItem(upgradedCards);
            if (picked is null)
            {
                break;
            }

            upgradedCards.Remove(picked);
            downgraded.Add(picked);
        }

        var upgradableCards = deck.Where(card => card.IsUpgradable).ToList();
        foreach (var card in downgraded)
        {
            if (!upgradableCards.Contains(card))
            {
                upgradableCards.Add(card);
            }
        }

        var upgraded = new List<CardModel>();
        for (var index = 0; index < 4; index++)
        {
            if (upgradableCards.Count == 0)
            {
                break;
            }

            var picked = rng.NextItem(upgradableCards);
            if (picked is null)
            {
                break;
            }

            upgradableCards.Remove(picked);
            upgraded.Add(picked);
        }

        if (downgraded.Count > 0)
        {
            lines.Add($"先降级 {JoinCards(downgraded)}。");
        }

        if (upgraded.Count > 0)
        {
            lines.Add($"再升级 {JoinCards(upgraded)}。");
        }

        if (lines.Count == 0)
        {
            lines.Add("当前没有可变化的牌。");
        }

        return lines;
    }

    private static CardModel? PredictNextSlipperyBridgeCard(SlipperyBridge bridge, CardModel? currentCard)
    {
        var owner = bridge.Owner!;
        var candidates = currentCard is not null
            ? owner.Deck.Cards.Where(card => card.GetType() != currentCard.GetType()).ToList()
            : owner.Deck.Cards.Where(card => card.Rarity != CardRarity.Basic).ToList();

        candidates.RemoveAll(card => !card.IsRemovable);
        if (candidates.Count == 0)
        {
            candidates = owner.Deck.Cards.Where(card => card.IsRemovable).ToList();
        }

        return CloneRng(bridge.Rng).NextItem(candidates);
    }

    private static IReadOnlyList<RelicModel> GetDollChoices()
    {
        return new RelicModel[]
        {
            ModelDb.Relic<DaughterOfTheWind>(),
            ModelDb.Relic<MrStruggles>(),
            ModelDb.Relic<BingBong>()
        };
    }

    private static IReadOnlyList<string> BuildTransformSelectionPreview(Player player, Rng rng, int selectionCount)
    {
        var candidates = PileType.Deck.GetPile(player).Cards
            .Where(card => card.Type != CardType.Quest && card.IsTransformable)
            .ToList();

        if (candidates.Count == 0)
        {
            return new[] { "当前没有可转化的牌。" };
        }

        var mappings = candidates
            .Select(card => (Source: card, Target: PeekTransformTarget(card, rng)))
            .Where(item => item.Target is not null)
            .Select(item => (item.Source, item.Target!))
            .ToList();

        if (mappings.Count == 0)
        {
            return new[] { "当前无法安全预览转化结果。" };
        }

        if (selectionCount <= 1)
        {
            return BuildSingleTransformLines(mappings);
        }

        return BuildMultiTransformLines(selectionCount, mappings);
    }

    private static IReadOnlyList<string> BuildSingleTransformLines(IReadOnlyList<(CardModel Source, CardModel Target)> mappings)
    {
        var targets = mappings
            .Select(item => CardTitle(item.Target))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets.Count == 1)
        {
            return new[]
            {
                $"无论选哪张可转化牌，都会变成 {targets[0]}。",
                $"可选牌：{JoinCards(mappings.Select(item => item.Source))}。"
            };
        }

        return mappings
            .Select(item => $"若选 {CardTitle(item.Source)} -> {CardTitle(item.Target)}。")
            .ToList();
    }

    private static IReadOnlyList<string> BuildMultiTransformLines(int selectionCount, IReadOnlyList<(CardModel Source, CardModel Target)> mappings)
    {
        var firstTargets = mappings
            .Select(item => CardTitle(item.Target))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = new List<string>
        {
            $"还需要先选择 {selectionCount} 张牌。"
        };

        if (firstTargets.Count == 1)
        {
            lines.Add($"第 1 张无论选哪张，都会先变成 {firstTargets[0]}。");
        }
        else
        {
            lines.Add("第 1 张的转化结果取决于你先选哪张牌：");
            foreach (var mapping in mappings)
            {
                lines.Add($"若先选 {CardTitle(mapping.Source)} -> {CardTitle(mapping.Target)}。");
            }
        }

        lines.Add("后续张数会受前一张选择和随机数消耗顺序影响。");
        return lines;
    }

    private static CardModel? PeekTransformTarget(CardModel original, Rng rng)
    {
        return CardFactory.CreateRandomCardForTransform(original, isInCombat: false, CloneRng(rng));
    }

    private static IReadOnlyList<CardModel> PeekRewardCards(Player player, CardCreationOptions options, int count)
    {
        return PeekRewardCards(player, new RewardPreviewState(player), options, count);
    }

    private static IReadOnlyList<CardModel> PeekRewardCards(Player player, RewardPreviewState state, CardCreationOptions options, int count)
    {
        var rewardRng = options.RngOverride is null ? state.RewardRng : CloneRng(options.RngOverride);
        var rarityOdds = options.RngOverride is null
            ? state.CardRarityOdds
            : new CardRarityOdds(player.PlayerOdds.CardRarity.CurrentValue, rewardRng);
        var blacklist = new List<CardModel>();
        var previewResults = new List<CardCreationResult>();

        for (var index = 0; index < count; index++)
        {
            options = Hook.ModifyCardRewardCreationOptions(player.RunState, player, options);

            var possibleCards = FilterPreviewRewardCardsForPlayerCount(player.RunState, options.GetPossibleCards(player))
                .Except(blacklist)
                .ToList();
            if (possibleCards.Count == 0)
            {
                break;
            }

            IEnumerable<CardModel> items;
            if (options.RarityOdds == CardRarityOddsType.Uniform)
            {
                items = possibleCards.Where(card => card.Rarity != CardRarity.Basic && card.Rarity != CardRarity.Ancient);
            }
            else
            {
                var allowedRarities = possibleCards.Select(card => card.Rarity).ToHashSet();
                var selectedRarity = RollPreviewRarity(options, allowedRarities, rarityOdds);
                if (selectedRarity == CardRarity.None)
                {
                    break;
                }

                items = possibleCards.Where(card => card.Rarity == selectedRarity);
            }

            var pickedCanonical = rewardRng.NextItem(items);
            if (pickedCanonical is null)
            {
                break;
            }

            var picked = player.RunState.CreateCard(pickedCanonical, player);
            blacklist.Add(picked.CanonicalInstance);
            if (!options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll))
            {
                ApplyPreviewUpgradeRoll(player, picked, rewardRng);
            }

            previewResults.Add(new CardCreationResult(picked));
        }

        if (!options.Flags.HasFlag(CardCreationFlags.NoModifyHooks))
        {
            Hook.TryModifyCardRewardOptions(player.RunState, player, previewResults, options, out _);
        }

        return previewResults.Select(static result => result.Card).ToList();
    }

    private static IEnumerable<CardModel> FilterPreviewRewardCardsForPlayerCount(IRunState runState, IEnumerable<CardModel> options)
    {
        return runState.Players.Count > 1
            ? options.Where(card => card.MultiplayerConstraint != CardMultiplayerConstraint.SingleplayerOnly)
            : options.Where(card => card.MultiplayerConstraint != CardMultiplayerConstraint.MultiplayerOnly);
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PeekScrollBoxesBundles(Player player)
    {
        var rewardRng = CloneRng(player.PlayerRng.Rewards);
        var isDefect = player.Character is Defect;

        var commonOptions = CardCreationOptions
            .ForNonCombatWithUniformOdds(new[] { player.Character.CardPool }, card => card.Rarity == CardRarity.Common)
            .WithFlags(CardCreationFlags.NoRarityModification);
        commonOptions = Hook.ModifyCardRewardCreationOptions(player.RunState, player, commonOptions);

        var uncommonOptions = CardCreationOptions
            .ForNonCombatWithUniformOdds(new[] { player.Character.CardPool }, card => card.Rarity == CardRarity.Uncommon)
            .WithFlags(CardCreationFlags.NoRarityModification);
        uncommonOptions = Hook.ModifyCardRewardCreationOptions(player.RunState, player, uncommonOptions);

        var commonCards = commonOptions.GetPossibleCards(player).ToList();
        var uncommonCards = uncommonOptions.GetPossibleCards(player).ToList();
        var bundles = new List<IReadOnlyList<CardModel>>();
        var usedCardIds = new HashSet<ModelId>();

        for (var bundleIndex = 0; bundleIndex < 2; bundleIndex++)
        {
            if (isDefect && rewardRng.NextInt(100) < 1)
            {
                var claw = ModelDb.Card<Claw>();
                bundles.Add(new[] { claw, claw, claw });
                continue;
            }

            var bundleCards = new List<CardModel>();
            var remainingCommons = commonCards.Where(card => !usedCardIds.Contains(card.Id)).ToList();
            for (var commonIndex = 0; commonIndex < 2 && remainingCommons.Count > 0; commonIndex++)
            {
                var pickedCommon = rewardRng.NextItem(remainingCommons);
                if (pickedCommon is null)
                {
                    break;
                }

                bundleCards.Add(pickedCommon);
                usedCardIds.Add(pickedCommon.Id);
                remainingCommons.Remove(pickedCommon);
            }

            var remainingUncommons = uncommonCards.Where(card => !usedCardIds.Contains(card.Id)).ToList();
            var pickedUncommon = rewardRng.NextItem(remainingUncommons);
            if (pickedUncommon is not null)
            {
                bundleCards.Add(pickedUncommon);
                usedCardIds.Add(pickedUncommon.Id);
            }

            if (bundleCards.Count > 0)
            {
                bundles.Add(bundleCards);
            }
        }

        return bundles;
    }

    private static CardRarity RollPreviewRarity(CardCreationOptions options, HashSet<CardRarity> allowedRarities, CardRarityOdds rarityOdds)
    {
        var shouldChangeFutureOdds = options.Flags.HasFlag(CardCreationFlags.ForceRarityOddsChange) ||
                                     (options.Source == CardCreationSource.Encounter &&
                                      options.RarityOdds is CardRarityOddsType.RegularEncounter or CardRarityOddsType.EliteEncounter or CardRarityOddsType.BossEncounter);

        var rolled = shouldChangeFutureOdds
            ? rarityOdds.Roll(options.RarityOdds)
            : rarityOdds.RollWithBaseOdds(options.RarityOdds);

        while (!allowedRarities.Contains(rolled) && rolled != CardRarity.None)
        {
            rolled = rolled.GetNextHighestRarity();
        }

        return rolled;
    }

    private static IReadOnlyList<RelicModel> PeekNextRelics(Player player, int count, RelicRarity? rarity = null)
    {
        var cloneBag = RelicGrabBag.FromSerializable(player.RelicGrabBag.ToSerializable());
        var rewardsRng = CloneRng(player.PlayerRng.Rewards);
        var relics = new List<RelicModel>();

        for (var index = 0; index < count; index++)
        {
            var nextRarity = rarity ?? RelicFactory.RollRarity(rewardsRng);
            var relic = cloneBag.PullFromFront(nextRarity, player.RunState);
            if (relic is null)
            {
                break;
            }

            relics.Add(relic);
        }

        return relics;
    }

    private static PotionModel? PeekSharedRewardPotion(Player player)
    {
        var rewardRng = CloneRng(player.PlayerRng.Rewards);
        var potions = player.Character.PotionPool.GetUnlockedPotions(player.UnlockState)
            .Concat(ModelDb.PotionPool<SharedPotionPool>().GetUnlockedPotions(player.UnlockState));

        return rewardRng.NextItem(potions);
    }

    private static PotionModel? PeekPotionByRarity(Player player, PotionRarity rarity)
    {
        var rewardRng = CloneRng(player.PlayerRng.Rewards);
        var potions = player.Character.PotionPool.GetUnlockedPotions(player.UnlockState)
            .Concat(ModelDb.PotionPool<SharedPotionPool>().GetUnlockedPotions(player.UnlockState))
            .Where(potion => potion.Rarity == rarity);
        return rewardRng.NextItem(potions);
    }

    private static bool TryGetPreviewByTextKey(IList<EventOptionPreview> previews, string textKeySnippet, out EventOptionPreview preview)
    {
        EventOptionPreview? candidate = previews.FirstOrDefault(optionPreview =>
            optionPreview.SourceOption.TextKey.Contains(textKeySnippet, StringComparison.OrdinalIgnoreCase));
        preview = candidate!;
        return candidate is not null;
    }

    private static void SetPreview(IList<EventOptionPreview> previews, string textKeySnippet, PreviewCoverage coverage, IEnumerable<string> lines)
    {
        if (!TryGetPreviewByTextKey(previews, textKeySnippet, out var preview))
        {
            return;
        }

        preview.Coverage = coverage;
        preview.Lines.Clear();
        foreach (var line in lines)
        {
            AddLine(preview.Lines, line);
        }
    }

    private static void SetPreview(IList<EventOptionPreview> previews, string textKeySnippet, PreviewCoverage coverage, params string[] lines)
    {
        SetPreview(previews, textKeySnippet, coverage, (IEnumerable<string>)lines);
    }

    private static void SetEntities(IList<EventOptionPreview> previews, string textKeySnippet, IEnumerable<EventPreviewEntity> entities)
    {
        if (!TryGetPreviewByTextKey(previews, textKeySnippet, out var preview))
        {
            return;
        }

        preview.Entities.Clear();
        AddEntities(preview.Entities, entities);
    }

    private static void AddEntities(ICollection<EventPreviewEntity> target, IEnumerable<EventPreviewEntity> entities)
    {
        foreach (var entity in entities)
        {
            if (target.Any(existing => string.Equals(existing.Key, entity.Key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            target.Add(entity);
        }
    }

    private static IReadOnlyList<EventPreviewEntity> CreateCardEntities(IEnumerable<CardModel> cards)
    {
        var entities = new List<EventPreviewEntity>();
        foreach (var card in cards)
        {
            AddEntities(entities, RandomVisionGameText.ExtractPreviewEntities(new IHoverTip[] { new CardHoverTip(card) }));
        }

        return entities;
    }

    private static IReadOnlyList<EventPreviewEntity> CreateRelicEntities(IEnumerable<RelicModel> relics)
    {
        var entities = new List<EventPreviewEntity>();
        foreach (var relic in relics)
        {
            AddEntities(entities, RandomVisionGameText.ExtractPreviewEntities(relic.HoverTips));
        }

        return entities;
    }

    private static IReadOnlyList<EventPreviewEntity> CreatePotionEntities(IEnumerable<PotionModel> potions)
    {
        var entities = new List<EventPreviewEntity>();
        foreach (var potion in potions)
        {
            AddEntities(entities, RandomVisionGameText.ExtractPreviewEntities(potion.HoverTips));
        }

        return entities;
    }

    private static void ApplyPreviewUpgradeRoll(Player player, CardModel card, Rng rewardRng)
    {
        var rolledOdds = (decimal)rewardRng.NextFloat();
        if (!card.IsUpgradable)
        {
            return;
        }

        decimal upgradeOdds = 0m;
        if (card.Rarity != CardRarity.Rare)
        {
            upgradeOdds += player.RunState.CurrentActIndex * UpgradedCardOddScaling;
        }

        upgradeOdds = Hook.ModifyCardRewardUpgradeOdds(player.RunState, player, card, upgradeOdds);
        if (rolledOdds <= upgradeOdds)
        {
            CardCmd.Upgrade(card);
        }
    }

    private static void AddLine(ICollection<string> lines, string? line)
    {
        var cleaned = RandomVisionI18n.LocalizeGeneratedText(RandomVisionGameText.Clean(line));
        if (string.IsNullOrWhiteSpace(cleaned) ||
            lines.Contains(cleaned, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        lines.Add(cleaned);
    }

    private static void FinalizePreview(EventOptionPreview preview)
    {
        if (preview.Lines.Count > 0)
        {
            return;
        }

        switch (preview.Coverage)
        {
            case PreviewCoverage.Complete:
                AddLine(preview.Lines, "当前结果已经完全可确定。");
                break;
            case PreviewCoverage.PartialNeedsInput:
                AddLine(preview.Lines, "还需要进一步选择后才能完全确定。");
                break;
            default:
                AddLine(preview.Lines, "本页结果已公开。");
                break;
        }
    }

    private static bool TryGetStringVar(EventModel eventModel, string name, out string value)
    {
        value = string.Empty;
        if (!eventModel.DynamicVars.TryGetValue(name, out var dynamicVar) || dynamicVar is not StringVar stringVar)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(stringVar.StringValue))
        {
            return false;
        }

        value = RandomVisionGameText.Clean(stringVar.StringValue);
        return true;
    }

    private static bool TryGetPotionFromOption(EventOption option, out PotionModel potion)
    {
        potion = null!;
        foreach (var hoverTip in IHoverTip.RemoveDupes(option.HoverTips))
        {
            if (hoverTip.CanonicalModel is PotionModel model)
            {
                potion = model;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetFuturePotionCardType(TheFutureOfPotions eventModel, PotionModel potion, EventOption option, out CardType cardType)
    {
        cardType = default;
        var cardTypes = FutureOfPotionsCardTypesRef(eventModel);
        if (cardTypes is not null && cardTypes.TryGetValue(potion, out cardType))
        {
            return true;
        }

        return TryInferCardTypeFromOption(option, out cardType);
    }

    private static CardRarity GetFuturePotionCardRarity(PotionModel potion)
    {
        return potion.Rarity switch
        {
            PotionRarity.Rare or PotionRarity.Event => CardRarity.Rare,
            PotionRarity.Uncommon => CardRarity.Uncommon,
            _ => CardRarity.Common
        };
    }

    private static bool TryInferCardTypeFromOption(EventOption option, out CardType cardType)
    {
        cardType = default;
        var text = $"{RandomVisionGameText.ResolveLocString(option.Title)} {RandomVisionGameText.ResolveLocString(option.Description)}";
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (ContainsTypeToken(text, CardType.Attack))
        {
            cardType = CardType.Attack;
            return true;
        }

        if (ContainsTypeToken(text, CardType.Skill))
        {
            cardType = CardType.Skill;
            return true;
        }

        if (ContainsTypeToken(text, CardType.Power))
        {
            cardType = CardType.Power;
            return true;
        }

        return false;
    }

    private static bool ContainsTypeToken(string text, CardType cardType)
    {
        var token = RandomVisionGameText.Clean(cardType.ToLocString().GetFormattedText());
        if (!string.IsNullOrWhiteSpace(token) &&
            text.Contains(token, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return cardType switch
        {
            CardType.Attack => text.Contains("attack", StringComparison.OrdinalIgnoreCase),
            CardType.Skill => text.Contains("skill", StringComparison.OrdinalIgnoreCase),
            CardType.Power => text.Contains("power", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static Rng CloneRng(Rng rng)
    {
        return new Rng(rng.Seed, rng.Counter);
    }

    private static string JoinCards(IEnumerable<CardModel> cards)
    {
        return JoinTitles(cards.Select(CardTitle));
    }

    private static string JoinRelics(IEnumerable<RelicModel> relics)
    {
        return JoinTitles(relics.Select(RelicTitle));
    }

    private static string JoinTitles(IEnumerable<string> titles)
    {
        return string.Join(" / ", titles.Where(title => !string.IsNullOrWhiteSpace(title)));
    }

    private static string CardTitle(CardModel card)
    {
        return RandomVisionGameText.ResolveCardTitle(card);
    }

    private static string RelicTitle(RelicModel relic)
    {
        return RandomVisionGameText.ResolveRelicTitle(relic);
    }

    private static string PotionTitle(PotionModel potion)
    {
        return RandomVisionGameText.ResolvePotionTitle(potion);
    }
}
