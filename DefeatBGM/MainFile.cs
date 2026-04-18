using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace DefeatBGM;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "DefeatBGM";

    public static void Initialize()
    {
        Log("initializing");

        var harmony = new Harmony(ModId);
        harmony.PatchAll(typeof(MainFile).Assembly);
    }

    public static void Log(string message)
    {
        GD.Print($"{ModId}: {message}");
    }

    public static void LogWarning(string message)
    {
        GD.PrintErr($"{ModId}: {message}");
    }

    public static void LogError(string context, Exception exception)
    {
        GD.PrintErr($"{ModId}: {context}: {exception}");
    }
}
