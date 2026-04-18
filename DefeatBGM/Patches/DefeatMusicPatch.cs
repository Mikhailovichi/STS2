using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Audio;
using DefeatBGM.Services;

namespace DefeatBGM.Patches;

[HarmonyPatch(typeof(NAudioManager))]
internal static class DefeatMusicPatch
{
    private const string DefaultGameOverMusic = "event:/temp/sfx/game_over";

    [HarmonyPrefix]
    [HarmonyPatch(nameof(NAudioManager.PlayMusic))]
    private static bool BeforePlayMusic(string music)
    {
        if (string.Equals(music, DefaultGameOverMusic, StringComparison.Ordinal))
        {
            return !DefeatSongService.TryPlayDefeatSong();
        }

        DefeatSongService.StopIfPlaying();
        return true;
    }
}
