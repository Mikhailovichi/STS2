# CombatQuill

`CombatQuill` is the first production mod in this workspace.

## Current State

- Battle UI lifecycle patch is installed.
- A battle-safe drawing overlay is live.
- The overlay now uses an official map-style toolbar in combat.
- Official quill / eraser / clear icons are reused.
- Official quill / eraser cursor assets are reused while a tool is active.
- Build and publish are wired to output `.dll + .pck + manifest + settings`.

## Current Controls

- `F8`: toggle CombatQuill on or off
- `Middle Mouse Drag`: draw or erase, depending on the selected tool
- `E`: swap between quill and eraser
- `F9`: clear the current combat canvas
- `Esc`: exit CombatQuill tool mode
- Toolbar buttons: select quill, eraser, or clear with the mouse

## Why The Input Differs From The Map Screen

The toolbar is intentionally map-like, but the drawing input is battle-safe.

- We keep drawing on `Middle Mouse` by default.
- This avoids stealing the game's normal left-click card drag flow.
- The toolbar still gives the same official tool semantics: quill, eraser, clear.

## Current Limitations

- No multiplayer stroke sync yet
- No controller workflow yet
- No persistence across combats yet
- No direct reuse of the full map `NMapDrawingInput` network flow yet

## Build

- Run `../build-combatquill.ps1` for a local build.
- Run `../build-combatquill.ps1 -Publish` to export the `.pck` as well.
- Local `.NET 9 SDK` lives under `modding/.tools/dotnet9/`.
- Local Godot `4.5.1 mono` lives under `modding/.tools/godot451/`.
