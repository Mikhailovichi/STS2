using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes;

namespace DefeatBGM.Services;

internal static class DefeatSongService
{
    private const string PlayerNodeName = "DefeatBGMPlayer";

    private static readonly StringName BgmBusName = new("BGM");
    private static readonly StringName MasterBusName = new("Master");

    private static bool _missingSongLogged;

    public static bool TryPlayDefeatSong()
    {
        try
        {
            var availableSongs = GetAvailableSongPaths().ToArray();
            if (availableSongs.Length == 0)
            {
                return false;
            }

            var songPath = ChooseSongPath(availableSongs);
            var stream = LoadSong(songPath);
            if (stream is null)
            {
                MainFile.LogWarning($"Failed to load defeat song, falling back to vanilla audio: {songPath}");
                return false;
            }

            var player = EnsurePlayer();
            if (player is null)
            {
                MainFile.LogWarning("Could not create the local audio player node, falling back to vanilla defeat audio.");
                return false;
            }

            player.Stop();
            player.Stream = stream;
            player.Bus = ResolvePlaybackBus();
            player.Play();

            MainFile.Log($"Playing random defeat track: {Path.GetFileName(songPath)}");
            return true;
        }
        catch (Exception exception)
        {
            MainFile.LogError("Playing defeat song failed", exception);
            return false;
        }
    }

    public static void StopIfPlaying()
    {
        var player = GetExistingPlayer();
        if (player is null || !player.IsPlaying())
        {
            return;
        }

        player.Stop();
    }

    private static AudioStreamPlayer? EnsurePlayer()
    {
        var existing = GetExistingPlayer();
        if (existing is not null)
        {
            return existing;
        }

        if (NGame.Instance is null)
        {
            return null;
        }

        var player = new AudioStreamPlayer
        {
            Name = PlayerNodeName,
            Bus = ResolvePlaybackBus()
        };

        NGame.Instance.AddChild(player);
        return player;
    }

    private static AudioStreamPlayer? GetExistingPlayer()
    {
        return NGame.Instance?.GetNodeOrNull<AudioStreamPlayer>(PlayerNodeName);
    }

    private static StringName ResolvePlaybackBus()
    {
        return AudioServer.GetBusIndex(BgmBusName) >= 0 ? BgmBusName : MasterBusName;
    }

    private static IEnumerable<string> GetAvailableSongPaths()
    {
        var musicDirectory = GetMusicDirectory();
        if (!Directory.Exists(musicDirectory))
        {
            LogMissingSongOnce(musicDirectory);
            return Enumerable.Empty<string>();
        }

        var songs = Directory
            .EnumerateFiles(musicDirectory)
            .Where(IsSupportedAudioFile)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (songs.Length == 0)
        {
            LogMissingSongOnce(musicDirectory);
            return Array.Empty<string>();
        }

        return songs;
    }

    private static string ChooseSongPath(IReadOnlyList<string> availableSongs)
    {
        if (availableSongs.Count == 1)
        {
            return availableSongs[0];
        }

        var index = Random.Shared.Next(availableSongs.Count);
        return availableSongs[index];
    }

    private static AudioStream? LoadSong(string songPath)
    {
        var extension = Path.GetExtension(songPath).ToLowerInvariant();
        return extension switch
        {
            ".ogg" => AudioStreamOggVorbis.LoadFromFile(songPath),
            ".mp3" => AudioStreamMP3.LoadFromFile(songPath),
            ".wav" => AudioStreamWav.LoadFromFile(songPath, new Godot.Collections.Dictionary()),
            _ => null
        };
    }

    private static string GetMusicDirectory()
    {
        var executableDirectory = Path.GetDirectoryName(OS.GetExecutablePath()) ?? ".";
        return Path.Combine(executableDirectory, "mods", MainFile.ModId, "music");
    }

    private static bool IsSupportedAudioFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".wav", StringComparison.OrdinalIgnoreCase);
    }

    private static void LogMissingSongOnce(string musicDirectory)
    {
        if (_missingSongLogged)
        {
            return;
        }

        MainFile.LogWarning(
            $"No supported defeat tracks were found. Put one or more .ogg, .mp3, or .wav files into: {musicDirectory}");
        _missingSongLogged = true;
    }
}
