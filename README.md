# Slay the Spire 2 Mods

This repository contains three Slay the Spire 2 mods developed in the same local workspace:

- `CombatQuill`: multiplayer combat drawing and tactical annotation overlay
- `PartyObserver`: synced teammate choice observer panel
- `DamageMeter`: combat damage, energy, and review chart overlay

## Projects

- `CombatQuill/`
- `PartyObserver/`
- `DamageMeter/`

## Local Build Notes

- These projects are built against a local Slay the Spire 2 installation.
- The current project files assume this repository lives under the game's `modding/` directory.
- `build-combatquill.ps1` builds or publishes `CombatQuill`.
- `build-partyobserver.ps1` builds or publishes `PartyObserver`.
- `DamageMeter/build.ps1` builds `DamageMeter` and can install it into the local `mods/` folder.

## What Is Not Included

- Game binaries and unpacked game data
- Local build outputs
- Godot cache folders
- Temporary inspection or scratch files
