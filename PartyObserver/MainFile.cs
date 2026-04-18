using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace PartyObserver;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "PartyObserver";

    public static void Initialize()
    {
        GD.Print($"{ModId}: initializing");

        var harmony = new Harmony(ModId);
        harmony.PatchAll(typeof(MainFile).Assembly);
    }
}
