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

    public static void ClearRunContext()
    {
        CombatQuillStyleRegistry.BindToNetService(null);
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

    public static bool TryEnsureOverlaysForCurrentRun()
    {
        var settings = CombatQuillSettingsStore.Load();
        if (!settings.Enabled)
        {
            InitializeRunContext();
            return false;
        }

        InitializeRunContext();

        if (!RunManager.Instance.IsInProgress || NRun.Instance is null)
        {
            return false;
        }

        var attachedAny = false;

        foreach (var combatUi in EnumerateNodes<NCombatUi>(NRun.Instance))
        {
            AttachOverlay(combatUi);
            attachedAny = true;
        }

        if (settings.EnableInRewards)
        {
            foreach (var rewardsScreen in EnumerateNodes<NRewardsScreen>(NRun.Instance))
            {
                AttachOverlay(rewardsScreen);
                attachedAny = true;
            }
        }

        return attachedAny;
    }

    private static void AttachOverlay(Control screenRoot, CombatQuillScreenKind screenKind)
    {
        var settings = CombatQuillSettingsStore.Load();
        if (!settings.Enabled)
        {
            GD.Print($"{MainFile.ModId}: skipped attach for {screenKind.GetDisplayName()} because mod is disabled in settings");
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

    private static IEnumerable<TNode> EnumerateNodes<TNode>(Node root) where TNode : class
    {
        if (root is TNode typedRoot)
        {
            yield return typedRoot;
        }

        foreach (Node child in root.GetChildren())
        {
            foreach (var nestedMatch in EnumerateNodes<TNode>(child))
            {
                yield return nestedMatch;
            }
        }
    }
}
