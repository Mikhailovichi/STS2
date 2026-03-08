using System.Text.Json;
using Godot;

namespace PartyObserver.Services;

internal static class PartyObserverSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static PartyObserverSettings? _cached;

    public static PartyObserverSettings Load()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var path = GetSettingsPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            if (!File.Exists(path))
            {
                _cached = new PartyObserverSettings();
                Save(_cached);
                return _cached;
            }

            var json = File.ReadAllText(path);
            _cached = JsonSerializer.Deserialize<PartyObserverSettings>(json, JsonOptions) ?? new PartyObserverSettings();
            _cached.Normalize();
        }
        catch (Exception exception)
        {
            GD.PrintErr($"{MainFile.ModId}: failed to load settings, using defaults: {exception}");
            _cached = new PartyObserverSettings();
        }

        return _cached;
    }

    public static void Save(PartyObserverSettings settings)
    {
        settings.Normalize();
        _cached = settings;
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(GetSettingsPath(), json);
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "mods", MainFile.ModId, "partyobserver.settings.json");
    }
}
