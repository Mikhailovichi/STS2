using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace RandomVision.Services;

internal static class RandomVisionI18n
{
    private const string DefaultLanguage = "en";
    private static readonly Regex MultiSpaceRegex = new(@"\s{2,}", RegexOptions.Compiled);
    private static string? _language;
    private static bool _subscribed;

    public static void Initialize()
    {
        EnsureLanguage();
        TrySubscribe();
    }

    public static bool IsChinese()
    {
        EnsureLanguage();
        return string.Equals(_language, "zhs", StringComparison.OrdinalIgnoreCase);
    }

    public static string Pick(string english, string chinese)
    {
        return IsChinese() ? chinese : english;
    }

    public static string LocalizeToken(string token)
    {
        return token.ToLowerInvariant() switch
        {
            "colorless" => Pick("Colorless", "无色"),
            "ironclad" => Pick("Ironclad", "铁甲战士"),
            "silent" => Pick("Silent", "寂静猎手"),
            "defect" => Pick("Defect", "机器人"),
            "regent" => Pick("Regent", "摄政"),
            "necrobinder" => Pick("Necrobinder", "缚灵师"),
            "attack" => Pick("Attack", "攻击"),
            "skill" => Pick("Skill", "技能"),
            "power" => Pick("Power", "能力"),
            "common" => Pick("Common", "普通"),
            "uncommon" => Pick("Uncommon", "非凡"),
            "rare" => Pick("Rare", "稀有"),
            _ => token
        };
    }

