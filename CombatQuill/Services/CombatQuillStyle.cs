using Godot;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CombatQuill.Services;

internal sealed class CombatQuillStyle
{
    public string StrokeColorHtml { get; set; } = "5cc8ff";

    public float StrokeWidth { get; set; } = 6f;

    public float StrokeOpacity { get; set; } = 0.9f;

    public float EraserWidth { get; set; } = 24f;

    public Color GetStrokeColor(Color fallbackColor)
    {
        Color color;
        try
        {
            color = Color.FromHtml($"#{StrokeColorHtml.TrimStart('#')}");
        }
        catch
        {
            color = fallbackColor;
        }

        color.A = Mathf.Clamp(StrokeOpacity, 0.15f, 1f);
        return color;
    }

    public float GetStrokeWidth()
    {
        return Mathf.Clamp(StrokeWidth, 2f, 20f);
    }

    public float GetEraserWidth()
    {
        return Mathf.Clamp(EraserWidth, 8f, 72f);
    }

    public void Normalize()
    {
        StrokeColorHtml = string.IsNullOrWhiteSpace(StrokeColorHtml) ? "5cc8ff" : StrokeColorHtml.TrimStart('#');
        StrokeWidth = GetStrokeWidth();
        StrokeOpacity = Mathf.Clamp(StrokeOpacity, 0.15f, 1f);
        EraserWidth = GetEraserWidth();
    }

    public static CombatQuillStyle FromSettings(CombatQuillSettings settings)
    {
        var style = new CombatQuillStyle
        {
            StrokeColorHtml = settings.GetSelectedColorHtml(),
            StrokeWidth = settings.GetStrokeWidth(),
            StrokeOpacity = Mathf.Clamp(settings.StrokeOpacity, 0.15f, 1f),
            EraserWidth = settings.GetEraserWidth()
        };
        style.Normalize();
        return style;
    }

    public static CombatQuillStyle CreateFallback(Player player)
    {
        var html = player.Character.MapDrawingColor.ToHtml().TrimStart('#');
        if (html.Length > 6)
        {
            html = html[..6];
        }

        var style = new CombatQuillStyle
        {
            StrokeColorHtml = html,
            StrokeWidth = 6f,
            StrokeOpacity = 0.9f,
            EraserWidth = 24f
        };
        style.Normalize();
        return style;
    }
}
