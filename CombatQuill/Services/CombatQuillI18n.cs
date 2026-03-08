using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace CombatQuill.Services;

internal static class CombatQuillI18n
{
    private const string DefaultLanguage = "en";

    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    private static Dictionary<string, string> _translations = new(Comparer);

    private static string? _loadedLanguage;

    private static bool _subscribed;

    public static event Action? Changed;

    public static void Initialize()
    {
        EnsureLoaded();
        TrySubscribe();
    }

    public static string Get(string key, string fallback)
    {
        EnsureLoaded();
        return _translations.GetValueOrDefault(key) ?? fallback;
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
        _loadedLanguage = null;
        EnsureLoaded(force: true);
        Changed?.Invoke();
    }

    private static void EnsureLoaded(bool force = false)
    {
        var language = ResolveLanguage();
        if (!force && string.Equals(_loadedLanguage, language, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _translations = LoadTranslations(language);
        _loadedLanguage = language;
    }

    private static Dictionary<string, string> LoadTranslations(string language)
    {
        foreach (var candidate in GetLanguageCandidates(language))
        {
            var path = $"res://CombatQuill/localization/{candidate}.json";
            var translations = TryLoadFromPck(path);
            if (translations is { Count: > 0 })
            {
                return translations;
            }
        }

        return new Dictionary<string, string>(Comparer);
    }

    private static Dictionary<string, string>? TryLoadFromPck(string path)
    {
        if (!Godot.FileAccess.FileExists(path))
        {
            return null;
        }

        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(file.GetAsText()) ?? new Dictionary<string, string>(Comparer);
        }
        catch (JsonException exception)
        {
            GD.PrintErr($"{MainFile.ModId}: failed to parse localization '{path}': {exception}");
            return null;
        }
    }

    private static string ResolveLanguage()
    {
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

        return NormalizeLanguageCode(language);
    }

    private static IEnumerable<string> GetLanguageCandidates(string language)
    {
        var seen = new HashSet<string>(Comparer);
        if (seen.Add(language))
        {
            yield return language;
        }

        var separatorIndex = language.IndexOf('_');
        if (separatorIndex > 0)
        {
            var shortLanguage = language[..separatorIndex];
            if (seen.Add(shortLanguage))
            {
                yield return shortLanguage;
            }
        }

        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase) && seen.Add("zhs"))
        {
            yield return "zhs";
        }

        if (seen.Add(DefaultLanguage))
        {
            yield return DefaultLanguage;
        }
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