    public static string LocalizeGeneratedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || IsChinese())
        {
            return text;
        }

        string result = text.Trim();

        result = ReplaceExact(result, "当前不可选。", "Option is currently unavailable.");
        result = ReplaceExact(result, "会先进入拒绝页。", "This opens the rejection page first.");
        result = ReplaceExact(result, "下一步可重新接受审判，或双倍下注直接弃局。", "On the next page, you can accept again or double down and abandon the run.");
        result = ReplaceExact(result, "之后会出现 2 组选卡奖励。", "Then 2 card reward selections will appear.");
        result = ReplaceExact(result, "会直接进入战斗。", "This immediately starts combat.");
        result = ReplaceExact(result, "当前没有可升级的牌。", "There are no upgradable cards right now.");
        result = ReplaceExact(result, "当前没有可交易的遗物。", "There are no tradable relics right now.");
        result = ReplaceExact(result, "当前没有可变化的牌。", "There are no changeable cards right now.");
        result = ReplaceExact(result, "当前没有可转化的牌。", "There are no transformable cards right now.");
        result = ReplaceExact(result, "当前无法安全预览转化结果。", "Unable to safely preview the transformation result right now.");
        result = ReplaceExact(result, "之后仍可继续坚持，或直接跨桥。", "You can keep holding on, or cross the bridge immediately.");
        result = ReplaceExact(result, "之后会获得 2 瓶药水。", "Then you will receive 2 potions.");
        result = ReplaceExact(result, "会先进入下一页。", "This opens the next page first.");
        result = ReplaceExact(result, "下一页只能继续打架。", "The next page only lets you continue the fight.");
        result = ReplaceExact(result, "然后会进入下一层奖励页。", "Then it proceeds to the next reward layer.");
        result = ReplaceExact(result, "每第 5 次抓取都会出现。", "This appears every 5th grab.");
        result = ReplaceExact(result, "这次不会花钱。", "This one does not cost gold.");
        result = ReplaceExact(result, "并回满生命。", "and fully heals you.");
        result = ReplaceExact(result, "当前结果已经完全可确定。", "The result is fully deterministic right now.");
        result = ReplaceExact(result, "还需要进一步选择后才能完全确定。", "More choices are needed before the full result becomes deterministic.");
        result = ReplaceExact(result, "本页结果已公开。", "This page already shows its result.");

        result = RegexReplace(result, @"^直接弃掉本次 run。$", "Abandon the current run.");
        result = RegexReplace(result, @"^获得 (?<item>.+) 金币。$", "Gain ${item} gold.");
        result = RegexReplace(result, @"^再获得 (?<item>.+) 金币。$", "Then gain ${item} gold.");
        result = RegexReplace(result, @"^花费 (?<item>.+) 金币。$", "Spend ${item} gold.");
        result = RegexReplace(result, @"^失去 (?<item>.+) 点生命。$", "Lose ${item} HP.");
        result = RegexReplace(result, @"^先失去 (?<item>.+) 点生命。$", "First lose ${item} HP.");
        result = RegexReplace(result, @"^再失去 (?<item>.+) 点生命。$", "Then lose ${item} HP.");
        result = RegexReplace(result, @"^回复 (?<item>.+) 点生命。$", "Recover ${item} HP.");
        result = RegexReplace(result, @"^增加 (?<item>.+) 点最大生命。$", "Gain ${item} max HP.");
        result = RegexReplace(result, @"^失去 (?<item>.+) 点最大生命。$", "Lose ${item} max HP.");
        result = RegexReplace(result, @"^先失去 (?<item>.+) 点最大生命。$", "First lose ${item} max HP.");
        result = RegexReplace(result, @"^获得 (?<item>.+)。$", "Gain ${item}.");
        result = RegexReplace(result, @"^再获得 (?<item>.+)。$", "Then gain ${item}.");
        result = RegexReplace(result, @"^会获得 (?<item>.+)。$", "You will gain ${item}.");
        result = RegexReplace(result, @"^会拿到 (?<item>.+)。$", "You will gain ${item}.");
        result = RegexReplace(result, @"^然后获得 (?<item>.+)。$", "Then gain ${item}.");
        result = RegexReplace(result, @"^然后获得下一件遗物。$", "Then gain the next relic.");
        result = RegexReplace(result, @"^随后获得 1 件遗物。$", "Then gain 1 relic.");
        result = RegexReplace(result, @"^随后获得 2 件遗物。$", "Then gain 2 relics.");
        result = RegexReplace(result, @"^交出 (?<item>.+)。$", "Give up ${item}.");
        result = RegexReplace(result, @"^换成 (?<item>.+)。$", "Trade it for ${item}.");
        result = RegexReplace(result, @"^喝掉 (?<item>.+)。$", "Drink ${item}.");
        result = RegexReplace(result, @"^加入 (?<item>.+)。$", "Add ${item}.");
        result = RegexReplace(result, @"^再加入 (?<item>.+)。$", "Then add ${item}.");
        result = RegexReplace(result, @"^复制整副牌。$", "Duplicate your entire deck.");
        result = RegexReplace(result, @"^升级 (?<item>.+)。$", "Upgrade ${item}.");
        result = RegexReplace(result, @"^会升级 (?<item>.+)。$", "This upgrades ${item}.");
        result = RegexReplace(result, @"^当前料理：升级 (?<item>.+)。$", "Current dish: upgrade ${item}.");
        result = RegexReplace(result, @"^降级 (?<item>.+)。$", "Downgrade ${item}.");
        result = RegexReplace(result, @"^离开时会降级 (?<item>.+)。$", "Leaving will downgrade ${item}.");
        result = RegexReplace(result, @"^离开时不会降级任何牌。$", "Leaving will not downgrade any card.");
        result = RegexReplace(result, @"^还需要先选择 (?<count>.+) 张牌。$", "Choose ${count} card(s) first.");
        result = RegexReplace(result, @"^还需要先选择 (?<count>.+) 张牌移除。$", "Choose ${count} card(s) to remove first.");
        result = RegexReplace(result, @"^还需要再选择 (?<count>.+) 张牌进行升级。$", "You still need to choose ${count} card(s) to upgrade.");
        result = RegexReplace(result, @"^还需要再选择 (?<count>.+) 张牌移除。$", "You still need to choose ${count} card(s) to remove.");
        result = RegexReplace(result, @"^还需要选择 (?<count>.+) 张牌。$", "Choose ${count} card(s).");
        result = RegexReplace(result, @"^还需要选择 (?<count>.+) 张牌，附加 (?<target>.+)。$", "Choose ${count} card(s), then apply ${target}.");
        result = RegexReplace(result, @"^还需要先选择 (?<count>.+) 张可附魔的牌。$", "Choose ${count} enchantable card(s) first.");
        result = RegexReplace(result, @"^还需要先选择 (?<count>.+) 张可移除的基础攻击牌。$", "Choose ${count} removable basic attack card(s) first.");
        result = RegexReplace(result, @"^还需要先选择 (?<count>.+) 张可移除的基础防御牌。$", "Choose ${count} removable basic defend card(s) first.");
        result = RegexReplace(result, @"^还需要选择 (?<count>.+) 张可转化的基础牌。$", "Choose ${count} transformable basic card(s).");
        result = RegexReplace(result, @"^还需要先选择 (?<count>.+) 张可转化的基础牌。$", "Choose ${count} transformable basic card(s) first.");
        result = RegexReplace(result, @"^还需要先选择 (?<count>.+) 张可转化牌。$", "Choose ${count} transformable card(s) first.");
        result = RegexReplace(result, @"^选定后会附加 (?<item>.+)。$", "The chosen card will receive ${item}.");
        result = RegexReplace(result, @"^选定后会升级该牌。$", "The chosen card will be upgraded.");
        result = RegexReplace(result, @"^选定后会按当前种子进行转化。$", "The chosen card will transform according to the current seed.");
        result = RegexReplace(result, @"^选定后会直接变成 (?<item>.+)。$", "The chosen card will turn directly into ${item}.");
        result = RegexReplace(result, @"^会出现 (?<count>.+) 张牌供你选 (?<pick>.+) 张。$", "${count} cards will be offered; choose ${pick}.");
        result = RegexReplace(result, @"^会出现 (?<count>.+) 张普通牌供你选 (?<pick>.+) 张。$", "${count} common cards will be offered; choose ${pick}.");
        result = RegexReplace(result, @"^之后会出现 (?<count>.+) 张无色牌：(?<items>.+)。$", "Then ${count} colorless cards will appear: ${items}.");
        result = RegexReplace(result, @"^之后会出现 (?<count>.+) 张无色牌供你选择。$", "Then ${count} colorless cards will appear for selection.");
        result = RegexReplace(result, @"^实际候选：(?<items>.+)。$", "Actual candidates: ${items}.");
        result = RegexReplace(result, @"^普通候选：(?<items>.+)。$", "Common candidates: ${items}.");
        result = RegexReplace(result, @"^非凡候选：(?<items>.+)。$", "Uncommon candidates: ${items}.");
        result = RegexReplace(result, @"^稀有候选：(?<items>.+)。$", "Rare candidates: ${items}.");
        result = RegexReplace(result, @"^当前料理：回复 (?<item>.+) 点生命。$", "Current dish: recover ${item} HP.");
        result = RegexReplace(result, @"^当前料理：增加 (?<item>.+) 点最大生命。$", "Current dish: gain ${item} max HP.");
        result = RegexReplace(result, @"^当前料理：获得 (?<item>.+)。$", "Current dish: gain ${item}.");
        result = RegexReplace(result, @"^当前料理：加入 (?<item>.+)。$", "Current dish: add ${item}.");
        result = RegexReplace(result, @"^当前料理：不会升级任何牌。$", "Current dish: no card will be upgraded.");
        result = RegexReplace(result, @"^不会升级任何牌。$", "No card will be upgraded.");
        result = RegexReplace(result, @"^会先加入 (?<item>.+)。$", "This first adds ${item}.");
        result = RegexReplace(result, @"^移除所有 (?<item>.+)。$", "Remove all ${item}.");
        result = RegexReplace(result, @"^然后会给 2 瓶药水和 2 件遗物。$", "Then it grants 2 potions and 2 relics.");
        result = RegexReplace(result, @"^然后会给 2 瓶药水和 (?<items>.+)。$", "Then it grants 2 potions and ${items}.");
        result = RegexReplace(result, @"^会出现 (?<count>.+) 瓶 (?<item>.+)。$", "${count} copies of ${item} will be offered.");
        result = RegexReplace(result, @"^无论选哪张可转化牌，都会变成 (?<item>.+)。$", "No matter which transformable card you choose, it will become ${item}.");
        result = RegexReplace(result, @"^可选牌：(?<items>.+)。$", "Available cards: ${items}.");
        result = RegexReplace(result, @"^若选 (?<from>.+) -> (?<to>.+)。$", "If you choose ${from} -> ${to}.");
        result = RegexReplace(result, @"^第 1 张无论选哪张，都会先变成 (?<item>.+)。$", "No matter which first card you choose, it will become ${item} first.");
        result = ReplaceExact(result, "第 1 张的转化结果取决于你先选哪张牌：", "The first transformation depends on which card you choose first:");
        result = ReplaceExact(result, "后续张数会受前一张选择和随机数消耗顺序影响。", "Later cards still depend on earlier picks and RNG consumption order.");

        result = ApplyFallbackEnglishFragments(result);
        return MultiSpaceRegex.Replace(result, " ").Trim();
    }

    private static string ReplaceExact(string source, string zh, string en)
    {
        return string.Equals(source, zh, StringComparison.Ordinal) ? en : source;
    }

    private static string RegexReplace(string source, string pattern, string replacement)
    {
        return Regex.Replace(source, pattern, replacement, RegexOptions.CultureInvariant);
    }

    private static string ApplyFallbackEnglishFragments(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !ContainsCjk(text))
        {
            return text;
        }

        string result = text;
        result = result.Replace("当前料理：", "Current dish: ", StringComparison.Ordinal);
        result = result.Replace("当前", "Current ", StringComparison.Ordinal);
        result = result.Replace("会先进入", "First enters ", StringComparison.Ordinal);
        result = result.Replace("下一页", "the next page", StringComparison.Ordinal);
        result = result.Replace("确定选项", "deterministic options", StringComparison.Ordinal);
        result = result.Replace("类型选项", "type options", StringComparison.Ordinal);
        result = result.Replace("按顺序出现全部娃娃", "show all dolls in order", StringComparison.Ordinal);
        result = result.Replace("之后仍可继续坚持，或直接跨桥。", "You can keep holding on, or cross the bridge immediately.", StringComparison.Ordinal);
        result = result.Replace("之后会出现", "Then ", StringComparison.Ordinal);
        result = result.Replace("之后会加入", "Then add ", StringComparison.Ordinal);
        result = result.Replace("之后会获得", "Then gain ", StringComparison.Ordinal);
        result = result.Replace("之后会给", "Then grant ", StringComparison.Ordinal);
        result = result.Replace("然后会进入", "Then enter ", StringComparison.Ordinal);
        result = result.Replace("然后获得", "Then gain ", StringComparison.Ordinal);
        result = result.Replace("然后还需要选择", "Then choose ", StringComparison.Ordinal);
        result = result.Replace("随后获得", "Then gain ", StringComparison.Ordinal);
        result = result.Replace("会直接获得", "Gain ", StringComparison.Ordinal);
        result = result.Replace("会获得", "Gain ", StringComparison.Ordinal);
        result = result.Replace("会拿到", "Gain ", StringComparison.Ordinal);
        result = result.Replace("会加入", "Add ", StringComparison.Ordinal);
        result = result.Replace("会升级", "Upgrade ", StringComparison.Ordinal);
        result = result.Replace("不会升级任何牌", "no card will be upgraded", StringComparison.Ordinal);
        result = result.Replace("获得诅咒怀疑。", "Gain the Doubt curse.", StringComparison.Ordinal);
        result = result.Replace("获得诅咒羞耻。", "Gain the Shame curse.", StringComparison.Ordinal);
        result = result.Replace("获得诅咒遗憾。", "Gain the Regret curse.", StringComparison.Ordinal);
        result = result.Replace("会进入商人审判。", "This enters the Merchant trial.", StringComparison.Ordinal);
        result = result.Replace("会进入贵族审判。", "This enters the Noble trial.", StringComparison.Ordinal);
        result = result.Replace("会进入无名者审判。", "This enters the Nondescript trial.", StringComparison.Ordinal);
        result = result.Replace("后续有罪：", "If guilty: ", StringComparison.Ordinal);
        result = result.Replace("后续无罪：", "If innocent: ", StringComparison.Ordinal);
        result = result.Replace("回复", "Recover ", StringComparison.Ordinal);
        result = result.Replace("增加", "Gain ", StringComparison.Ordinal);
        result = result.Replace("失去", "Lose ", StringComparison.Ordinal);
        result = result.Replace("先失去", "First lose ", StringComparison.Ordinal);
        result = result.Replace("再失去", "Then lose ", StringComparison.Ordinal);
        result = result.Replace("先承受", "First take ", StringComparison.Ordinal);
        result = result.Replace("交出", "Give up ", StringComparison.Ordinal);
        result = result.Replace("换成", "Trade for ", StringComparison.Ordinal);
        result = result.Replace("喝掉", "Drink ", StringComparison.Ordinal);
        result = result.Replace("加入", "Add ", StringComparison.Ordinal);
        result = result.Replace("再加入", "Then add ", StringComparison.Ordinal);
        result = result.Replace("升级", "Upgrade ", StringComparison.Ordinal);
        result = result.Replace("再升级", "Then upgrade ", StringComparison.Ordinal);
        result = result.Replace("先降级", "First downgrade ", StringComparison.Ordinal);
        result = result.Replace("降级", "Downgrade ", StringComparison.Ordinal);
        result = result.Replace("花费", "Spend ", StringComparison.Ordinal);
        result = result.Replace("再获得", "Then gain ", StringComparison.Ordinal);
        result = result.Replace("获得", "Gain ", StringComparison.Ordinal);
        result = result.Replace("实际候选：", "Actual candidates: ", StringComparison.Ordinal);
        result = result.Replace("普通候选：", "Common candidates: ", StringComparison.Ordinal);
        result = result.Replace("非凡候选：", "Uncommon candidates: ", StringComparison.Ordinal);
        result = result.Replace("稀有候选：", "Rare candidates: ", StringComparison.Ordinal);
        result = result.Replace("可选牌：", "Available cards: ", StringComparison.Ordinal);
        result = result.Replace("无论选哪张可转化牌，都会变成", "No matter which transformable card you choose, it will become", StringComparison.Ordinal);
        result = result.Replace("若先选", "If you choose first ", StringComparison.Ordinal);
        result = result.Replace("若选", "If you choose ", StringComparison.Ordinal);
        result = result.Replace("还需要先选择", "Choose ", StringComparison.Ordinal);
        result = result.Replace("还需要再选择", "Then choose ", StringComparison.Ordinal);
        result = result.Replace("还需要选择", "Choose ", StringComparison.Ordinal);
        result = result.Replace("可附魔的牌", "enchantable card(s)", StringComparison.Ordinal);
        result = result.Replace("可移除的基础攻击牌", "removable basic attack card(s)", StringComparison.Ordinal);
        result = result.Replace("可移除的基础防御牌", "removable basic defend card(s)", StringComparison.Ordinal);
        result = result.Replace("可转化的基础牌", "transformable basic card(s)", StringComparison.Ordinal);
        result = result.Replace("牌移除", " card(s) to remove", StringComparison.Ordinal);
        result = result.Replace("牌进行升级", " card(s) to upgrade", StringComparison.Ordinal);
        result = result.Replace("张牌", " card(s)", StringComparison.Ordinal);
        result = result.Replace("金币", " gold", StringComparison.Ordinal);
        result = result.Replace("点最大生命", " max HP", StringComparison.Ordinal);
        result = result.Replace("点生命", " HP", StringComparison.Ordinal);
        result = result.Replace("点伤害", " damage", StringComparison.Ordinal);
        result = result.Replace("瓶药水", " potions", StringComparison.Ordinal);
        result = result.Replace("瓶非凡药水", " uncommon potion", StringComparison.Ordinal);
        result = result.Replace("瓶", " potion(s)", StringComparison.Ordinal);
        result = result.Replace("件遗物", " relic(s)", StringComparison.Ordinal);
        result = result.Replace("并回满生命。", "and fully heal.", StringComparison.Ordinal);
        result = result.Replace("；超时则无奖励。", "; no reward if the timer expires.", StringComparison.Ordinal);
        result = result.Replace("。", ".", StringComparison.Ordinal);
        result = result.Replace("：", ": ", StringComparison.Ordinal);

        return result;
    }

    private static bool ContainsCjk(string text)
    {
        foreach (var ch in text)
        {
            if (ch >= 0x4E00 && ch <= 0x9FFF)
            {
                return true;
            }
        }

        return false;
    }

    private static void TrySubscribe()
    {
        if (_subscribed)
        {
            return;
        }

        try
        {
            if (LocManager.Instance is not null)
            {
                LocManager.Instance.SubscribeToLocaleChange(OnLocaleChanged);
                _subscribed = true;
            }
        }
        catch (Exception exception)
        {
            GD.PrintErr($"{MainFile.ModId}: failed to subscribe to locale changes: {exception}");
        }
    }

    private static void OnLocaleChanged()
    {
        _language = null;
        EnsureLanguage();
    }

    private static void EnsureLanguage()
    {
        if (!string.IsNullOrWhiteSpace(_language))
        {
            return;
        }

        string? language = null;
        try
        {
            language = LocManager.Instance?.Language;
        }
        catch
        {
        }

        if (string.IsNullOrWhiteSpace(language))
        {
            try
            {
                language = TranslationServer.GetLocale();
            }
            catch
            {
            }
        }

        _language = NormalizeLanguageCode(language);
    }

    private static string NormalizeLanguageCode(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return DefaultLanguage;
        }

        var normalized = language.Trim().Replace('-', '_').ToLowerInvariant();
        return normalized switch
        {
            "zh" or "zh_cn" or "zh_hans" or "zh_sg" => "zhs",
            "en_us" or "en_gb" => "en",
            _ => normalized
        };
    }
}
