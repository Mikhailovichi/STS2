# DefeatBGM

## 简介 / Overview

`DefeatBGM` 是一个为《Slay the Spire 2》制作的轻量音频模组。
它会拦截原版战败 / game over 音乐触发，在你失败时优先播放放在模组文件夹里的自定义本地曲目。

这个发布版默认内置音乐为 **《关羽之歌》**。
如果你还想加入别的曲目，也可以继续把 `.ogg`、`.mp3` 或 `.wav` 文件放进 `mods/DefeatBGM/music` 目录。

`DefeatBGM` is a lightweight audio mod for *Slay the Spire 2*.
It intercepts the vanilla defeat / game-over music trigger and plays a custom local track from the mod folder instead.

This release ships with **Guan Yu's Song** as the default bundled track.
If you want additional songs, you can still add your own `.ogg`, `.mp3`, or `.wav` files into `mods/DefeatBGM/music`.

## 功能说明 / Features

- 拦截原版战败 `game_over` 音乐并优先播放自定义曲目
- 发布包默认内置《关羽之歌》
- 每次战败时都会扫描 `mods/DefeatBGM/music` 目录
- 支持 `.ogg`、`.mp3`、`.wav` 三种常见音频格式
- 只有一首可用曲目时固定播放，多首时每次战败随机选择
- 如果没有找到可用音频文件，会自动回退到原版失败音乐
- 当游戏恢复正常音乐播放时，会停止当前自定义失败 BGM

- Intercepts the vanilla `game_over` defeat cue and prefers a custom track
- Bundles *Guan Yu's Song* as the default release track
- Scans `mods/DefeatBGM/music` every time you are defeated
- Supports three common audio formats: `.ogg`, `.mp3`, and `.wav`
- Plays the only available track if there is one, or randomly selects one when multiple tracks exist
- Falls back to the vanilla defeat music if no usable audio file is found
- Stops the current custom defeat track when the game resumes normal music playback

## 安装方法 / Installation

1. 完全退出游戏。
2. 将发布包中的 `mods/DefeatBGM` 文件夹复制到游戏根目录下的 `mods` 文件夹中。
3. 发布包默认已附带《关羽之歌》；如果你想扩充曲库，也可以再把自己的 `.ogg`、`.mp3` 或 `.wav` 文件放入 `mods/DefeatBGM/music`。
4. 启动游戏并确认模组已正常加载。

1. Close the game completely.
2. Copy the included `mods/DefeatBGM` folder into the game's `mods` directory.
3. The release already includes *Guan Yu's Song*; if you want more tracks, you can also add your own `.ogg`, `.mp3`, or `.wav` files into `mods/DefeatBGM/music`.
4. Launch the game and confirm that the mod is loaded.

## 使用方式 / How To Use

1. 安装完成后，默认即可使用内置的《关羽之歌》；如果你想自定义，也可以继续往 `mods/DefeatBGM/music` 里添加音频文件。
2. 在游戏中战败时，模组会自动扫描该目录并尝试播放其中一首曲目。
3. 如果目录里有多首可用音频，每次战败都会随机播放其中一首；如果目录里只保留内置曲目，就会播放《关羽之歌》。

1. After installation, the bundled *Guan Yu's Song* already works by default; if you want customization, you can add more audio files into `mods/DefeatBGM/music`.
2. When you are defeated in game, the mod automatically scans that folder and attempts to play one of the tracks.
3. If multiple supported files are present, one is chosen at random on each defeat; if only the bundled track remains, it will play *Guan Yu's Song*.
