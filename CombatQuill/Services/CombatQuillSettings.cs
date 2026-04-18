using Godot;

namespace CombatQuill.Services;

internal sealed class CombatQuillCharacterColorProfile
{
    public string CharacterKey { get; set; } = string.Empty;

    public string StrokeColorHtml { get; set; } = "5cc8ff";

    public string[] PaletteHtml { get; set; } = [];

    public int SelectedColorIndex { get; set; }

    public void Normalize(string defaultColorHtml, string[] fallbackPalette)
    {
        CharacterKey = string.IsNullOrWhiteSpace(CharacterKey) ? string.Empty : CharacterKey.Trim();
        PaletteHtml = CreateCharacterPalette(defaultColorHtml, PaletteHtml, fallbackPalette);
        StrokeColorHtml = NormalizeColorHtml(
            string.IsNullOrWhiteSpace(StrokeColorHtml) ? PaletteHtml[0] : StrokeColorHtml,
            fallback: PaletteHtml[0]);
        SelectedColorIndex = ResolveSelectedColorIndex(SelectedColorIndex, StrokeColorHtml, PaletteHtml);
    }

    internal static CombatQuillCharacterColorProfile CreateDefault(string characterKey, string defaultColorHtml, string[] fallbackPalette)
    {
        var profile = new CombatQuillCharacterColorProfile
        {
            CharacterKey = characterKey,
            StrokeColorHtml = NormalizeColorHtml(defaultColorHtml, fallbackPalette.FirstOrDefault() ?? "5cc8ff"),
            PaletteHtml = CreateCharacterPalette(defaultColorHtml, [], fallbackPalette),
            SelectedColorIndex = 0
        };
        profile.Normalize(defaultColorHtml, fallbackPalette);
        return profile;
    }

    internal static string[] CreateCharacterPalette(string defaultColorHtml, string[]? profilePalette, string[] fallbackPalette)
    {
        var orderedColors = new List<string>();
        AddUniqueColor(orderedColors, defaultColorHtml);

        if (profilePalette is not null)
        {
            foreach (var color in profilePalette)
            {
                AddUniqueColor(orderedColors, color);
            }
        }

        foreach (var color in fallbackPalette)
        {
            AddUniqueColor(orderedColors, color);
        }

        if (orderedColors.Count == 0)
        {
            orderedColors.Add("5cc8ff");
        }

        return orderedColors.ToArray();
    }

    internal static int ResolveSelectedColorIndex(int requestedIndex, string strokeColorHtml, string[] palette)
    {
        if (palette.Length == 0)
        {
            return 0;
        }

        var normalizedStrokeColor = NormalizeColorHtml(strokeColorHtml, palette[0]);
        var exactIndex = Array.FindIndex(
            palette,
            color => string.Equals(
                NormalizeColorHtml(color, normalizedStrokeColor),
                normalizedStrokeColor,
                StringComparison.OrdinalIgnoreCase));
        if (exactIndex >= 0)
        {
            return exactIndex;
        }

        return Mathf.Clamp(requestedIndex, 0, palette.Length - 1);
    }

    internal static string NormalizeColorHtml(string? colorHtml, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(colorHtml) ? fallback : colorHtml.Trim().TrimStart('#');
        return string.IsNullOrWhiteSpace(normalized) ? fallback.TrimStart('#') : normalized;
    }

    private static void AddUniqueColor(List<string> colors, string? colorHtml)
    {
        if (string.IsNullOrWhiteSpace(colorHtml))
        {
            return;
        }

        var normalized = colorHtml.Trim().TrimStart('#');
        if (colors.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        colors.Add(normalized);
    }
}

internal enum CombatQuillActivationTriggerMode
{
    Hold,
    Toggle
}

internal sealed class CombatQuillSettings
{
    public bool Enabled { get; set; } = true;

    public bool StartInQuillMode { get; set; }

    public bool ShowStatusPanel { get; set; }

    public bool ShowOwnerLegend { get; set; } = true;

    public bool EnableInRewards { get; set; } = true;

    public string ToggleKey { get; set; } = nameof(Key.F8);

    public string ClearKey { get; set; } = nameof(Key.F9);

    public string EraserKey { get; set; } = nameof(Key.E);

    public string ActivationKey { get; set; } = nameof(Key.Ctrl);

    public string ActivationTriggerMode { get; set; } = nameof(CombatQuillActivationTriggerMode.Hold);

    public string DrawButton { get; set; } = nameof(MouseButton.Right);

