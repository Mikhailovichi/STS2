using System.Text.Json;
using Godot;

namespace CombatQuill.Services;

internal static class CombatQuillSettingsStore
{
    private const string SettingsFileName = "combatquill.settings";
    private const string LegacySettingsFileName = "combatquill.settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static CombatQuillSettings? _cached;

    public static CombatQuillSettings Load()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var path = GetSettingsPath();
        MigrateLegacySettings(path);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            if (!File.Exists(path))
            {
                _cached = new CombatQuillSettings();
                Save(_cached);
                return _cached;
            }

            var json = File.ReadAllText(path);
            _cached = JsonSerializer.Deserialize<CombatQuillSettings>(json, JsonOptions) ?? new CombatQuillSettings();
            _cached.Normalize();
        }
        catch (Exception exception)
        {
            GD.PrintErr($"{MainFile.ModId}: failed to load settings, using defaults: {exception}");
            _cached = new CombatQuillSettings();
        }

        return _cached;
    }

    public static void Save(CombatQuillSettings settings)
    {
        settings.Normalize();
        _cached = settings;
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(GetSettingsPath(), json);
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "mods", MainFile.ModId, SettingsFileName);
    }

    private static string GetLegacySettingsPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "mods", MainFile.ModId, LegacySettingsFileName);
    }

    private static void MigrateLegacySettings(string path)
    {
        var legacyPath = GetLegacySettingsPath();
        if (File.Exists(path) || !File.Exists(legacyPath))
        {
            return;
        }

        try
        {
            File.Move(legacyPath, path);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"{MainFile.ModId}: failed to migrate legacy settings file: {exception.Message}");
        }
    }
}
