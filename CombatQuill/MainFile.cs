using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace CombatQuill;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "CombatQuill";

    public static void Initialize()
    {
        GD.Print($"{ModId}: initializing");

        var harmony = new Harmony(ModId);
        harmony.PatchAll();
    }
}
