using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace RelicRpsChoice;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "RelicRpsChoice";

    public static void Initialize()
    {
        GD.Print($"{ModId}: initializing");

        var harmony = new Harmony(ModId);
        harmony.PatchAll();
    }

    public static void Log(string message)
    {
        GD.Print($"{ModId}: {message}");
    }

    public static void LogError(string context, Exception exception)
    {
        GD.PrintErr($"{ModId}: {context}: {exception}");
    }
}
