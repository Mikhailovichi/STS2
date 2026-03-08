using CombatQuill.UI;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Runs;

namespace CombatQuill.Services;

internal static class CombatQuillService
{
    public static void InitializeRunContext()
    {
        var settings = CombatQuillSettingsStore.Load();
        settings.Normalize();
        if (ApplyCurrentCharacterColorProfile(settings))
        {
            CombatQuillSettingsStore.Save(settings);
        }

        CombatQuillStyleRegistry.BindToNetService(RunManager.Instance.IsInProgress ? RunManager.Instance.NetService : null);
    }

    public static void AttachOverlay(NCombatUi combatUi)
    {
        AttachOverlay(combatUi, CombatQuillScreenKind.Combat);
    }

    public static void AttachOverlay(NRewardsScreen rewardsScreen)
    {
        if (!CombatQuillSettingsStore.Load().EnableInRewards)
        {
            return;
        }

        AttachOverlay(rewardsScreen, CombatQuillScreenKind.Rewards);
    }

    public static void PersistSettings(CombatQuillSettings settings, bool broadcastStyle = true)
    {
        SaveCurrentCharacterColorProfile(settings);
        CombatQuillSettingsStore.Save(settings);
        CombatQuillStyleRegistry.UpdateLocalFromSettings(settings, broadcastStyle);
    }

    private static void AttachOverlay(Control screenRoot, CombatQuillScreenKind screenKind)
    {
        var settings = CombatQuillSettingsStore.Load();
        if (!settings.Enabled)
        {
            return;
        }

        InitializeRunContext();

        var overlayNodeName = screenKind.GetOverlayNodeName();
        if (screenRoot.GetNodeOrNull<CombatQuillOverlay>(overlayNodeName) is not null)
        {
            return;
        }

        var overlay = new CombatQuillOverlay();
        overlay.Name = overlayNodeName;
        overlay.Initialize(settings, screenKind);
        screenRoot.AddChild(overlay);

        GD.Print($"{MainFile.ModId}: attached overlay to {screenKind.GetDisplayName()}");
    }

    private static bool ApplyCurrentCharacterColorProfile(CombatQuillSettings settings)
    {
        return CombatQuillCharacterContextResolver.TryResolveCurrent(out var context) &&
               settings.ApplyCharacterColorProfile(context.CharacterKey, context.DefaultColorHtml);
    }

    private static void SaveCurrentCharacterColorProfile(CombatQuillSettings settings)
    {
        if (!CombatQuillCharacterContextResolver.TryResolveCurrent(out var context))
        {
            return;
        }

        settings.SaveCharacterColorProfile(context.CharacterKey, context.DefaultColorHtml);
    }
}