    public float StrokeWidth { get; set; } = 6f;

    public float EraserRadius { get; set; } = 28f;

    public float EraserWidth { get; set; } = 24f;

    public string StrokeColorHtml { get; set; } = "5cc8ff";

    public string[] PaletteHtml { get; set; } =
    [
        "5cc8ff",
        "ff7a7a",
        "ffd166",
        "83e8ad",
        "c89bff",
        "ffffff"
    ];

    public int SelectedColorIndex { get; set; }

    public float StrokeOpacity { get; set; } = 0.9f;

    public float PanelOpacity { get; set; } = 0.82f;

    public float ToolbarPositionX { get; set; } = -1f;

    public float ToolbarPositionY { get; set; } = -1f;

    public bool ToolbarLocked { get; set; } = true;

    public List<CombatQuillCharacterColorProfile> CharacterColorProfiles { get; set; } = [];

    public Key GetToggleKey()
    {
        return ParseEnum(ToggleKey, Key.F8);
    }

    public Key GetClearKey()
    {
        return ParseEnum(ClearKey, Key.F9);
    }

    public Key GetEraserKey()
    {
        return ParseEnum(EraserKey, Key.E);
    }

    public Key GetActivationKey()
    {
        return ParseEnum(ActivationKey, Key.Ctrl);
    }

    public CombatQuillActivationTriggerMode GetActivationTriggerMode()
    {
        return ParseEnum(ActivationTriggerMode, CombatQuillActivationTriggerMode.Hold);
    }

    public MouseButton GetDrawButton()
    {
        return ParseEnum(DrawButton, MouseButton.Right);
    }

    public Color GetStrokeColor(float? alphaOverride = null)
    {
        var color = Color.FromHtml($"#{GetSelectedColorHtml().TrimStart('#')}");
        color.A = Mathf.Clamp(alphaOverride ?? StrokeOpacity, 0.1f, 1f);
        return color;
    }

    public float GetStrokeWidth()
    {
        return Mathf.Clamp(StrokeWidth, 2f, 20f);
    }

    public float GetEraserWidth()
    {
        var fallback = EraserWidth > 0f ? EraserWidth : EraserRadius;
        return Mathf.Clamp(fallback, 8f, 72f);
    }

    public string GetSelectedColorHtml()
    {
        var palette = GetPalette();
        var index = Mathf.Clamp(SelectedColorIndex, 0, palette.Length - 1);
        var paletteColor = palette[index];
        return string.IsNullOrWhiteSpace(paletteColor) ? StrokeColorHtml : paletteColor;
    }

    public string[] GetPalette()
    {
        var palette = PaletteHtml?.Where(static color => !string.IsNullOrWhiteSpace(color)).ToArray();
        if (palette is { Length: > 0 })
        {
            return palette;
        }

        return
        [
            string.IsNullOrWhiteSpace(StrokeColorHtml) ? "5cc8ff" : StrokeColorHtml.TrimStart('#')
        ];
    }

    public bool ApplyCharacterColorProfile(string characterKey, string defaultColorHtml)
    {
        var normalizedCharacterKey = NormalizeCharacterKey(characterKey);
        if (string.IsNullOrWhiteSpace(normalizedCharacterKey))
        {
            return false;
        }

        var fallbackPalette = GetPalette();
        var profile = GetOrCreateCharacterColorProfile(normalizedCharacterKey, defaultColorHtml, fallbackPalette, out var created);
        var normalizedStrokeColor = CombatQuillCharacterColorProfile.NormalizeColorHtml(profile.StrokeColorHtml, profile.PaletteHtml[0]);
        var selectedIndex = CombatQuillCharacterColorProfile.ResolveSelectedColorIndex(profile.SelectedColorIndex, normalizedStrokeColor, profile.PaletteHtml);

        var changed =
            !string.Equals(StrokeColorHtml.TrimStart('#'), normalizedStrokeColor, StringComparison.OrdinalIgnoreCase) ||
            SelectedColorIndex != selectedIndex ||
            !PaletteSequenceEquals(PaletteHtml, profile.PaletteHtml);

        StrokeColorHtml = normalizedStrokeColor;
        PaletteHtml = profile.PaletteHtml.ToArray();
        SelectedColorIndex = selectedIndex;

        return changed || created;
    }

