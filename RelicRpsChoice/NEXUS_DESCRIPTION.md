# Relic RPS Choice

## 简介 / Overview

`Relic RPS Choice` 是一个为《Slay the Spire 2》多人模式制作的轻量玩法 mod。
当多人宝箱房里有多名玩家选择同一件共享遗物时，这个 mod 会把冲突结算改成一个可见、可操作的石头剪刀布对决流程。

`Relic RPS Choice` is a lightweight multiplayer gameplay mod for *Slay the Spire 2*.
When multiple players choose the same shared relic in a treasure room, this mod turns that conflict into a visible and interactive rock-paper-scissors showdown.

## 功能说明 / Features

- 多人共享遗物冲突时弹出石头 / 剪刀 / 布选择界面
- 每件发生冲突的遗物独立结算，没有冲突的遗物仍按正常流程分配
- 支持多轮淘汰，直到决出最终赢家
- 支持鼠标点击，以及 `1 / 2 / 3`、`R / P / S`、方向键、`Enter / Space` 操作
- 对决期间会隐藏并拦截 `Skip` 按钮，避免误跳过

- Adds a rock-paper-scissors prompt when shared relic picks collide in multiplayer
- Resolves each contested relic independently while leaving uncontested relics on the normal path
- Supports repeated elimination rounds until a final winner is decided
- Supports mouse input plus `1 / 2 / 3`, `R / P / S`, arrow keys, and `Enter / Space`
- Hides and blocks the `Skip` button during the showdown to prevent accidental bypasses

## 安装方法 / Installation

1. 完全退出游戏。
2. 将压缩包中的 `mods/RelicRpsChoice` 文件夹复制到游戏根目录下的 `mods` 文件夹中。
3. 启动游戏并确认 mod 已正常加载。

1. Close the game completely.
2. Copy the included `mods/RelicRpsChoice` folder into the game's `mods` directory.
3. Launch the game and confirm that the mod is loaded.

## 使用方式 / How To Use

1. 进入多人宝箱房共享遗物选择流程。
2. 如果多名玩家选择同一件遗物，界面会出现石头 / 剪刀 / 布选择面板。
3. 本地玩家完成出招后，系统会等待其他参与者完成选择并自动结算结果。

1. Enter a multiplayer treasure room shared relic selection.
2. If multiple players choose the same relic, the rock-paper-scissors panel will appear.
3. After the local player locks in a move, the mod waits for the other involved players and resolves the result automatically.
