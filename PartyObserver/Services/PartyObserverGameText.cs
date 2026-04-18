using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace PartyObserver.Services;

internal static class PartyObserverGameText
{
    public static string ResolveCardTitle(CardModel card)
    {
        return !string.IsNullOrWhiteSpace(card.Title)
            ? card.Title
            : ResolveLocString(card.TitleLocString, card.DynamicVars);
    }

    public static string ResolveCardDescription(CardModel card)
    {
        try
        {
            return card.GetDescriptionForPile(PileType.None);
        }
        catch
        {
            return ResolveLocString(card.Description, card.DynamicVars);
        }
    }

    public static string ResolveCardImagePath(CardModel card)
    {
        return FirstNonEmpty(
            card.PortraitPath,
            card.AllPortraitPaths?.FirstOrDefault(static path => !string.IsNullOrWhiteSpace(path))) ?? string.Empty;
    }

    public static string ResolveRelicTitle(RelicModel relic)
    {
        return ResolveLocString(relic.Title, relic.DynamicVars);
    }

    public static string ResolveRelicDescription(RelicModel relic)
    {
        return FirstNonEmpty(
                   ResolveLocString(relic.DynamicDescription, relic.DynamicVars),
                   ResolveLocString(TryGetRelicLocString(relic, "Description"), relic.DynamicVars),
                   ResolveLocString(relic.DynamicEventDescription, relic.DynamicVars),
                   ResolveLocString(TryGetRelicLocString(relic, "EventDescription"), relic.DynamicVars))
               ?? string.Empty;
    }

    public static string ResolveRelicImagePath(RelicModel relic)
    {
        return FirstNonEmpty(
            relic.IconPath,
            relic.PackedIconPath) ?? string.Empty;
    }

    public static string ResolvePotionTitle(PotionModel potion)
    {
        return ResolveLocString(potion.Title, potion.DynamicVars);
    }

    public static string ResolvePotionDescription(PotionModel potion)
    {
        return ResolveLocString(potion.DynamicDescription, potion.DynamicVars);
    }

    public static string ResolvePotionImagePath(PotionModel potion)
    {
        return FirstNonEmpty(
            potion.ImagePath,
            potion.OutlinePath) ?? string.Empty;
    }

    public static string ResolveLocString(LocString? locString, DynamicVarSet? dynamicVars = null)
    {
        if (locString is null || LocString.IsNullOrWhitespace(locString))
        {
            return string.Empty;
        }

        var variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (locString.Variables is not null)
        {
            foreach (var pair in locString.Variables)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null)
                {
                    variables[pair.Key] = pair.Value;
                }
            }
        }

        if (dynamicVars is not null)
        {
            foreach (var pair in dynamicVars)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    variables[pair.Key] = pair.Value;
                }
            }
        }

        if (LocManager.Instance is not null)
        {
            try
            {
                if (variables.Count > 0)
                {
                    return LocManager.Instance.SmartFormat(locString, variables);
                }
            }
            catch
            {
            }
        }

        try
        {
            return locString.GetFormattedText();
        }
        catch
        {
            try
            {
                return locString.GetRawText();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static LocString? TryGetRelicLocString(RelicModel relic, string propertyName)
    {
        for (var type = relic.GetType(); type is not null; type = type.BaseType)
        {
            try
            {
                var property = type.GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property?.GetValue(relic) is LocString locString)
                {
                    return locString;
                }
            }
            catch
            {
            }
        }

        return null;
    }
}
