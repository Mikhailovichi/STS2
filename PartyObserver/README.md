# PartyObserver

`PartyObserver` is a standalone multiplayer quality-of-life mod.

## Current Scope

- Adds a compact teammate observer panel to supported run screens.
- Lets you inspect synced teammate `card reward selection` choices.
- Lets you inspect synced teammate `event` options.
- Falls back to showing the teammate's current multiplayer screen type when detailed choices are not synced.

## Current UI

- A small draggable inspector panel appears on supported screens.
- Click a teammate row to inspect their currently synced options.
- The current build uses a clean row-based teammate list so we can swap it to avatars later without rewriting the sync layer.

## Supported Screens In This Build

- Combat UI
- Rewards screen
- Card reward selection screen
- Event room

## Limitations

- This is not a full remote screen mirror.
- Shop, campfire, transform, removal, upgrade, and map choices are not synced yet.
- Unsupported screens currently show status only.
