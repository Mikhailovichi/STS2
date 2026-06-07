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
        public RewardPreviewState(Player player, Rng? rewardRng = null)
        {
            RewardRng = rewardRng is null ? CloneRng(player.PlayerRng.Rewards) : CloneRng(rewardRng);
            CardRarityOdds = new CardRarityOdds(player.PlayerOdds.CardRarity.CurrentValue, RewardRng);
        }

        public Rng RewardRng { get; }

        public CardRarityOdds CardRarityOdds { get; }
    }

    private static readonly AccessTools.FieldRef<SlipperyBridge, CardModel> SlipperyBridgeCardRef =
        AccessTools.FieldRefAccess<SlipperyBridge, CardModel>("_randomCardToLose");

    private static readonly AccessTools.FieldRef<SlipperyBridge, int> SlipperyBridgeHoldOnsRef =
        AccessTools.FieldRefAccess<SlipperyBridge, int>("_numberOfHoldOns");

    private static readonly AccessTools.FieldRef<SlipperyBridge, HashSet<CardModel>?> SlipperyBridgeSkippedRemovalsRef =
        AccessTools.FieldRefAccess<SlipperyBridge, HashSet<CardModel>?>("_skippedRemovals");

    private static readonly AccessTools.FieldRef<StoneOfAllTime, PotionModel> StonePotionRef =
        AccessTools.FieldRefAccess<StoneOfAllTime, PotionModel>("_drinkAndLiftPotion");

    private static readonly AccessTools.FieldRef<WelcomeToWongos, RelicModel> WongosFeaturedItemRef =
        AccessTools.FieldRefAccess<WelcomeToWongos, RelicModel>("_featuredItem");

    private static readonly AccessTools.FieldRef<TabletOfTruth, int> TabletOfTruthDecipherCountRef =
        AccessTools.FieldRefAccess<TabletOfTruth, int>("_decipherCount");

    private static readonly AccessTools.FieldRef<TheFutureOfPotions, Dictionary<PotionModel, CardType>?> FutureOfPotionsCardTypesRef =
        AccessTools.FieldRefAccess<TheFutureOfPotions, Dictionary<PotionModel, CardType>?>("_cardTypes");

    public static EventPreviewResult BuildEventPreview(EventModel eventModel)
    {
        LogPreviewStep(eventModel, $"start options={eventModel.CurrentOptions.Count()} adapter={eventModel.GetType().Name}");

        var optionPreviews = BuildBaselineOptionPreviews(eventModel);
        LogPreviewStep(eventModel, $"baseline-built options={optionPreviews.Count}");
        LogOptionPreviews(eventModel, "baseline", optionPreviews);

        try
        {
            LogPreviewStep(eventModel, $"enrich-start adapter={eventModel.GetType().Name}");
            ApplyEventSpecificPreview(eventModel, optionPreviews);
            LogPreviewStep(eventModel, $"enrich-done adapter={eventModel.GetType().Name}");
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
        LogOptionPreviews(eventModel, "final", optionPreviews);
        LogPreviewStep(eventModel, $"done title=\"{CleanLogValue(eventTitle)}\" options={optionPreviews.Count}");
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
            case Darv darv:
                ApplyDarvPreview(darv, previews);
                break;
            case Vakuu vakuu:
                ApplyVakuuPreview(vakuu, previews);
                break;
            case Orobas orobas:
                ApplyOrobasPreview(orobas, previews);
                break;
            case Tezcatara tezcatara:
                ApplyTezcataraPreview(tezcatara, previews);
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
            case TabletOfTruth tabletOfTruth:
                ApplyTabletOfTruthPreview(tabletOfTruth, previews);
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
                case HeftyTablet:
                {
                    if (!TryGetIntVar(neow, "Cards", out var optionCount) || optionCount <= 0)
                    {
                        optionCount = 3;
                    }

                    var options = new CardCreationOptions(
                            new[] { neow.Owner.Character.CardPool },
                            CardCreationSource.Other,
                            CardRarityOddsType.Uniform,
                            card => card.Rarity == CardRarity.Rare)
                        .WithFlags(CardCreationFlags.NoUpgradeRoll);
                    var cards = PeekRewardCards(neow.Owner, options, optionCount).ToList();
                    optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;

                    if (cards.Count == 0)
                    {
                        AddLine(optionPreview.Lines, $"沉重碑板会出现 {optionCount} 张稀有牌供你选择，并加入 1 张 {CardTitle(ModelDb.Card<Injury>())}。");
                    }
                    else
                    {
                        AddLine(optionPreview.Lines,
                            $"沉重碑板会出现 {JoinCards(cards)} 供你 {cards.Count} 选 1，并加入 1 张 {CardTitle(ModelDb.Card<Injury>())}。");
                        AddEntities(optionPreview.Entities, CreateCardEntities(cards));
                    }

                    handledAny = true;
                    break;
                }
                case Kaleidoscope:
                {
                    if (!TryGetIntVar(neow, "Cards", out var rewardCount) || rewardCount <= 0)
                    {
                        rewardCount = 2;
                    }

                    var rewards = PeekKaleidoscopeRewards(neow.Owner, rewardCount);
                    optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;

                    if (rewards.Count == 0)
                    {
                        AddLine(optionPreview.Lines, $"万花筒会出现 {rewardCount} 组其他角色牌奖励。");
                    }
                    else
                    {
                        AddLine(optionPreview.Lines, $"万花筒会出现 {rewards.Count} 组其他角色牌奖励。");
                        for (var rewardIndex = 0; rewardIndex < rewards.Count; rewardIndex++)
                        {
                            AddLine(optionPreview.Lines, $"Reward {rewardIndex + 1}：{JoinCards(rewards[rewardIndex])}。");
                        }

                        AddEntities(optionPreview.Entities, CreateCardEntities(rewards.SelectMany(reward => reward)));
                    }

                    handledAny = true;
                    break;
                }
                case NeowsBones neowsBones:
                {
                    var relicCount = GetIntVarOrDefault(neowsBones, "Relics", 2);
                    var curseCount = GetIntVarOrDefault(neowsBones, "Curses", 1);
                    var bonesRelics = PeekNeowsBonesRelics(neow.Owner, neowsBones, relicCount).ToList();
                    var curses = PeekNeowsBonesCurses(neow.Owner, curseCount).ToList();
                    optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;

                    if (bonesRelics.Count == 0)
                    {
                        AddLine(optionPreview.Lines, $"尼奥的骨头会提供 {relicCount} 件其他尼奥遗物。");
                    }
                    else
                    {
                        AddLine(optionPreview.Lines, $"尼奥的骨头会提供 {JoinRelics(bonesRelics)}。");
                        AddEntities(optionPreview.Entities, CreateRelicEntities(bonesRelics));
                        foreach (var bonesRelic in bonesRelics)
                        {
                            AddNeowsBonesRelicAdapterPreview(neow, optionPreview, bonesRelic);
                        }
                    }

                    if (curses.Count == 0)
                    {
                        AddLine(optionPreview.Lines, $"之后会加入 {curseCount} 张随机诅咒。");
                    }
                    else
                    {
                        AddLine(optionPreview.Lines, $"之后会加入 {JoinCards(curses)}。");
                        AddEntities(optionPreview.Entities, CreateCardEntities(curses));
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
                case PhialHolster phialHolster:
                {
                    var potionSlots = GetIntVarOrDefault(phialHolster, "PotionSlots", 1);
                    var potionCount = GetIntVarOrDefault(phialHolster, "Potions", 2);
                    var potions = PeekPhialHolsterPotions(neow.Owner, potionCount);
                    optionPreview.Coverage = PreviewCoverage.Complete;

                    AddLine(optionPreview.Lines, $"药瓶套会增加 {potionSlots} 个药水栏位。");
                    if (potions.Count == 0)
                    {
                        AddLine(optionPreview.Lines, $"然后获得 {potionCount} 瓶随机药水。");
                    }
                    else
                    {
                        AddLine(optionPreview.Lines, $"然后获得 {JoinPotions(potions)}。");
                        AddEntities(optionPreview.Entities, CreatePotionEntities(potions));
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
                    var mappings = BuildTransformSelectionMappings(neow.Owner, neow.Owner.RunState.Rng.Niche);
                    optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;
                    foreach (var line in BuildTransformSelectionPreview(mappings, 1))
                    {
                        AddLine(optionPreview.Lines, line);
                    }
                    AddEntities(optionPreview.Entities, CreateCardEntities(mappings.Select(item => item.Target)));

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

    private static void ApplyDarvPreview(Darv darv, IList<EventOptionPreview> previews)
    {
        if (darv.Owner is null)
        {
            return;
        }

        foreach (var preview in previews.Where(preview => !preview.SourceOption.IsLocked))
        {
            switch (preview.SourceOption.Relic)
            {
                case Astrolabe astrolabe:
                {
                    var selectionCount = astrolabe.DynamicVars.Cards.IntValue;
                    var outcomes = PeekAstrolabeTransformOutcomes(darv.Owner, selectionCount);
                    preview.Coverage = PreviewCoverage.PartialNeedsInput;
                    if (outcomes.Count == 0)
                    {
                        AddLine(preview.Lines, "当前没有可转化的牌。");
                    }
                    else
                    {
                        AddLine(preview.Lines, $"还需要选择 {selectionCount} 张牌；结果会随选择顺序变化。");
                        foreach (var outcome in outcomes)
                        {
                            AddLine(preview.Lines, $"{CardTitle(outcome.Source)} -> {JoinCards(outcome.Targets)}。");
                        }

                        AddEntities(preview.Entities, CreateCardEntities(outcomes.SelectMany(outcome => outcome.Targets)));
                    }

                    break;
                }
                case PandorasBox:
                {
                    var transformations = PeekPandorasBoxTransformations(darv.Owner);
                    preview.Coverage = PreviewCoverage.Complete;
                    if (transformations.Count == 0)
                    {
                        AddLine(preview.Lines, "当前没有可转化的打击或防御。");
                    }
                    else
                    {
                        foreach (var transformation in transformations)
                        {
                            AddLine(preview.Lines, $"{CardTitle(transformation.Source)} -> {CardTitle(transformation.Target)}。");
                        }

                        AddEntities(preview.Entities, CreateCardEntities(transformations.Select(item => item.Target)));
                    }

                    break;
                }
                case CallingBell callingBell:
                {
                    var curse = ModelDb.Card<CurseOfTheBell>();
                    var relicCount = GetIntVarOrDefault(callingBell, "Relics", 3);
                    var relics = PeekCallingBellRelics(darv.Owner, relicCount);
                    preview.Coverage = PreviewCoverage.PartialNeedsInput;

                    AddLine(preview.Lines, $"会加入 {CardTitle(curse)}。");
                    if (relics.Count == 0)
                    {
                        AddLine(preview.Lines, $"然后出现 {relicCount} 件遗物奖励。");
                    }
                    else
                    {
                        AddLine(preview.Lines, $"然后出现 {JoinRelics(relics)}。");
                        AddEntities(preview.Entities, CreateRelicEntities(relics));
                    }

                    AddEntities(preview.Entities, CreateCardEntities(new[] { curse }));
                    break;
                }
            }
        }
    }

    private static void ApplyVakuuPreview(Vakuu vakuu, IList<EventOptionPreview> previews)
    {
        if (vakuu.Owner is null)
        {
            return;
        }

        foreach (var preview in previews.Where(preview => !preview.SourceOption.IsLocked))
        {
            if (preview.SourceOption.Relic is not SereTalon sereTalon)
            {
                continue;
            }

            var curseCount = GetIntVarOrDefault(sereTalon, "Curses", 2);
            var curses = PeekRandomGeneratedCurses(vakuu.Owner, curseCount);
            preview.Coverage = PreviewCoverage.Complete;

            if (curses.Count == 0)
            {
                AddLine(preview.Lines, $"会加入 {curseCount} 张随机诅咒。");
            }
            else
            {
                AddLine(preview.Lines, $"会加入 {JoinCards(curses)}。");
                AddEntities(preview.Entities, CreateCardEntities(curses));
            }
        }
    }

    private static void ApplyOrobasPreview(Orobas orobas, IList<EventOptionPreview> previews)
    {
        if (orobas.Owner is null)
        {
            return;
        }

        if (TryGetPreviewByTextKey(previews, "GLASS_EYE", out var glassEyePreview))
        {
            var rewards = PeekGlassEyeRewards(orobas.Owner);
            var lines = new List<string>
            {
                "玻璃眼会出现 5 组卡牌奖励。"
            };

            for (var rewardIndex = 0; rewardIndex < rewards.Count; rewardIndex++)
            {
                var rarityName = rewardIndex switch
                {
                    0 or 1 => "普通",
                    2 or 3 => "非凡",
                    _ => "稀有"
                };
                lines.Add($"第 {rewardIndex + 1} 组（{rarityName}）：{JoinCards(rewards[rewardIndex])}。");
            }

            SetPreview(previews, "GLASS_EYE", PreviewCoverage.PartialNeedsInput, lines);
            AddEntities(glassEyePreview.Entities, CreateCardEntities(rewards.SelectMany(reward => reward)));
        }

        if (TryGetPreviewByTextKey(previews, "ALCHEMICAL_COFFER", out var alchemicalCofferPreview))
        {
            var potionCount = ModelDb.Relic<AlchemicalCoffer>().DynamicVars["PotionSlots"].IntValue;
            var potions = PeekAlchemicalCofferPotions(orobas.Owner, potionCount);
            SetPreview(previews, "ALCHEMICAL_COFFER", PreviewCoverage.Complete,
                potions.Count == 0
                    ? $"会获得 {potionCount} 个药水栏，并填入随机药水。"
                    : $"会获得 {potionCount} 个药水栏，并填入 {JoinPotions(potions)}。");
            AddEntities(alchemicalCofferPreview.Entities, CreatePotionEntities(potions));
        }

        foreach (var optionPreview in previews.Where(preview => !preview.SourceOption.IsLocked))
        {
            switch (optionPreview.SourceOption.Relic)
            {
                case SeaGlass seaGlass:
                {
                    var rewards = PeekSeaGlassCards(orobas.Owner, seaGlass);
                    optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;
                    optionPreview.Lines.Clear();
                    AddLine(optionPreview.Lines,
                        $"海玻璃会出现 {rewards.All.Count} 张 {RandomVisionGameText.ResolveLocString(rewards.Character.Title)} 牌，可任意选择加入。");
                    if (rewards.Common.Count > 0)
                    {
                        AddLine(optionPreview.Lines, $"普通：{JoinCards(rewards.Common)}。");
                    }
                    if (rewards.Uncommon.Count > 0)
                    {
                        AddLine(optionPreview.Lines, $"非凡：{JoinCards(rewards.Uncommon)}。");
                    }
                    if (rewards.Rare.Count > 0)
                    {
                        AddLine(optionPreview.Lines, $"稀有：{JoinCards(rewards.Rare)}。");
                    }
                    AddEntities(optionPreview.Entities, CreateCardEntities(rewards.All));
                    break;
                }
                case SandCastle sandCastle:
                {
                    var upgraded = orobas.Owner.Deck.Cards
                        .Where(card => card?.IsUpgradable ?? false)
                        .ToList()
                        .StableShuffle(CloneRng(orobas.Owner.RunState.Rng.Niche))
                        .Take(sandCastle.DynamicVars.Cards.IntValue)
                        .ToList();
                    optionPreview.Coverage = PreviewCoverage.Complete;
                    optionPreview.Lines.Clear();
                    AddLine(optionPreview.Lines,
                        upgraded.Count == 0 ? "沙堡不会升级任何牌。" : $"沙堡会升级 {JoinCards(upgraded)}。");
                    AddEntities(optionPreview.Entities, CreateCardEntities(upgraded));
                    break;
                }
            }
        }
    }

    private static void ApplyTezcataraPreview(Tezcatara tezcatara, IList<EventOptionPreview> previews)
    {
        if (tezcatara.Owner is null)
        {
            return;
        }

        if (TryGetPreviewByTextKey(previews, "TOY_BOX", out var toyBoxPreview))
        {
            var relicCount = ModelDb.Relic<ToyBox>().DynamicVars["Relics"].IntValue;
            var relics = PeekToyBoxRelics(tezcatara.Owner, relicCount);
            SetPreview(previews, "TOY_BOX", PreviewCoverage.Complete,
                relics.Count == 0
                    ? $"玩具箱会提供 {relicCount} 件蜡制遗物。"
                    : $"玩具箱会提供 {JoinRelics(relics)}。");
            AddEntities(toyBoxPreview.Entities, CreateRelicEntities(relics));
        }
    }

    private static void AddNeowsBonesRelicAdapterPreview(Neow neow, EventOptionPreview parentPreview, RelicModel relic)
    {
        var nestedPreview = new EventOptionPreview(parentPreview.SourceOption, RelicTitle(relic), PreviewCoverage.AlreadyVisible);
        if (!TryApplyNestedNeowRelicPreview(neow, nestedPreview, relic))
        {
            return;
        }

        foreach (var line in nestedPreview.Lines)
        {
            AddLine(parentPreview.Lines, $"{RelicTitle(relic)}：{line}");
        }

        AddEntities(parentPreview.Entities, nestedPreview.Entities);
    }

    private static bool TryApplyNestedNeowRelicPreview(Neow neow, EventOptionPreview optionPreview, RelicModel relic)
    {
        switch (relic)
        {
            case LargeCapsule:
            {
                var capsuleRelics = PeekNextRelics(neow.Owner!, 2).ToList();
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

                return true;
            }
            case ArcaneScroll:
            {
                var options = new CardCreationOptions(
                        new[] { neow.Owner!.Character.CardPool },
                        CardCreationSource.Other,
                        CardRarityOddsType.Uniform,
                        card => card.Rarity == CardRarity.Rare)
                    .WithFlags(CardCreationFlags.NoUpgradeRoll);
                var cards = PeekRewardCards(neow.Owner!, options, 1).ToList();
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

                return true;
            }
            case LeadPaperweight:
            {
                var options = new CardCreationOptions(
                    new[] { ModelDb.CardPool<ColorlessCardPool>() },
                    CardCreationSource.Other,
                    CardRarityOddsType.RegularEncounter);
                var cards = PeekRewardCards(neow.Owner!, options, 2).ToList();
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

                return true;
            }
            case HeftyTablet:
            {
                var optionCount = GetIntVarOrDefault(relic, "Cards", 3);
                var options = new CardCreationOptions(
                        new[] { neow.Owner!.Character.CardPool },
                        CardCreationSource.Other,
                        CardRarityOddsType.Uniform,
                        card => card.Rarity == CardRarity.Rare)
                    .WithFlags(CardCreationFlags.NoUpgradeRoll);
                var cards = PeekRewardCards(neow.Owner!, options, optionCount).ToList();
                optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;

                if (cards.Count == 0)
                {
                    AddLine(optionPreview.Lines, $"沉重碑板会出现 {optionCount} 张稀有牌供你选择，并加入 1 张 {CardTitle(ModelDb.Card<Injury>())}。");
                }
                else
                {
                    AddLine(optionPreview.Lines,
                        $"沉重碑板会出现 {JoinCards(cards)} 供你 {cards.Count} 选 1，并加入 1 张 {CardTitle(ModelDb.Card<Injury>())}。");
                    AddEntities(optionPreview.Entities, CreateCardEntities(cards));
                }

                return true;
            }
            case Kaleidoscope:
            {
                var rewardCount = GetIntVarOrDefault(relic, "Cards", 2);
                var rewards = PeekKaleidoscopeRewards(neow.Owner!, rewardCount);
                optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;

                if (rewards.Count == 0)
                {
                    AddLine(optionPreview.Lines, $"万花筒会出现 {rewardCount} 组其他角色牌奖励。");
                }
                else
                {
                    AddLine(optionPreview.Lines, $"万花筒会出现 {rewards.Count} 组其他角色牌奖励。");
                    for (var rewardIndex = 0; rewardIndex < rewards.Count; rewardIndex++)
                    {
                        AddLine(optionPreview.Lines, $"Reward {rewardIndex + 1}：{JoinCards(rewards[rewardIndex])}。");
                    }

                    AddEntities(optionPreview.Entities, CreateCardEntities(rewards.SelectMany(reward => reward)));
                }

                return true;
            }
            case SmallCapsule:
            {
                var smallCapsuleRelic = PeekNextRelics(neow.Owner!, 1).FirstOrDefault();
                optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;

                if (smallCapsuleRelic is null)
                {
                    AddLine(optionPreview.Lines, "小扭蛋会出现 1 件遗物供你选择。");
                }
                else
                {
                    AddLine(optionPreview.Lines, $"小扭蛋会出现 {RelicTitle(smallCapsuleRelic)}。");
                    AddEntities(optionPreview.Entities, CreateRelicEntities(new[] { smallCapsuleRelic }));
                }

                return true;
            }
            case MassiveScroll:
            {
                var customCardPool = ModelDb.CardPool<ColorlessCardPool>()
                    .GetUnlockedCards(neow.Owner!.RunState.UnlockState, neow.Owner.RunState.CardMultiplayerConstraint)
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

                return true;
            }
            case LostCoffer:
            {
                var options = new CardCreationOptions(
                    new[] { neow.Owner!.Character.CardPool },
                    CardCreationSource.Other,
                    CardRarityOddsType.RegularEncounter);
                var cards = PeekRewardCards(neow.Owner!, options, 3).ToList();
                var potion = PeekSharedRewardPotion(neow.Owner!);
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

                return true;
            }
            case PhialHolster phialHolster:
            {
                var potionSlots = GetIntVarOrDefault(phialHolster, "PotionSlots", 1);
                var potionCount = GetIntVarOrDefault(phialHolster, "Potions", 2);
                var potions = PeekPhialHolsterPotions(neow.Owner!, potionCount);
                optionPreview.Coverage = PreviewCoverage.Complete;

                AddLine(optionPreview.Lines, $"药瓶套会增加 {potionSlots} 个药水栏位。");
                if (potions.Count == 0)
                {
                    AddLine(optionPreview.Lines, $"然后获得 {potionCount} 瓶随机药水。");
                }
                else
                {
                    AddLine(optionPreview.Lines, $"然后获得 {JoinPotions(potions)}。");
                    AddEntities(optionPreview.Entities, CreatePotionEntities(potions));
                }

                return true;
            }
            case LeafyPoultice:
            {
                var transformedCards = PredictLeafyPoulticeTransformCards(neow.Owner!);
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

                return true;
            }
            case NewLeaf:
            {
                var mappings = BuildTransformSelectionMappings(neow.Owner!, neow.Owner!.RunState.Rng.Niche);
                optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;
                foreach (var line in BuildTransformSelectionPreview(mappings, 1))
                {
                    AddLine(optionPreview.Lines, line);
                }

                AddEntities(optionPreview.Entities, CreateCardEntities(mappings.Select(item => item.Target)));
                return true;
            }
            case ScrollBoxes:
            {
                var bundles = PeekScrollBoxesBundles(neow.Owner!);
                optionPreview.Coverage = PreviewCoverage.PartialNeedsInput;
                AddLine(optionPreview.Lines, $"卷轴盒会先失去全部金币（当前 {neow.Owner!.Gold}）。");

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

                return true;
            }
            default:
                return false;
        }
    }

    private static void ApplyTrialPreview(Trial trial, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "DOUBLE_DOWN", out _))
        {
            ApplyTrialAcceptPreview(trial, previews, "ACCEPT");
            SetPreview(previews, "DOUBLE_DOWN", PreviewCoverage.Complete, "直接弃掉本次 run。");
            return;
        }

        if (TryGetPreviewByTextKey(previews, "INITIAL.options.ACCEPT", out _))
        {
            ApplyTrialAcceptPreview(trial, previews, "INITIAL.options.ACCEPT");
            SetPreview(previews, "INITIAL.options.REJECT", PreviewCoverage.Complete,
                "会先进入拒绝页。",
                "下一步可重新接受审判，或双倍下注直接弃局。");
            return;
        }

        if (TryGetPreviewByTextKey(previews, "MERCHANT.options.GUILTY", out var merchantGuiltyPreview))
        {
            var merchantRelics = PeekNextRelics(trial.Owner!, 2).ToList();
            SetPreview(previews, "MERCHANT.options.GUILTY", PreviewCoverage.Complete,
                "获得诅咒遗憾。",
                merchantRelics.Count == 0 ? "再获得 2 件遗物。" : $"再获得 {JoinRelics(merchantRelics)}。");
            AddEntities(merchantGuiltyPreview.Entities, CreateRelicEntities(merchantRelics));
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

        if (TryGetPreviewByTextKey(previews, "NONDESCRIPT.options.GUILTY", out var nondescriptGuiltyPreview))
        {
            var rewards = PeekTrialCardRewards(trial.Owner!, 2);
            var guiltyLines = new List<string>
            {
                "获得诅咒怀疑。"
            };
            if (rewards.Count == 0)
            {
                guiltyLines.Add("之后会出现 2 组选卡奖励。");
            }
            else
            {
                for (var rewardIndex = 0; rewardIndex < rewards.Count; rewardIndex++)
                {
                    guiltyLines.Add($"奖励 {rewardIndex + 1}：{JoinCards(rewards[rewardIndex])}。");
                }
            }

            SetPreview(previews, "NONDESCRIPT.options.GUILTY", PreviewCoverage.PartialNeedsInput,
                guiltyLines);
            AddEntities(nondescriptGuiltyPreview.Entities, CreateCardEntities(rewards.SelectMany(reward => reward)));
            var innocentLines = new List<string>
            {
                "获得诅咒怀疑。"
            };
            var innocentOutcomes = PeekTrialTransformOutcomes(trial.Owner!, trial.Rng, 2);
            innocentLines.AddRange(BuildOrderedTransformOutcomeLines(innocentOutcomes, 2));
            SetPreview(previews, "NONDESCRIPT.options.INNOCENT", PreviewCoverage.PartialNeedsInput, innocentLines);
            if (TryGetPreviewByTextKey(previews, "NONDESCRIPT.options.INNOCENT", out var nondescriptInnocentPreview))
            {
                AddEntities(nondescriptInnocentPreview.Entities, CreateCardEntities(innocentOutcomes.SelectMany(outcome => outcome.Targets)));
            }
        }
    }

    private static void ApplyTrialAcceptPreview(Trial trial, IList<EventOptionPreview> previews, string textKeySnippet)
    {
        if (!TryGetPreviewByTextKey(previews, textKeySnippet, out var preview))
        {
            return;
        }

        var branchRng = CloneRng(trial.Rng);
        var branch = branchRng.NextInt(3);
        var lines = BuildTrialAcceptLines(branch).ToList();
        if (trial.Owner is not null)
        {
            switch (branch)
            {
                case 0:
                {
                    var relics = PeekNextRelics(trial.Owner, 2).ToList();
                    if (relics.Count > 0)
                    {
                        lines.Add($"预测有罪奖励：{JoinRelics(relics)}。");
                        AddEntities(preview.Entities, CreateRelicEntities(relics));
                    }
                    break;
                }
                case 2:
                {
                    var rewards = PeekTrialCardRewards(trial.Owner, 2);
                    for (var rewardIndex = 0; rewardIndex < rewards.Count; rewardIndex++)
                    {
                        lines.Add($"预测有罪奖励 {rewardIndex + 1}：{JoinCards(rewards[rewardIndex])}。");
                    }
                    AddEntities(preview.Entities, CreateCardEntities(rewards.SelectMany(reward => reward)));

                    var innocentOutcomes = PeekTrialTransformOutcomes(trial.Owner, branchRng, 2);
                    lines.Add("预测无罪转化：");
                    lines.AddRange(BuildOrderedTransformOutcomeLines(innocentOutcomes, 2));
                    AddEntities(preview.Entities, CreateCardEntities(innocentOutcomes.SelectMany(outcome => outcome.Targets)));
                    break;
                }
            }
        }

        SetPreview(previews, textKeySnippet, PreviewCoverage.Complete, lines);
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

    private static IReadOnlyList<IReadOnlyList<CardModel>> PeekTrialCardRewards(Player player, int rewardCount)
    {
        var rewards = new List<IReadOnlyList<CardModel>>();
        var rewardState = new RewardPreviewState(player);

        for (var rewardIndex = 0; rewardIndex < rewardCount; rewardIndex++)
        {
            var options = CardCreationOptions.ForNonCombatWithDefaultOdds(new[] { player.Character.CardPool });
            var cards = PeekRewardCards(player, rewardState, options, 3);
            if (cards.Count > 0)
            {
                rewards.Add(cards);
            }
        }

        return rewards;
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

        var outcomes = PredictNextSlipperyBridgeHoldOns(bridge, currentCard, 10);
        if (outcomes.Count == 0)
        {
            lines.Add("之后没有可预测的可移除牌。");
        }
        else
        {
            foreach (var outcome in outcomes)
            {
                lines.Add($"坚持 {outcome.Step}：承受 {outcome.HpLoss} 点伤害，之后跨桥会失去 {CardTitle(outcome.Card)}。");
            }
        }

        lines.Add("每次坚持后仍可继续坚持，或直接跨桥。");
        SetPreview(previews, "HOLD_ON", PreviewCoverage.Complete, lines);
        if (TryGetPreviewByTextKey(previews, "HOLD_ON", out var holdOnPreview))
        {
            AddEntities(holdOnPreview.Entities, CreateCardEntities(outcomes.Select(outcome => outcome.Card)));
        }
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
        if (TryGetPreviewByTextKey(previews, "TOUCH_A_MIRROR", out var touchMirrorPreview))
        {
            SetPreview(previews, "TOUCH_A_MIRROR", PreviewCoverage.Complete, BuildReflectionsLines(reflections));
            var affectedCards = PredictReflectionsCards(reflections);
            AddEntities(touchMirrorPreview.Entities, CreateCardEntities(affectedCards.Downgraded.Concat(affectedCards.Upgraded)));
        }

        if (TryGetPreviewByTextKey(previews, "SHATTER", out var shatterPreview))
        {
            SetPreview(previews, "SHATTER", PreviewCoverage.Complete,
                "复制整副牌。",
                $"再加入 {CardTitle(ModelDb.Card<BadLuck>())}。");
            AddEntities(shatterPreview.Entities, CreateCardEntities(reflections.Owner!.Deck.Cards.Concat(new[] { ModelDb.Card<BadLuck>() })));
        }
    }

    private static void ApplyDoorsPreview(DoorsOfLightAndDark doors, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "LIGHT", out var lightPreview))
        {
            var cards = doors.Owner!.Deck.Cards
                .Where(card => card?.IsUpgradable ?? false)
                .ToList();
            var upgraded = cards
                .StableShuffle(CloneRng(doors.Rng))
                .Take(2)
                .ToList();

            if (upgraded.Count > 0)
            {
                var prefix = upgraded.Count == 1
                    ? "Light Door will upgrade the only available card"
                    : "Light Door will upgrade 2 random cards";
                SetPreview(previews, "LIGHT", PreviewCoverage.Complete,
                    $"{prefix}: {JoinCards(upgraded)}.");
                AddEntities(lightPreview.Entities, CreateCardEntities(upgraded));
            }
            else
            {
                SetPreview(previews, "LIGHT", PreviewCoverage.Complete, "Light Door has no upgradable cards.");
            }
        }

        if (TryGetPreviewByTextKey(previews, "DARK", out _))
        {
            SetPreview(previews, "DARK", PreviewCoverage.PartialNeedsInput,
                "还需要再选择 1 张牌移除。");
        }
    }

    private static void ApplyTabletOfTruthPreview(TabletOfTruth tablet, IList<EventOptionPreview> previews)
    {
        foreach (var preview in previews)
        {
            if (preview.SourceOption.IsLocked ||
                !preview.SourceOption.TextKey.Contains("DECIPHER", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var outcomes = PeekTabletOfTruthUpgradeOrder(tablet);
            var lines = new List<string>();
            var currentCount = Math.Clamp(GetTabletOfTruthDecipherCount(tablet), 1, 4);
            foreach (var outcome in outcomes)
            {
                var prefix = outcome.DecipherCount == currentCount ? "本次" : $"第 {outcome.DecipherCount} 次";
                if (outcome.UpgradesAll)
                {
                    lines.Add(outcome.Cards.Count == 0
                        ? $"{prefix} decipher：失去 {outcome.MaxHpLoss} 最大生命，然后没有可升级的牌。"
                        : $"{prefix} decipher：失去 {outcome.MaxHpLoss} 最大生命，然后升级全部剩余可升级牌：{JoinCards(outcome.Cards)}。");
                }
                else
                {
                    lines.Add(outcome.Cards.Count == 0
                        ? $"{prefix} decipher：失去 {outcome.MaxHpLoss} 最大生命，然后没有可升级的牌。"
                        : $"{prefix} decipher：失去 {outcome.MaxHpLoss} 最大生命，升级 {JoinCards(outcome.Cards)}。");
                }
            }

            if (lines.Count == 0)
            {
                lines.Add("没有更多 decipher 升级可预测。");
            }

            preview.Coverage = PreviewCoverage.Complete;
            preview.Lines.Clear();
            foreach (var line in lines)
            {
                AddLine(preview.Lines, line);
            }

            preview.Entities.Clear();
            AddEntities(preview.Entities, CreateCardEntities(outcomes.SelectMany(outcome => outcome.Cards)));
        }
    }

    private static void ApplyTrashHeapPreview(TrashHeap trashHeap, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "DIVE_IN", out var diveInPreview))
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
                AddEntities(diveInPreview.Entities, CreateRelicEntities(new[] { diveRelic }));
            }
        }

        if (TryGetPreviewByTextKey(previews, "GRAB", out var grabPreview))
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
                AddEntities(grabPreview.Entities, CreateCardEntities(new[] { grabCard }));
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
                AddEntities(bargainBinPreview.Entities, CreateRelicEntities(new[] { commonRelic }));
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
                AddEntities(featuredPreview.Entities, CreateRelicEntities(new[] { featuredItem }));
            }
        }

        if (TryGetPreviewByTextKey(previews, "MYSTERY_BOX", out var mysteryPreview) && !mysteryPreview.SourceOption.IsLocked)
        {
            SetPreview(previews, "MYSTERY_BOX", PreviewCoverage.Complete,
                $"花费 {wongos.DynamicVars["MysteryBoxCost"].IntValue} 金币。",
                $"会拿到 {RelicTitle(ModelDb.Relic<WongosMysteryTicket>())}。");
            AddEntities(mysteryPreview.Entities, CreateRelicEntities(new[] { ModelDb.Relic<WongosMysteryTicket>() }));
        }

        if (TryGetPreviewByTextKey(previews, "LEAVE", out var leavePreview))
        {
            var downgraded = CloneRng(wongos.Rng).NextItem(wongos.Owner!.Deck.Cards.Where(card => card.IsUpgraded));
            SetPreview(previews, "LEAVE", PreviewCoverage.Complete,
                downgraded is null
                    ? "离开时不会降级任何牌。"
                    : $"离开时会降级 {CardTitle(downgraded)}。");
            if (downgraded is not null)
            {
                AddEntities(leavePreview.Entities, CreateCardEntities(new[] { downgraded }));
            }
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
            if (rewardRelic is not null)
            {
                SetEntities(previews, "INITIAL.options.POTION", CreateRelicEntities(new[] { rewardRelic }));
            }
        }

        if (TryGetPreviewByTextKey(previews, "INITIAL.options.GOLD", out _))
        {
            SetPreview(previews, "INITIAL.options.GOLD", PreviewCoverage.Complete,
                $"花费 {ranwid.DynamicVars.Gold.IntValue} 金币。",
                rewardRelic is null ? "随后获得 1 件遗物。" : $"随后获得 {RelicTitle(rewardRelic)}。");
            if (rewardRelic is not null)
            {
                SetEntities(previews, "INITIAL.options.GOLD", CreateRelicEntities(new[] { rewardRelic }));
            }
        }

        if (TryGetStringVar(ranwid, "Relic", out var relicName))
        {
            SetPreview(previews, "INITIAL.options.RELIC", PreviewCoverage.Complete,
                $"交出 {relicName}。",
                rewardRelics.Count == 0 ? "随后获得 2 件遗物。" : $"随后获得 {JoinRelics(rewardRelics)}。");
            if (rewardRelics.Count > 0)
            {
                SetEntities(previews, "INITIAL.options.RELIC", CreateRelicEntities(rewardRelics));
            }
        }
    }

    private static void ApplyBattlewornDummyPreview(BattlewornDummy dummy, IList<EventOptionPreview> previews)
    {
        var owner = dummy.Owner!;

        if (TryGetPreviewByTextKey(previews, "SETTING_1", out var setting1Preview))
        {
            var potion = PeekSharedRewardPotion(owner);
            SetPreview(previews, "SETTING_1", PreviewCoverage.PartialNeedsInput,
                potion is null
                    ? "战斗胜利后会获得 1 瓶药水；超时则无奖励。"
                    : $"战斗胜利后会获得 {PotionTitle(potion)}；超时则无奖励。");
            if (potion is not null)
            {
                AddEntities(setting1Preview.Entities, CreatePotionEntities(new[] { potion }));
            }
        }

        if (TryGetPreviewByTextKey(previews, "SETTING_2", out var setting2Preview))
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
            AddEntities(setting2Preview.Entities, CreateCardEntities(upgraded));
        }

        if (TryGetPreviewByTextKey(previews, "SETTING_3", out var setting3Preview))
        {
            var relic = PeekNextRelics(owner, 1).FirstOrDefault();
            SetPreview(previews, "SETTING_3", PreviewCoverage.PartialNeedsInput,
                relic is null
                    ? "战斗胜利后会获得下一件遗物；超时则无奖励。"
                    : $"战斗胜利后会获得 {RelicTitle(relic)}；超时则无奖励。");
            if (relic is not null)
            {
                AddEntities(setting3Preview.Entities, CreateRelicEntities(new[] { relic }));
            }
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

        if (TryGetPreviewByTextKey(previews, "ORNATE", out var ornatePreview))
        {
            var relic = PeekNextRelics(thisOrThat.Owner!, 1).FirstOrDefault();
            if (relic is not null)
            {
                SetPreview(previews, "ORNATE", PreviewCoverage.Complete,
                    $"获得 {RelicTitle(relic)}。",
                    $"再加入 {CardTitle(ModelDb.Card<Clumsy>())}。");
                AddEntities(ornatePreview.Entities, CreateRelicEntities(new[] { relic }));
            }
        }
    }

    private static void ApplyAromaOfChaosPreview(AromaOfChaos aromaOfChaos, IList<EventOptionPreview> previews)
    {
        if (TryGetPreviewByTextKey(previews, "LET_GO", out var letGoPreview))
        {
            var mappings = BuildTransformSelectionMappings(aromaOfChaos.Owner!, aromaOfChaos.Rng);
            SetPreview(previews, "LET_GO", PreviewCoverage.PartialNeedsInput,
                BuildTransformSelectionPreview(mappings, 1));
            AddEntities(letGoPreview.Entities, CreateCardEntities(mappings.Select(item => item.Target)));
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
        if (TryGetPreviewByTextKey(previews, "GROUP", out var groupPreview))
        {
            var outcomes = PeekTrialTransformOutcomes(morphicGrove.Owner!, morphicGrove.Rng, 2);
            var lines = new List<string>
            {
                "选择 2 张牌变化；结果会随选择顺序变化。"
            };
            lines.AddRange(BuildOrderedTransformOutcomeLines(outcomes, 2));
            SetPreview(previews, "GROUP", PreviewCoverage.PartialNeedsInput, lines);
            AddEntities(groupPreview.Entities, CreateCardEntities(outcomes.SelectMany(outcome => outcome.Targets)));
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
        if (TryGetPreviewByTextKey(previews, "BOTTLE", out var bottlePreview))
        {
            var potion = PeekOutOfCombatPotions(wellspring.Owner!, 1).FirstOrDefault();
            SetPreview(previews, "BOTTLE", PreviewCoverage.Complete,
                potion is null ? "会获得 1 瓶药水。" : $"会获得 {PotionTitle(potion)}。");
            if (potion is not null)
            {
                AddEntities(bottlePreview.Entities, CreatePotionEntities(new[] { potion }));
            }
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
        if (TryGetPreviewByTextKey(previews, "GOLD", out var goldPreview))
        {
            var potions = PeekOutOfCombatPotions(whisperingHollow.Owner!, 2);
            SetPreview(previews, "GOLD", PreviewCoverage.Complete,
                $"花费 {whisperingHollow.DynamicVars.Gold.IntValue} 金币。",
                potions.Count == 0 ? "之后会获得 2 瓶药水。" : $"之后会获得 {JoinPotions(potions)}。");
            AddEntities(goldPreview.Entities, CreatePotionEntities(potions));
        }

        if (TryGetPreviewByTextKey(previews, "HUG", out var hugPreview))
        {
            var mappings = BuildTransformSelectionMappings(whisperingHollow.Owner!, whisperingHollow.Rng);
            var lines = new List<string>
            {
                $"先失去 {whisperingHollow.DynamicVars.HpLoss.IntValue} 点生命。"
            };
            lines.AddRange(BuildTransformSelectionPreview(mappings, 1));
            SetPreview(previews, "HUG", PreviewCoverage.PartialNeedsInput, lines);
            AddEntities(hugPreview.Entities, CreateCardEntities(mappings.Select(item => item.Target)));
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

        if (TryGetPreviewByTextKey(previews, "KILL_WITH_FIRE", out var killWithFirePreview))
        {
            var mappings = BuildTransformSelectionMappings(symbiote.Owner!, symbiote.Rng);
            SetPreview(previews, "KILL_WITH_FIRE", PreviewCoverage.PartialNeedsInput,
                BuildTransformSelectionPreview(mappings, symbiote.DynamicVars.Cards.IntValue));
            AddEntities(killWithFirePreview.Entities, CreateCardEntities(mappings.Select(item => item.Target)));
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
                AddEntities(tributePreview.Entities, CreateRelicEntities(new[] { relic }));
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
            ApplyPunchOffFightRewardPreview(punchOff, takeThemPreview, "会先进入下一页。下一页开打胜利后：");
        }

        if (TryGetPreviewByTextKey(previews, "FIGHT", out var fightPreview) && !fightPreview.SourceOption.IsLocked)
        {
            ApplyPunchOffFightRewardPreview(punchOff, fightPreview, "战斗胜利后：");
        }
    }

    private static void ApplyPunchOffFightRewardPreview(PunchOff punchOff, EventOptionPreview preview, string prefix)
    {
        var rewards = PeekPunchOffFightRewards(punchOff.Owner!);

        preview.Coverage = PreviewCoverage.PartialNeedsInput;
        preview.Lines.Clear();
        AddLine(preview.Lines, prefix);
        AddLine(preview.Lines, rewards.Relics.Count == 0 ? "获得 1 件遗物。" : $"获得 {JoinRelics(rewards.Relics)}。");
        AddLine(preview.Lines, rewards.Potions.Count == 0 ? "获得 1 瓶药水。" : $"获得 {JoinPotions(rewards.Potions)}。");
        AddLine(preview.Lines, rewards.Cards.Count == 0 ? "出现 1 组选卡奖励。" : $"选卡奖励：{JoinCards(rewards.Cards)}。");

        preview.Entities.Clear();
        AddEntities(preview.Entities, CreateRelicEntities(rewards.Relics));
        AddEntities(preview.Entities, CreatePotionEntities(rewards.Potions));
        AddEntities(preview.Entities, CreateCardEntities(rewards.Cards));
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
        if (TryGetPreviewByTextKey(previews, "OBSERVE_CHEF", out var observeChefPreview))
        {
            var upgraded = CloneRng(endlessConveyor.Rng).NextItem(endlessConveyor.Owner!.Deck.Cards.Where(card => card.IsUpgradable));
            SetPreview(previews, "OBSERVE_CHEF", PreviewCoverage.Complete,
                upgraded is null ? "不会升级任何牌。" : $"会升级 {CardTitle(upgraded)}。");
            if (upgraded is not null)
            {
                AddEntities(observeChefPreview.Entities, CreateCardEntities(new[] { upgraded }));
            }
        }

        var currentDishId = GetEndlessConveyorCurrentDishId(endlessConveyor);
        if (!string.IsNullOrWhiteSpace(currentDishId) &&
            TryDescribeEndlessDish(endlessConveyor, currentDishId, out var dishLines))
        {
            var key = currentDishId;
            if (TryGetPreviewByTextKey(previews, "LOCKED", out var lockedPreview) && lockedPreview.SourceOption.IsLocked)
            {
                key = "LOCKED";
            }

            var lines = dishLines.ToList();
            lines.AddRange(BuildEndlessConveyorSequenceLines(endlessConveyor));
            SetPreview(previews, key, PreviewCoverage.PartialNeedsInput, lines);
        }
    }

    private static IReadOnlyList<string> BuildEndlessConveyorSequenceLines(EndlessConveyor endlessConveyor)
    {
        var currentDishId = GetEndlessConveyorCurrentDishId(endlessConveyor);
        if (string.IsNullOrWhiteSpace(currentDishId))
        {
            return Array.Empty<string>();
        }

        var owner = endlessConveyor.Owner!;
        var cost = endlessConveyor.DynamicVars.Gold.IntValue;
        var gold = owner.Gold;
        var rng = CloneRng(endlessConveyor.Rng);
        var numOfGrabs = previewsafeGetNumber(endlessConveyor, "_numOfGrabs");
        var lastDishId = GetPrivateString(endlessConveyor, "_lastDishId");
        var virtualUpgraded = new HashSet<CardModel>();
        var lines = new List<string> { "连续抓取预测：" };

        for (var grab = 1; grab <= 200; grab++)
        {
            if (gold < cost)
            {
                lines.Add($"停止：金币 {gold}，不足 {cost}，第一项会灰掉。");
                break;
            }

            var beforeGold = gold;
            if (currentDishId == "GOLDEN_FYSH")
            {
                gold += endlessConveyor.DynamicVars["GoldenFyshGold"].IntValue;
            }
            else
            {
                gold -= cost;
            }

            var effect = BuildEndlessConveyorSimulatedEffect(endlessConveyor, currentDishId, rng, virtualUpgraded);
            lines.Add($"{grab}. {EndlessDishTitle(currentDishId)}：{effect}（金币 {beforeGold} -> {gold}）。");

            if (currentDishId == "JELLY_LIVER")
            {
                lines.Add("Jelly Liver 之后的下一道菜取决于你选哪张牌转化；预测在这里停止。");
                break;
            }

            lastDishId = currentDishId;
            currentDishId = RollNextEndlessConveyorDishId(endlessConveyor, rng, ++numOfGrabs, lastDishId);
            if (string.IsNullOrWhiteSpace(currentDishId))
            {
                lines.Add("停止：无法预测下一道料理。");
                break;
            }
        }

        return lines;
    }

    private static string BuildEndlessConveyorSimulatedEffect(
        EndlessConveyor endlessConveyor,
        string dishId,
        Rng rng,
        ISet<CardModel> virtualUpgraded)
    {
        var owner = endlessConveyor.Owner!;
        switch (dishId)
        {
            case "CLAM_ROLL":
                return $"回复 {endlessConveyor.DynamicVars["ClamRollHeal"].IntValue} 点生命";
            case "CAVIAR":
                return $"增加 {endlessConveyor.DynamicVars["CaviarMaxHp"].IntValue} 点最大生命";
            case "SUSPICIOUS_CONDIMENT":
                return "获得 1 瓶药水";
            case "JELLY_LIVER":
                return "选择 1 张牌并随机转化";
            case "SEAPUNK_SALAD":
                return $"加入 {CardTitle(ModelDb.Card<FeedingFrenzy>())}";
            case "FRIED_EEL":
                return "加入 1 张无色牌";
            case "GOLDEN_FYSH":
                return $"获得 {endlessConveyor.DynamicVars["GoldenFyshGold"].IntValue} 金币";
            case "SPICY_SNAPPY":
            {
                var candidates = owner.Deck.Cards
                    .Where(card => card.IsUpgradable && !virtualUpgraded.Contains(card))
                    .ToList();
                if (candidates.Count == 0)
                {
                    return "不会升级任何牌";
                }

                var upgraded = rng.NextItem(candidates);
                if (upgraded is null)
                {
                    return "不会升级任何牌";
                }

                virtualUpgraded.Add(upgraded);
                return $"升级 {CardTitle(upgraded)}";
            }
            default:
                return "执行当前料理效果";
        }
    }

    private static string? RollNextEndlessConveyorDishId(EndlessConveyor endlessConveyor, Rng rng, int nextNumOfGrabs, string? lastDishId)
    {
        if (nextNumOfGrabs % 5 == 0)
        {
            return "SEAPUNK_SALAD";
        }

        var owner = endlessConveyor.Owner!;
        var dishes = new List<(string Id, float Weight)>
        {
            ("CAVIAR", 6f),
            ("SPICY_SNAPPY", 3f),
            ("JELLY_LIVER", 3f),
            ("FRIED_EEL", 3f)
        };

        if (owner.HasOpenPotionSlots)
        {
            dishes.Add(("SUSPICIOUS_CONDIMENT", 3f));
        }

        if (owner.Creature.CurrentHp != owner.Creature.MaxHp)
        {
            dishes.Add(("CLAM_ROLL", 6f));
        }

        if (nextNumOfGrabs > 1)
        {
            dishes.Add(("GOLDEN_FYSH", 1f));
        }

        dishes.RemoveAll(dish => string.Equals(dish.Id, lastDishId, StringComparison.OrdinalIgnoreCase));
        if (dishes.Count == 0)
        {
            return null;
        }

        var totalWeight = dishes.Sum(dish => dish.Weight);
        var roll = rng.NextFloat(1f) * totalWeight;
        var cumulative = 0f;
        foreach (var dish in dishes)
        {
            cumulative += dish.Weight;
            if (roll < cumulative)
            {
                return dish.Id;
            }
        }

        return dishes[^1].Id;
    }

    private static string? GetEndlessConveyorCurrentDishId(EndlessConveyor endlessConveyor)
    {
        var field = AccessTools.Field(typeof(EndlessConveyor), "_currentDish");
        var currentDish = field?.GetValue(endlessConveyor);
        return currentDish is null ? null : AccessTools.Field(currentDish.GetType(), "id")?.GetValue(currentDish) as string;
    }

    private static string? GetPrivateString<T>(T instance, string fieldName)
    {
        return AccessTools.Field(typeof(T), fieldName)?.GetValue(instance) as string;
    }

    private static string EndlessDishTitle(string dishId)
    {
        return RandomVisionGameText.ResolveLocString(new LocString("events", $"ENDLESS_CONVEYOR.DISHES.{dishId}.title"));
    }

    private static bool TryDescribeEndlessDish(EndlessConveyor endlessConveyor, string currentDishId, out IReadOnlyList<string> lines)
    {
        var owner = endlessConveyor.Owner!;
        lines = Array.Empty<string>();
        var cardRewardState = new RewardPreviewState(owner);

        if (string.Equals(currentDishId, "CLAM_ROLL", StringComparison.OrdinalIgnoreCase))
        {
            lines = new[] { $"当前料理：回复 {endlessConveyor.DynamicVars["ClamRollHeal"].IntValue} 点生命。", $"会花费 {endlessConveyor.DynamicVars.Gold.IntValue} 金币。" };
            return true;
        }
        if (string.Equals(currentDishId, "CAVIAR", StringComparison.OrdinalIgnoreCase))
        {
            lines = new[] { $"当前料理：增加 {endlessConveyor.DynamicVars["CaviarMaxHp"].IntValue} 点最大生命。", $"会花费 {endlessConveyor.DynamicVars.Gold.IntValue} 金币。" };
            return true;
        }
        if (string.Equals(currentDishId, "SUSPICIOUS_CONDIMENT", StringComparison.OrdinalIgnoreCase))
        {
            var potion = PeekSharedRewardPotion(owner);
            lines = new[] { potion is null ? "当前料理：获得 1 瓶药水。" : $"当前料理：获得 {PotionTitle(potion)}。", $"会花费 {endlessConveyor.DynamicVars.Gold.IntValue} 金币。" };
            return true;
        }
        if (string.Equals(currentDishId, "JELLY_LIVER", StringComparison.OrdinalIgnoreCase))
        {
            lines = BuildTransformSelectionPreview(owner, endlessConveyor.Rng, 1)
                .Prepend($"会花费 {endlessConveyor.DynamicVars.Gold.IntValue} 金币。")
                .ToList();
            return true;
        }
        if (string.Equals(currentDishId, "SEAPUNK_SALAD", StringComparison.OrdinalIgnoreCase))
        {
            lines = new[] { $"当前料理：加入 {CardTitle(ModelDb.Card<FeedingFrenzy>())}。", "每第 5 次抓取都会出现。" };
            return true;
        }
        if (string.Equals(currentDishId, "FRIED_EEL", StringComparison.OrdinalIgnoreCase))
        {
            var eelCards = PeekRewardCards(owner, cardRewardState, CardCreationOptions.ForNonCombatWithDefaultOdds(new[] { ModelDb.CardPool<ColorlessCardPool>() }), 1);
            lines = new[] { eelCards.Count > 0 ? $"当前料理：加入 {CardTitle(eelCards[0])}。" : "当前料理：加入 1 张无色牌。", $"会花费 {endlessConveyor.DynamicVars.Gold.IntValue} 金币。" };
            return true;
        }
        if (string.Equals(currentDishId, "GOLDEN_FYSH", StringComparison.OrdinalIgnoreCase))
        {
            lines = new[] { $"当前料理：获得 {endlessConveyor.DynamicVars["GoldenFyshGold"].IntValue} 金币。", "这次不会花钱。" };
            return true;
        }
        if (string.Equals(currentDishId, "SPICY_SNAPPY", StringComparison.OrdinalIgnoreCase))
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

        if (TryGetPreviewByTextKey(previews, "FRAGRANT_MUSHROOM", out var fragrantPreview))
        {
            var upgraded = hungryForMushrooms.Owner!.Deck.Cards
                .Where(card => card?.IsUpgradable ?? false)
                .ToList()
                .StableShuffle(CloneRng(hungryForMushrooms.Owner.RunState.Rng.Niche))
                .Take(ModelDb.Relic<FragrantMushroom>().DynamicVars.Cards.IntValue)
                .ToList();
            SetPreview(previews, "FRAGRANT_MUSHROOM", PreviewCoverage.Complete,
                "会失去 15 点生命。",
                $"然后获得 {RelicTitle(ModelDb.Relic<FragrantMushroom>())}。",
                upgraded.Count == 0 ? "不会升级任何牌。" : $"会升级 {JoinCards(upgraded)}。");
            SetEntities(previews, "FRAGRANT_MUSHROOM", CreateRelicEntities(new[] { ModelDb.Relic<FragrantMushroom>() }));
            AddEntities(fragrantPreview.Entities, CreateCardEntities(upgraded));
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

        if (TryGetPreviewByTextKey(previews, "RANSACK", out var ransackPreview))
        {
            var potion = PeekPotionByRarity(potionCourier.Owner!, PotionRarity.Uncommon);
            SetPreview(previews, "RANSACK", PreviewCoverage.Complete,
                potion is null ? "会获得 1 瓶非凡药水。" : $"会获得 {PotionTitle(potion)}。");
            if (potion is not null)
            {
                AddEntities(ransackPreview.Entities, CreatePotionEntities(new[] { potion }));
            }
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

        if (TryGetPreviewByTextKey(previews, "PICK_FIGHT", out var pickFightPreview))
        {
            var relic = PeekNextRelics(roundTeaParty.Owner!, 1).FirstOrDefault();
            SetPreview(previews, "PICK_FIGHT", PreviewCoverage.Complete,
                "会先进入下一页。",
                "下一页只能继续打架。",
                relic is null ? "打架后会获得下一件遗物。" : $"打架后会获得 {RelicTitle(relic)}。");
            if (relic is not null)
            {
                AddEntities(pickFightPreview.Entities, CreateRelicEntities(new[] { relic }));
            }
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
            var rng = CloneRng(tinkerTime.Rng);
            var nextTypes = TinkerTimeCardTypes()
                .TakeRandom(2, rng)
                .ToList();
            var lines = new List<string>
            {
                $"第 2 层会出现 2 个类型选项：{JoinTitles(nextTypes.Select(TinkerTimeCardTypeTitle))}。"
            };
            var entities = new List<EventPreviewEntity>();
            foreach (var cardType in nextTypes)
            {
                var riderRng = CloneRng(rng);
                var riders = TinkerTimeRidersFor(cardType)
                    .TakeRandom(2, riderRng)
                    .ToList();
                lines.Add($"若选 {TinkerTimeCardTypeTitle(cardType)}，第 3 层会出现：{JoinTitles(riders.Select(TinkerTimeRiderTitle))}。");
                AddEntities(entities, CreateTinkerTimeCardEntities(tinkerTime, cardType, riders));
            }

            SetPreview(previews, "INITIAL.options.CHOOSE_CARD_TYPE", PreviewCoverage.PartialNeedsInput, lines);
            SetEntities(previews, "INITIAL.options.CHOOSE_CARD_TYPE", entities);
            return;
        }

        foreach (var preview in previews.Where(preview => !preview.SourceOption.IsLocked))
        {
            if (TryGetTinkerTimeCardType(preview.SourceOption, out var cardType))
            {
                var riders = TinkerTimeRidersFor(cardType)
                    .TakeRandom(2, CloneRng(tinkerTime.Rng))
                    .ToList();
                preview.Coverage = PreviewCoverage.PartialNeedsInput;
                preview.Lines.Clear();
                AddLine(preview.Lines, $"第 3 层会出现 2 个改造效果：{JoinTitles(riders.Select(TinkerTimeRiderTitle))}。");
                foreach (var rider in riders)
                {
                    AddLine(preview.Lines, $"若选 {TinkerTimeRiderTitle(rider)}，获得 {TinkerTimeMadScienceTitle(tinkerTime, cardType, rider)}。");
                }

                AddEntities(preview.Entities, CreateTinkerTimeCardEntities(tinkerTime, cardType, riders));
                continue;
            }

            if (TryGetTinkerTimeRider(preview.SourceOption, out var finalRider))
            {
                var finalCard = CreateTinkerTimeCard(tinkerTime, GetTinkerTimeChosenCardType(tinkerTime), finalRider);
                preview.Coverage = PreviewCoverage.Complete;
                preview.Lines.Clear();
                AddLine(preview.Lines,
                    $"获得 {TinkerTimeMadScienceTitle(finalCard, GetTinkerTimeChosenCardType(tinkerTime), finalRider)}。");
                AddEntities(preview.Entities, CreateTinkerTimeCardEntities(
                    tinkerTime,
                    GetTinkerTimeChosenCardType(tinkerTime),
                    new[] { finalRider }));
            }
        }
    }

    private static IReadOnlyList<CardType> TinkerTimeCardTypes()
    {
        return new[] { CardType.Attack, CardType.Skill, CardType.Power };
    }

    private static IReadOnlyList<TinkerTime.RiderEffect> TinkerTimeRidersFor(CardType cardType)
    {
        return cardType switch
        {
            CardType.Attack => new[]
            {
                TinkerTime.RiderEffect.Sapping,
                TinkerTime.RiderEffect.Violence,
                TinkerTime.RiderEffect.Choking
            },
            CardType.Skill => new[]
            {
                TinkerTime.RiderEffect.Energized,
                TinkerTime.RiderEffect.Wisdom,
                TinkerTime.RiderEffect.Chaos
            },
            CardType.Power => new[]
            {
                TinkerTime.RiderEffect.Expertise,
                TinkerTime.RiderEffect.Curious,
                TinkerTime.RiderEffect.Improvement
            },
            _ => Array.Empty<TinkerTime.RiderEffect>()
        };
    }

    private static bool TryGetTinkerTimeCardType(EventOption option, out CardType cardType)
    {
        var key = option.TextKey;
        if (key.Contains("CHOOSE_CARD_TYPE.options.ATTACK", StringComparison.OrdinalIgnoreCase))
        {
            cardType = CardType.Attack;
            return true;
        }

        if (key.Contains("CHOOSE_CARD_TYPE.options.SKILL", StringComparison.OrdinalIgnoreCase))
        {
            cardType = CardType.Skill;
            return true;
        }

        if (key.Contains("CHOOSE_CARD_TYPE.options.POWER", StringComparison.OrdinalIgnoreCase))
        {
            cardType = CardType.Power;
            return true;
        }

        return TryInferCardTypeFromOption(option, out cardType);
    }

    private static CardType GetTinkerTimeChosenCardType(TinkerTime tinkerTime)
    {
        return AccessTools.Field(typeof(TinkerTime), "_chosenCardType")?.GetValue(tinkerTime) is CardType cardType
            ? cardType
            : default;
    }

    private static bool TryGetTinkerTimeRider(EventOption option, out TinkerTime.RiderEffect rider)
    {
        var key = option.TextKey;
        foreach (var candidate in Enum.GetValues<TinkerTime.RiderEffect>())
        {
            if (candidate == TinkerTime.RiderEffect.None)
            {
                continue;
            }

            if (key.Contains($".{candidate.ToString().ToUpperInvariant()}", StringComparison.OrdinalIgnoreCase))
            {
                rider = candidate;
                return true;
            }
        }

        rider = default;
        return false;
    }

    private static string TinkerTimeCardTypeTitle(CardType cardType)
    {
        return cardType.ToLocString().GetFormattedText();
    }

    private static string TinkerTimeRiderTitle(TinkerTime.RiderEffect rider)
    {
        return rider switch
        {
            TinkerTime.RiderEffect.Sapping => "Sapping",
            TinkerTime.RiderEffect.Violence => "Violence",
            TinkerTime.RiderEffect.Choking => "Choking",
            TinkerTime.RiderEffect.Energized => "Energized",
            TinkerTime.RiderEffect.Wisdom => "Wisdom",
            TinkerTime.RiderEffect.Chaos => "Chaos",
            TinkerTime.RiderEffect.Expertise => "Expertise",
            TinkerTime.RiderEffect.Curious => "Curious",
            TinkerTime.RiderEffect.Improvement => "Improvement",
            _ => rider.ToString()
        };
    }

    private static string TinkerTimeMadScienceTitle(TinkerTime tinkerTime, CardType cardType, TinkerTime.RiderEffect rider)
    {
        return TinkerTimeMadScienceTitle(CreateTinkerTimeCard(tinkerTime, cardType, rider), cardType, rider);
    }

    private static string TinkerTimeMadScienceTitle(CardModel? card, CardType cardType, TinkerTime.RiderEffect rider)
    {
        var title = card is null ? CardTitle(ModelDb.Card<MadScience>()) : CardTitle(card);
        return $"{title}（{TinkerTimeCardTypeTitle(cardType)} + {TinkerTimeRiderTitle(rider)}）";
    }

    private static IReadOnlyList<EventPreviewEntity> CreateTinkerTimeCardEntities(TinkerTime tinkerTime, CardType cardType, IEnumerable<TinkerTime.RiderEffect> riders)
    {
        var entities = new List<EventPreviewEntity>();
        foreach (var rider in riders)
        {
            var card = CreateTinkerTimeCard(tinkerTime, cardType, rider);
            if (card is null)
            {
                continue;
            }

            entities.Add(new EventPreviewEntity(
                $"card:{card.Id}:{card.IsUpgraded}:tinker:{cardType}:{rider}",
                TinkerTimeMadScienceTitle(card, cardType, rider),
                new IHoverTip[] { new CardHoverTip(card) }));
        }

        return entities;
    }

    private static CardModel? CreateTinkerTimeCard(TinkerTime tinkerTime, CardType cardType, TinkerTime.RiderEffect rider)
    {
        if (tinkerTime.Owner is null)
        {
            return null;
        }

        var card = tinkerTime.Owner.RunState.CreateCard<MadScience>(tinkerTime.Owner);
        card.TinkerTimeType = cardType;
        card.TinkerTimeRider = rider;
        return card;
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
            var rewards = PeekPotionThenRelicRewards(warHistorianRepy.Owner!, potionCount: 2, relicCount: 2);
            SetPreview(previews, "UNLOCK_CHEST", PreviewCoverage.PartialNeedsInput,
                $"移除所有 {CardTitle(ModelDb.Card<LanternKey>())}。",
                BuildPotionRelicRewardLine(rewards.Potions, rewards.Relics));
            var entities = CreateCardEntities(new[] { ModelDb.Card<LanternKey>() }).ToList();
            AddEntities(entities, CreatePotionEntities(rewards.Potions));
            AddEntities(entities, CreateRelicEntities(rewards.Relics));
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
        var cards = PredictReflectionsCards(reflections);

        if (cards.Downgraded.Count > 0)
        {
            lines.Add($"先降级 {JoinCards(cards.Downgraded)}。");
        }

        if (cards.Upgraded.Count > 0)
        {
            lines.Add($"再升级 {JoinCards(cards.Upgraded)}。");
        }

        if (lines.Count == 0)
        {
            lines.Add("当前没有可变化的牌。");
        }

        return lines;
    }

    private static (IReadOnlyList<CardModel> Downgraded, IReadOnlyList<CardModel> Upgraded) PredictReflectionsCards(Reflections reflections)
    {
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

        return (downgraded, upgraded);
    }

    private static IReadOnlyList<(int Step, int HpLoss, CardModel Card)> PredictNextSlipperyBridgeHoldOns(
        SlipperyBridge bridge,
        CardModel? currentCard,
        int count)
    {
        var owner = bridge.Owner!;
        var rng = CloneRng(bridge.Rng);
        var skippedRemovals = SlipperyBridgeSkippedRemovalsRef(bridge) is { } existingSkipped
            ? new HashSet<CardModel>(existingSkipped)
            : new HashSet<CardModel>();
        var numberOfHoldOns = SlipperyBridgeHoldOnsRef(bridge);
        var outcomes = new List<(int Step, int HpLoss, CardModel Card)>();

        for (var step = 1; step <= count; step++)
        {
            var nextCard = PredictNextSlipperyBridgeCard(owner, rng, currentCard, skippedRemovals);
            if (nextCard is null)
            {
                break;
            }

            outcomes.Add((step, 3 + numberOfHoldOns + step - 1, nextCard));
            currentCard = nextCard;
        }

        return outcomes;
    }

    private static CardModel? PredictNextSlipperyBridgeCard(
        Player owner,
        Rng rng,
        CardModel? currentCard,
        ISet<CardModel> skippedRemovals)
    {
        var deckCards = owner.Deck.Cards;
        List<CardModel> candidates;
        if (currentCard is null)
        {
            candidates = deckCards
                .Where(card => card.Rarity != CardRarity.Basic)
                .ToList();
        }
        else
        {
            skippedRemovals.Add(currentCard);
            candidates = deckCards
                .Where(card => card.GetType() != currentCard.GetType())
                .ToList();
        }

        candidates.RemoveAll(card => !card.IsRemovable || skippedRemovals.Contains(card));
        if (candidates.Count == 0)
        {
            candidates = deckCards
                .Where(card => card.IsRemovable)
                .ToList();
        }

        return rng.NextItem(candidates);
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
        var candidates = BuildTransformSelectionCandidates(player);
        if (candidates.Count == 0)
        {
            return new[] { "当前没有可转化的牌。" };
        }

        return BuildTransformSelectionPreview(BuildTransformSelectionMappings(candidates, rng), selectionCount);
    }

    private static IReadOnlyList<(CardModel Source, IReadOnlyList<CardModel> Targets)> PeekTrialTransformOutcomes(Player player, Rng rng, int selectionCount)
    {
        var candidates = BuildTransformSelectionCandidates(player);
        if (candidates.Count == 0 || selectionCount <= 0)
        {
            return Array.Empty<(CardModel Source, IReadOnlyList<CardModel> Targets)>();
        }

        selectionCount = Math.Min(selectionCount, candidates.Count);
        var targetsByCandidate = candidates
            .Select(_ => new List<CardModel>())
            .ToList();
        var selected = new List<int>(selectionCount);
        var used = new bool[candidates.Count];

        SimulateOrderedTransformSelections(candidates, rng, selectionCount, selected, used, targetsByCandidate, upgradeTargets: false);

        return candidates
            .Select((source, index) => (Source: source, Targets: (IReadOnlyList<CardModel>)DeduplicateCardsByTitle(targetsByCandidate[index])))
            .Where(item => item.Targets.Count > 0)
            .ToList();
    }

    private static IReadOnlyList<string> BuildOrderedTransformOutcomeLines(IReadOnlyList<(CardModel Source, IReadOnlyList<CardModel> Targets)> outcomes, int selectionCount)
    {
        if (outcomes.Count == 0)
        {
            return new[] { "当前没有可转化的牌。" };
        }

        var lines = new List<string>
        {
            $"还需要选择 {selectionCount} 张牌；结果会随选择顺序变化。"
        };
        foreach (var outcome in outcomes)
        {
            lines.Add($"{CardTitle(outcome.Source)} -> {JoinCards(outcome.Targets)}。");
        }

        return lines;
    }

    private static IReadOnlyList<(CardModel Source, CardModel Target)> BuildTransformSelectionMappings(Player player, Rng rng)
    {
        return BuildTransformSelectionMappings(BuildTransformSelectionCandidates(player), rng);
    }

    private static List<CardModel> BuildTransformSelectionCandidates(Player player)
    {
        var candidates = PileType.Deck.GetPile(player).Cards
            .Where(card => card.Type != CardType.Quest && card.IsTransformable)
            .ToList();

        return candidates;
    }

    private static IReadOnlyList<(CardModel Source, CardModel Target)> BuildTransformSelectionMappings(
        IReadOnlyList<CardModel> candidates,
        Rng rng)
    {
        return candidates
            .Select(card => (Source: card, Target: PeekTransformTarget(card, rng)))
            .Where(item => item.Target is not null)
            .Select(item => (item.Source, item.Target!))
            .ToList();
    }

    private static IReadOnlyList<string> BuildTransformSelectionPreview(IReadOnlyList<(CardModel Source, CardModel Target)> mappings, int selectionCount)
    {
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

    private static IReadOnlyList<IReadOnlyList<CardModel>> PeekKaleidoscopeRewards(Player player, int rewardCount)
    {
        var rewards = new List<IReadOnlyList<CardModel>>();
        var rewardState = new RewardPreviewState(player);
        var nicheRng = CloneRng(player.RunState.Rng.Niche);

        for (var rewardIndex = 0; rewardIndex < rewardCount; rewardIndex++)
        {
            var rewardCards = new List<CardModel>();
            var pools = player.UnlockState.CharacterCardPools
                .Where(pool => !ReferenceEquals(pool, player.Character.CardPool))
                .ToList()
                .StableShuffle(nicheRng)
                .Take(3);

            foreach (var pool in pools)
            {
                var options = new CardCreationOptions(
                        new[] { pool },
                        CardCreationSource.Other,
                        CardRarityOddsType.RegularEncounter)
                    .WithFlags(CardCreationFlags.NoCardPoolModifications);
                var card = PeekRewardCards(player, rewardState, options, 1).FirstOrDefault();
                if (card is not null)
                {
                    rewardCards.Add(card);
                }
            }

            if (rewardCards.Count > 0)
            {
                rewards.Add(rewardCards);
            }
        }

        return rewards;
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PeekGlassEyeRewards(Player player)
    {
        var rewards = new List<IReadOnlyList<CardModel>>();
        var rewardState = new RewardPreviewState(player);
        var rarities = new[]
        {
            CardRarity.Common,
            CardRarity.Common,
            CardRarity.Uncommon,
            CardRarity.Uncommon,
            CardRarity.Rare
        };

        foreach (var rarity in rarities)
        {
            var options = CardCreationOptions
                .ForNonCombatWithUniformOdds(new[] { player.Character.CardPool }, card => card.Rarity == rarity)
                .WithFlags(CardCreationFlags.NoUpgradeRoll);
            var cards = PeekRewardCards(player, rewardState, options, 3);
            if (cards.Count > 0)
            {
                rewards.Add(cards);
            }
        }

        return rewards;
    }

    private static IReadOnlyList<PotionModel> PeekAlchemicalCofferPotions(Player player, int potionCount)
    {
        return PotionFactory.CreateRandomPotionsOutOfCombat(
            player,
            potionCount,
            CloneRng(player.RunState.Rng.CombatPotionGeneration),
            null);
    }

    private static (CharacterModel Character, IReadOnlyList<CardModel> Common, IReadOnlyList<CardModel> Uncommon, IReadOnlyList<CardModel> Rare, IReadOnlyList<CardModel> All) PeekSeaGlassCards(Player player, SeaGlass seaGlass)
    {
        var character = seaGlass.CharacterId is null
            ? ModelDb.Character<Ironclad>()
            : ModelDb.GetById<CharacterModel>(seaGlass.CharacterId);
        var cardsPerRarity = seaGlass.DynamicVars.Cards.IntValue / 3;
        var rewardState = new RewardPreviewState(player);

        var common = PeekSeaGlassCardsByRarity(player, rewardState, character, CardRarity.Common, cardsPerRarity);
        var uncommon = PeekSeaGlassCardsByRarity(player, rewardState, character, CardRarity.Uncommon, cardsPerRarity);
        var rare = PeekSeaGlassCardsByRarity(player, rewardState, character, CardRarity.Rare, cardsPerRarity);
        var all = common.Concat(uncommon).Concat(rare).ToList();

        return (character, common, uncommon, rare, all);
    }

    private static IReadOnlyList<CardModel> PeekSeaGlassCardsByRarity(Player player, RewardPreviewState rewardState, CharacterModel character, CardRarity rarity, int count)
    {
        var options = CardCreationOptions
            .ForNonCombatWithUniformOdds(new[] { character.CardPool }, card => card.Rarity == rarity)
            .WithFlags(CardCreationFlags.NoUpgradeRoll | CardCreationFlags.NoRarityModification);

        return PeekRewardCards(player, rewardState, options, count);
    }

    private static IReadOnlyList<RelicModel> PeekToyBoxRelics(Player player, int relicCount)
    {
        var cloneBag = RelicGrabBag.FromSerializable(player.RelicGrabBag.ToSerializable());
        var rewardsRng = CloneRng(player.PlayerRng.Rewards);
        var relics = new List<RelicModel>();

        for (var index = 0; index < relicCount; index++)
        {
            var rarity = RelicFactory.RollRarity(rewardsRng);
            var relic = cloneBag.PullFromFront(rarity, player.RunState)?.ToMutable();
            if (relic is null)
            {
                break;
            }

            relic.IsWax = true;
            relics.Add(relic);
        }

        return relics;
    }

    private static IReadOnlyList<RelicModel> PeekNeowsBonesRelics(Player player, NeowsBones neowsBones, int relicCount)
    {
        var validRelics = ModelDb.Event<Neow>()
            .AllPossibleOptions
            .Where(option => option.Relic is not null &&
                             option.Relic.IsAllowedAtNeow(player) &&
                             option.Relic is not NeowsBones)
            .Select(option => option.Relic)
            .OfType<RelicModel>()
            .ToList();
        CloneRng(player.PlayerRng.Rewards).Shuffle(validRelics);
        return validRelics.Take(relicCount).ToList();
    }

    private static IReadOnlyList<CardModel> PeekNeowsBonesCurses(Player player, int curseCount)
    {
        return PeekRandomGeneratedCurses(player, curseCount);
    }

    private static IReadOnlyList<CardModel> PeekRandomGeneratedCurses(Player player, int curseCount)
    {
        var availableCurses = ModelDb.CardPool<CurseCardPool>()
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(card => card.CanBeGeneratedByModifiers)
            .OrderBy(card => card.Id)
            .ToList();
        var nicheRng = CloneRng(player.RunState.Rng.Niche);
        var curses = new List<CardModel>();

        for (var index = 0; index < curseCount && availableCurses.Count > 0; index++)
        {
            var curse = nicheRng.NextItem(availableCurses);
            if (curse is null)
            {
                break;
            }

            availableCurses.Remove(curse);
            curses.Add(player.RunState.CreateCard(curse, player));
        }

        return curses;
    }

    private static IReadOnlyList<(CardModel Source, CardModel Target)> PeekPandorasBoxTransformations(Player player)
    {
        var rng = CloneRng(player.RunState.Rng.Niche);
        return PileType.Deck.GetPile(player).Cards
            .Where(card => card is not null && card.IsBasicStrikeOrDefend && card.IsRemovable)
            .Select(card => (Source: card, Target: CardFactory.CreateRandomCardForTransform(card, isInCombat: false, rng)))
            .Where(item => item.Target is not null)
            .Select(item => (item.Source, item.Target!))
            .ToList();
    }

    private static IReadOnlyList<(CardModel Source, IReadOnlyList<CardModel> Targets)> PeekAstrolabeTransformOutcomes(Player player, int selectionCount)
    {
        var candidates = BuildTransformSelectionCandidates(player);
        if (candidates.Count == 0 || selectionCount <= 0)
        {
            return Array.Empty<(CardModel Source, IReadOnlyList<CardModel> Targets)>();
        }

        selectionCount = Math.Min(selectionCount, candidates.Count);
        var targetsByCandidate = candidates
            .Select(_ => new List<CardModel>())
            .ToList();
        var selected = new List<int>(selectionCount);
        var used = new bool[candidates.Count];

        SimulateOrderedTransformSelections(candidates, player.RunState.Rng.Niche, selectionCount, selected, used, targetsByCandidate, upgradeTargets: true);

        return candidates
            .Select((source, index) => (Source: source, Targets: (IReadOnlyList<CardModel>)DeduplicateCardsByTitle(targetsByCandidate[index])))
            .Where(item => item.Targets.Count > 0)
            .ToList();
    }

    private static void SimulateOrderedTransformSelections(
        IReadOnlyList<CardModel> candidates,
        Rng rng,
        int selectionCount,
        List<int> selected,
        bool[] used,
        IReadOnlyList<List<CardModel>> targetsByCandidate,
        bool upgradeTargets)
    {
        if (selected.Count == selectionCount)
        {
            var clonedRng = CloneRng(rng);
            foreach (var candidateIndex in selected)
            {
                var target = CardFactory.CreateRandomCardForTransform(candidates[candidateIndex], isInCombat: false, clonedRng);
                if (target is null)
                {
                    continue;
                }

                if (upgradeTargets)
                {
                    CardCmd.Upgrade(target);
                }

                targetsByCandidate[candidateIndex].Add(target);
            }

            return;
        }

        for (var index = 0; index < candidates.Count; index++)
        {
            if (used[index])
            {
                continue;
            }

            used[index] = true;
            selected.Add(index);
            SimulateOrderedTransformSelections(candidates, rng, selectionCount, selected, used, targetsByCandidate, upgradeTargets);
            selected.RemoveAt(selected.Count - 1);
            used[index] = false;
        }
    }

    private static IReadOnlyList<CardModel> DeduplicateCardsByTitle(IEnumerable<CardModel> cards)
    {
        return cards
            .GroupBy(CardTitle, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static IReadOnlyList<RelicModel> PeekCallingBellRelics(Player player, int relicCount)
    {
        var rarities = new[] { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare };
        var cloneBag = RelicGrabBag.FromSerializable(player.RelicGrabBag.ToSerializable());
        var relics = new List<RelicModel>();

        foreach (var rarity in rarities.Take(relicCount))
        {
            var relic = cloneBag.PullFromFront(rarity, player.RunState);
            if (relic is not null)
            {
                relics.Add(relic);
            }
        }

        return relics;
    }

    private static IReadOnlyList<(int DecipherCount, int MaxHpLoss, bool UpgradesAll, IReadOnlyList<CardModel> Cards)> PeekTabletOfTruthUpgradeOrder(TabletOfTruth tablet)
    {
        var owner = tablet.Owner!;
        var rng = CloneRng(tablet.Rng);
        var virtualUpgraded = new HashSet<CardModel>();
        var outcomes = new List<(int DecipherCount, int MaxHpLoss, bool UpgradesAll, IReadOnlyList<CardModel> Cards)>();
        var currentCount = Math.Clamp(GetTabletOfTruthDecipherCount(tablet), 1, 4);

        for (var decipherCount = currentCount; decipherCount <= 4; decipherCount++)
        {
            var upgradable = owner.Deck.Cards
                .Where(card => card?.IsUpgradable == true && !virtualUpgraded.Contains(card))
                .ToList();
            var maxHpLoss = GetTabletOfTruthDecipherCost(owner, decipherCount);

            if (decipherCount == 4)
            {
                foreach (var card in upgradable)
                {
                    virtualUpgraded.Add(card);
                }

                outcomes.Add((decipherCount, maxHpLoss, true, (IReadOnlyList<CardModel>)upgradable));
                continue;
            }

            var upgraded = rng.NextItem(upgradable);
            IReadOnlyList<CardModel> cards = upgraded is null ? Array.Empty<CardModel>() : new[] { upgraded };
            if (upgraded is not null)
            {
                virtualUpgraded.Add(upgraded);
            }

            outcomes.Add((decipherCount, maxHpLoss, false, cards));
        }

        return outcomes;
    }

    private static int GetTabletOfTruthDecipherCount(TabletOfTruth tablet)
    {
        var count = TabletOfTruthDecipherCountRef(tablet);
        return count <= 0 ? 1 : count;
    }

    private static int GetTabletOfTruthDecipherCost(Player player, int decipherCount)
    {
        return decipherCount switch
        {
            1 => 6,
            2 => 12,
            3 => 24,
            4 => Math.Max(0, player.Creature.MaxHp - 1),
            _ => 0
        };
    }

    private static (IReadOnlyList<RelicModel> Relics, IReadOnlyList<PotionModel> Potions, IReadOnlyList<CardModel> Cards) PeekPunchOffFightRewards(Player player)
    {
        var rewardsRng = CloneRng(player.PlayerRng.Rewards);
        var cloneBag = RelicGrabBag.FromSerializable(player.RelicGrabBag.ToSerializable());
        var relics = PullPreviewRelics(player, cloneBag, rewardsRng, 1);
        var potions = CreatePreviewPotions(player, rewardsRng, 1);
        var rewardState = new RewardPreviewState(player, rewardsRng);
        var options = new CardCreationOptions(
            new[] { player.Character.CardPool },
            CardCreationSource.Encounter,
            CardRarityOddsType.RegularEncounter);
        var cards = PeekRewardCards(player, rewardState, options, 3);

        return (relics, potions, cards);
    }

    private static (IReadOnlyList<PotionModel> Potions, IReadOnlyList<RelicModel> Relics) PeekPotionThenRelicRewards(Player player, int potionCount, int relicCount)
    {
        var rewardsRng = CloneRng(player.PlayerRng.Rewards);
        var potions = CreatePreviewPotions(player, rewardsRng, potionCount);
        var cloneBag = RelicGrabBag.FromSerializable(player.RelicGrabBag.ToSerializable());
        var relics = PullPreviewRelics(player, cloneBag, rewardsRng, relicCount);
        return (potions, relics);
    }

    private static IReadOnlyList<PotionModel> PeekPhialHolsterPotions(Player player, int potionCount)
    {
        return PotionFactory.CreateRandomPotionsOutOfCombat(
            player,
            potionCount,
            CloneRng(player.RunState.Rng.CombatPotionGeneration),
            null);
    }

    private static IReadOnlyList<PotionModel> PeekOutOfCombatPotions(Player player, int potionCount)
    {
        return CreatePreviewPotions(player, CloneRng(player.PlayerRng.Rewards), potionCount);
    }

    private static IReadOnlyList<PotionModel> CreatePreviewPotions(Player player, Rng rng, int potionCount)
    {
        var potions = new List<PotionModel>();
        for (var index = 0; index < potionCount; index++)
        {
            var potion = PotionFactory.CreateRandomPotionOutOfCombat(player, rng, null);
            if (potion is null)
            {
                break;
            }

            potions.Add(potion);
        }

        return potions;
    }

    private static IReadOnlyList<RelicModel> PullPreviewRelics(Player player, RelicGrabBag cloneBag, Rng rng, int relicCount)
    {
        var relics = new List<RelicModel>();
        for (var index = 0; index < relicCount; index++)
        {
            var rarity = RelicFactory.RollRarity(rng);
            var relic = cloneBag.PullFromFront(rarity, player.RunState);
            if (relic is null)
            {
                break;
            }

            relics.Add(relic);
        }

        return relics;
    }

    private static string BuildPotionRelicRewardLine(IReadOnlyList<PotionModel> potions, IReadOnlyList<RelicModel> relics)
    {
        if (potions.Count == 0 && relics.Count == 0)
        {
            return "然后会给 2 瓶药水和 2 件遗物。";
        }

        var potionText = potions.Count == 0 ? "2 瓶药水" : JoinPotions(potions);
        var relicText = relics.Count == 0 ? "2 件遗物" : JoinRelics(relics);
        return $"然后会给 {potionText} 和 {relicText}。";
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
            rolled = GetNextHighestRarityOrNone(rolled);
        }

        return rolled;
    }

    private static CardRarity GetNextHighestRarityOrNone(CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Basic => CardRarity.Common,
            CardRarity.Common => CardRarity.Uncommon,
            CardRarity.Uncommon => CardRarity.Rare,
            _ => CardRarity.None
        };
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

    private static void LogPreviewStep(EventModel eventModel, string step)
    {
        MainFile.LogInfo($"preview/event id={CleanLogValue(eventModel.Id.Entry)} type={eventModel.GetType().Name} step={step}");
    }

    private static void LogOptionPreviews(EventModel eventModel, string phase, IReadOnlyList<EventOptionPreview> previews)
    {
        for (var index = 0; index < previews.Count; index++)
        {
            LogOptionPreview(eventModel, phase, index, previews[index]);
        }
    }

    private static void LogOptionPreview(EventModel eventModel, string phase, int index, EventOptionPreview preview)
    {
        var lines = preview.Lines.Count == 0
            ? "<none>"
            : string.Join(" | ", preview.Lines.Select(line => CleanLogValue(line, 160)));
        var entities = preview.Entities.Count == 0
            ? "<none>"
            : string.Join(", ", preview.Entities.Select(entity => CleanLogValue(entity.Label, 80)));

        MainFile.LogInfo(
            $"preview/option event={CleanLogValue(eventModel.Id.Entry)} phase={phase} index={index} " +
            $"key=\"{CleanLogValue(preview.SourceOption.TextKey)}\" title=\"{CleanLogValue(preview.Title)}\" " +
            $"locked={preview.SourceOption.IsLocked} coverage={preview.Coverage} " +
            $"lines=\"{CleanLogValue(lines, 700)}\" entities=\"{CleanLogValue(entities, 400)}\"");
    }

    private static string CleanLogValue(string? value, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        return cleaned.Length <= maxLength
            ? cleaned
            : $"{cleaned[..Math.Max(0, maxLength - 3)]}...";
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

    private static bool TryGetIntVar(EventModel eventModel, string name, out int value)
    {
        value = 0;
        if (!eventModel.DynamicVars.TryGetValue(name, out var dynamicVar) || dynamicVar is not IntVar intVar)
        {
            return false;
        }

        value = intVar.IntValue;
        return true;
    }

    private static int GetIntVarOrDefault(RelicModel relic, string name, int defaultValue)
    {
        if (!relic.DynamicVars.TryGetValue(name, out var dynamicVar) || dynamicVar is not IntVar intVar || intVar.IntValue <= 0)
        {
            return defaultValue;
        }

        return intVar.IntValue;
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

    private static string JoinPotions(IEnumerable<PotionModel> potions)
    {
        return JoinTitles(potions.Select(PotionTitle));
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