    public bool SaveCharacterColorProfile(string characterKey, string defaultColorHtml)
    {
        var normalizedCharacterKey = NormalizeCharacterKey(characterKey);
        if (string.IsNullOrWhiteSpace(normalizedCharacterKey))
        {
            return false;
        }

        var palette = GetPalette();
        var normalizedStrokeColor = CombatQuillCharacterColorProfile.NormalizeColorHtml(GetSelectedColorHtml(), palette[0]);
        var selectedIndex = CombatQuillCharacterColorProfile.ResolveSelectedColorIndex(SelectedColorIndex, normalizedStrokeColor, palette);
        var profile = GetOrCreateCharacterColorProfile(normalizedCharacterKey, defaultColorHtml, palette, out var created);

        var changed =
            created ||
            !string.Equals(profile.StrokeColorHtml, normalizedStrokeColor, StringComparison.OrdinalIgnoreCase) ||
            profile.SelectedColorIndex != selectedIndex ||
            !PaletteSequenceEquals(profile.PaletteHtml, palette);

        profile.StrokeColorHtml = normalizedStrokeColor;
        profile.PaletteHtml = CombatQuillCharacterColorProfile.CreateCharacterPalette(defaultColorHtml, palette, palette);
        profile.SelectedColorIndex = CombatQuillCharacterColorProfile.ResolveSelectedColorIndex(selectedIndex, normalizedStrokeColor, profile.PaletteHtml);

        return changed;
    }

    public void Normalize()
    {
        ToggleKey = string.IsNullOrWhiteSpace(ToggleKey) ? nameof(Key.F8) : ToggleKey;
        ClearKey = string.IsNullOrWhiteSpace(ClearKey) ? nameof(Key.F9) : ClearKey;
        EraserKey = string.IsNullOrWhiteSpace(EraserKey) ? nameof(Key.E) : EraserKey;
        ActivationKey = string.IsNullOrWhiteSpace(ActivationKey) ? nameof(Key.Ctrl) : ActivationKey;
        ActivationTriggerMode = string.IsNullOrWhiteSpace(ActivationTriggerMode)
            ? nameof(CombatQuillActivationTriggerMode.Hold)
            : ActivationTriggerMode;
        DrawButton = string.IsNullOrWhiteSpace(DrawButton) ? nameof(MouseButton.Right) : DrawButton;
        StrokeColorHtml = GetSelectedColorHtml().TrimStart('#');
        StrokeWidth = GetStrokeWidth();
        EraserWidth = GetEraserWidth();
        StrokeOpacity = Mathf.Clamp(StrokeOpacity, 0.15f, 1f);
        PanelOpacity = Mathf.Clamp(PanelOpacity, 0.25f, 1f);
        SelectedColorIndex = Mathf.Clamp(SelectedColorIndex, 0, GetPalette().Length - 1);
        CharacterColorProfiles ??= [];
        CharacterColorProfiles = CharacterColorProfiles
            .Where(static profile => profile is not null)
            .GroupBy(profile => NormalizeCharacterKey(profile.CharacterKey), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .Select(group =>
            {
                var profile = group.First();
                profile.CharacterKey = group.Key;
                profile.Normalize(profile.StrokeColorHtml, GetPalette());
                return profile;
            })
            .ToList();
    }

    private static T ParseEnum<T>(string rawValue, T fallback) where T : struct, Enum
    {
        return Enum.TryParse(rawValue, ignoreCase: true, out T parsedValue) ? parsedValue : fallback;
    }

    private CombatQuillCharacterColorProfile GetOrCreateCharacterColorProfile(
        string characterKey,
        string defaultColorHtml,
        string[] fallbackPalette,
        out bool created)
    {
        CharacterColorProfiles ??= [];
        var existing = CharacterColorProfiles.FirstOrDefault(
            profile => string.Equals(profile.CharacterKey, characterKey, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Normalize(defaultColorHtml, fallbackPalette);
            created = false;
            return existing;
        }

        existing = CombatQuillCharacterColorProfile.CreateDefault(characterKey, defaultColorHtml, fallbackPalette);
        CharacterColorProfiles.Add(existing);
        created = true;
        return existing;
    }

    private static bool PaletteSequenceEquals(string[]? left, string[]? right)
    {
        left ??= [];
        right ??= [];
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!string.Equals(
                    CombatQuillCharacterColorProfile.NormalizeColorHtml(left[index], string.Empty),
                    CombatQuillCharacterColorProfile.NormalizeColorHtml(right[index], string.Empty),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeCharacterKey(string? characterKey)
    {
        return string.IsNullOrWhiteSpace(characterKey) ? string.Empty : characterKey.Trim();
    }
}
