# DefeatBGM

DefeatBGM intercepts the default `game_over` defeat music cue and replaces it with a custom local track loaded from the mod folder.

## Usage

1. The release already includes `关羽之歌.mp3` by default.

2. If you want more tracks, put one or more audio files into:

```text
mods/DefeatBGM/music/
```

You can keep using that track, add more songs to the folder, or replace it with your own selection.

3. Supported formats:

```text
.ogg
.mp3
.wav
```

4. Each time you are defeated, the mod scans the `music` folder and randomly picks one supported track to play.

## Notes

- If the folder only contains the bundled `关羽之歌.mp3`, that track will play on defeat.
- If the `music` folder contains only one supported track, that track will always be used.
- If the folder contains multiple supported tracks, one of them is chosen randomly on each defeat.
- If no supported track is found, the game falls back to the original `game over` audio.
- The custom track uses the BGM bus when available and falls back to the Master bus otherwise.
