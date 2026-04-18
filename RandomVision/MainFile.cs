using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using RandomVision.Services;

namespace RandomVision;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "RandomVision";

    public static void Initialize()
    {
        GD.Print($"{ModId}: initializing");
        RandomVisionI18n.Initialize();

        var harmony = new Harmony(ModId);
        harmony.PatchAll(typeof(MainFile).Assembly);
    }

    public static void LogError(string context, Exception exception)
    {
        GD.PrintErr($"{ModId}: {context}: {exception}");
    }
}
