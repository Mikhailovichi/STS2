using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace CombatQuill.Services;

internal readonly record struct CombatQuillCharacterContext(string CharacterKey, string DefaultColorHtml);

internal static class CombatQuillCharacterContextResolver
{
    private static readonly string[] CharacterPropertyCandidates =
    [
        "CharacterId",
        "Id",
        "Key",
        "SaveKey",
        "Slug",
        "InternalName",
        "Name",
        "CharacterType",
        "Type"
    ];

    public static bool TryResolveCurrent(out CombatQuillCharacterContext context)
    {
        context = default;

        var runManager = RunManager.Instance;
        if (runManager?.NetService is null)
        {
            return false;
        }

        if (runManager.DebugOnlyGetState() is not IPlayerCollection playerCollection)
        {
            return false;
        }

        var player = playerCollection.GetPlayer(runManager.NetService.NetId);
        if (player is null || player.Character is null)
        {
            return false;
        }

        var characterKey = ResolveCharacterKey(player);
        if (string.IsNullOrWhiteSpace(characterKey))
        {
            return false;
        }

        var defaultColorHtml = player.Character.MapDrawingColor.ToHtml();
        if (defaultColorHtml.Length > 6)
        {
            defaultColorHtml = defaultColorHtml[..6];
        }

        context = new CombatQuillCharacterContext(characterKey.Trim(), defaultColorHtml.TrimStart('#'));
        return true;
    }

    private static string ResolveCharacterKey(Player player)
    {
        var character = player.Character;
        if (character is null)
        {
            return string.Empty;
        }

        foreach (var propertyName in CharacterPropertyCandidates)
        {
            if (TryReadSimpleMember(character, propertyName, out var value))
            {
                return value;
            }
        }

        foreach (var propertyName in CharacterPropertyCandidates)
        {
            if (TryReadNestedMember(character, propertyName, out var value))
            {
                return value;
            }
        }

        return character.GetType().FullName ?? character.GetType().Name;
    }

    private static bool TryReadNestedMember(object source, string targetMemberName, out string value)
    {
        value = string.Empty;
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var nestedMembers = source.GetType()
            .GetMembers(flags)
            .Where(static member => member.MemberType is MemberTypes.Field or MemberTypes.Property);

        foreach (var member in nestedMembers)
        {
            if (!TryGetMemberValue(source, member, out var nestedValue) || nestedValue is null)
            {
                continue;
            }

            if (nestedValue is string or Enum || nestedValue.GetType().IsPrimitive)
            {
                continue;
            }

            if (TryReadSimpleMember(nestedValue, targetMemberName, out value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadSimpleMember(object source, string memberName, out string value)
    {
        value = string.Empty;
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var property = source.GetType().GetProperty(memberName, flags);
        if (property is not null && TryGetMemberValue(source, property, out var propertyValue))
        {
            value = NormalizeIdentifier(propertyValue);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
        }

        var field = source.GetType().GetField(memberName, flags);
        if (field is not null && TryGetMemberValue(source, field, out var fieldValue))
        {
            value = NormalizeIdentifier(fieldValue);
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    private static bool TryGetMemberValue(object source, MemberInfo memberInfo, out object? value)
    {
        value = null;

        try
        {
            value = memberInfo switch
            {
                PropertyInfo property when property.GetIndexParameters().Length == 0 => property.GetValue(source),
                FieldInfo field => field.GetValue(source),
                _ => null
            };
            return value is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeIdentifier(object? rawValue)
    {
        if (rawValue is null)
        {
            return string.Empty;
        }

        var value = rawValue switch
        {
            string text => text,
            Enum enumValue => enumValue.ToString(),
            _ => rawValue.ToString() ?? string.Empty
        };

        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
