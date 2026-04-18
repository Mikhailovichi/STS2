# STS2 Mods

**Language / 语言 / 言語**: [简体中文](./README.md) | **English** | [日本語](./README.ja.md)

Source repository for *Slay the Spire 2* mods focused on multiplayer coordination, combat information visibility, and cleaner in-run communication.

## Active Mods

- [CombatQuill](./CombatQuill): tactical markup and quick communication on combat-related screens
- [DefeatBGM](./DefeatBGM): replaces the default defeat music with custom local tracks from the mod folder
- [FrozenEye](./FrozenEye): shows the draw pile in actual draw order instead of the game's default sorting
- [PartyObserver](./PartyObserver): surfaces synced teammate choices and current screen context
- [RandomVision](./RandomVision): adds more transparent previews for Crystal Sphere and event pages
- [RelicRpsChoice](./RelicRpsChoice): resolves shared relic conflicts with a visible rock-paper-scissors flow

## Archived Mods

- [legacy/DamageMeter](./legacy/DamageMeter): preserved for source history and reference, but no longer treated as an actively maintained primary mod

## Release Versioning

- The shared release numbering rules live in [RELEASE_VERSIONING.md](./RELEASE_VERSIONING.md)
- Versions follow `x.y.z`
- `x` is for major architecture changes, `y` is for feature additions, and `z` is for bug fixes and optimizations

## Local Build Notes

- This repository tracks source and documentation only; it does not include local work directories such as `.tools`, `_workspace`, `_release`, or `mods/`
- `build-combatquill.ps1` and `build-partyobserver.ps1` first try repo-local `.tools`, then fall back to `modding/.tools` under the game install, and finally to a system `dotnet 9`
- If the repository is not checked out under the game root, pass `-Sts2Path` to point at your *Slay the Spire 2* install directory
- Other mods can be built directly from their local `*.csproj` or `*.sln` files

## Repository Layout

- The root keeps only actively maintained source, documentation, and the small set of shared scripts
- `legacy/` holds archived mods whose history should remain available
- Build artifacts, caches, editor folders, and runtime install directories stay out of version control
