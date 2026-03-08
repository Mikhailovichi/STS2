# STS2 Mods

**Language / 语言 / 言語**: [简体中文](./README.md) | **English** | [日本語](./README.ja.md)

A collection of mods for *Slay the Spire 2* focused on multiplayer coordination, combat visualization, and better in-run communication.

This repository currently contains three standalone mods that are designed to complement each other:

- 🖋️ [CombatQuill](./CombatQuill): tactical markup and communication inside combat and related screens
- 👀 [PartyObserver](./PartyObserver): view key teammate choices that are currently synced to your client
- 📊 [DamageMeter](./DamageMeter): track combat output, resource usage, and post-fight review data

These mods are not meant to be isolated feature patches. The broader goal is to add a clearer collaboration layer to multiplayer runs:

- `CombatQuill` helps answer "what should we do right now?"
- `PartyObserver` helps answer "what is my teammate choosing right now?"
- `DamageMeter` helps answer "what actually happened in this fight?"

## ✨ Repository Vision

This repository is closer to an evolving multiplayer utility suite than a one-off experiment.  
All three mods are already functional and actively iterated on, but the overall project is still in an early public phase: the direction is clear, the UI and interaction model are already usable, and the foundation is strong, while deeper multiplayer sync, broader information coverage, UI polish, and long-term data systems still have plenty of room to grow.

## 🖋️ CombatQuill

`CombatQuill` is built around one idea: adding a battle-safe markup layer to multiplayer combat that feels useful without getting in the way of the base game flow.

In multiplayer runs, a lot of communication normally happens through voice chat or quick text explanations such as "hit the left enemy first", "use this card here", or "I will defend this turn, you finish the target".  
`CombatQuill` turns that kind of moment-to-moment shot-calling into visible on-screen annotations so coordination becomes faster and more intuitive.

### Feature Overview

- Adds an on-demand drawing overlay to combat screens for highlighting targets, tracing routes, and communicating play order.
- Uses map-drawing-inspired visual language such as quill, eraser, and clear tools to stay close to the official UI style.
- Keeps drawing input on a safer path by default so it does not steal the normal left-click card drag flow.
- Can be toggled quickly during combat, which makes it practical for high-pressure turns instead of forcing a permanent overlay.
- Already supports baseline customization such as colors, stroke behavior, erasing, opacity, and toolbar-related settings.
- In practice, it works less like a simple doodle layer and more like a lightweight tactical whiteboard for marking kill order, focus-fire targets, temporary holds, or resource allocation for a specific turn.

### Roadmap

The most important next step for `CombatQuill` is to move from a strong local annotation tool toward a real synchronized tactical layer for multiplayer play.

- Near-term work will focus on stronger multiplayer sync so annotations are not only visible locally, but can be shared and understood more reliably by teammates.
- Marker ownership, color semantics, and layer management will likely be expanded so simultaneous use by multiple players stays readable.
- Short-term persistence across transitions is also a natural direction, especially for reviewing key decisions after a combat ends.
- Controller support and broader input customization are both clear future targets. The current design prioritizes keyboard-and-mouse safety during combat, but it should eventually feel more natural on gamepad as well.
- Longer term, `CombatQuill` may evolve from a pure drawing tool into a broader tactical expression system with more structured indicators, emphasis markers, temporary pings, or other communication shortcuts that fit multiplayer play better.

## 👀 PartyObserver

`PartyObserver` is designed to turn a common multiplayer pain point into a quick UI glance: "I do not know what my teammate is currently looking at, choosing, or comparing."

In multiplayer mode, many teammate decisions are not naturally visible from your side.  
You may know someone is no longer in combat, but not whether they are looking at rewards, an event, or already locking in a choice. You may also know they are on a card reward screen, but still have no idea which options they are actually weighing.  
`PartyObserver` exists to bring that missing context into a compact, stable, low-distraction panel.

### Feature Overview

- Adds a compact teammate observer panel on supported screens to show synchronized teammate state.
- Already supports viewing synced card reward choices and event-room options from teammates.
- When detailed context is not yet available for a given screen, it can still fall back to showing the teammate's current multiplayer screen type.
- Uses a lightweight draggable panel so the information can stay visible during runs without aggressively taking over the main UI.
- The current structure is intentionally expandable, so even though the presentation is still simple, the underlying system is already set up for richer future views rather than a disposable temporary UI.

### Roadmap

`PartyObserver` will continue moving toward the goal of surfacing teammate decision context more naturally, but it is not meant to become a full remote screen mirror. The goal is a multiplayer-aware observer for state and choices.

- The clearest near-term expansion is support for more important contexts such as shops, campfires, upgrades, removals, transforms, and map navigation.
- Future versions will likely care more about "why is the teammate staying on this screen?" and "what are they comparing right now?" instead of only showing screen type.
- The UI is expected to move toward longer-session multiplayer usability with better grouping, stronger state feedback, and possibly avatar-based presentation.
- If sync coverage becomes stable enough, lightweight summaries could be added for candidate options, stage intent, or simplified spectator-friendly views.
- Longer term, `PartyObserver` aims to become a true collaboration-awareness layer where you no longer need to ask what your teammate is doing because the UI already provides the relevant context.

## 📊 DamageMeter

`DamageMeter` is not just meant to show a damage ranking. Its real goal is to organize the most review-worthy combat data into something readable, comparable, and accumulative.

In multiplayer runs, simply looking at final damage numbers hides a lot of the story.  
Who contributed more team support? Who absorbed more punishment? Which turn created the biggest spike? Which character keeps wasting energy? Which fight was unusually efficient or inefficient?  
`DamageMeter` is built to bring that process-level information into the same visualization layer.

### Feature Overview

- Provides an in-combat statistics panel for both current-fight and cumulative tracking.
- Already supports many stat dimensions, including total damage, actual damage, damage taken, damage per turn, team support damage, team block support, cards played, block, energy, damage efficiency, overkill, potion usage, debuffs applied, and more.
- Includes chart-oriented views for reading team tempo, per-turn spikes, and resource waste rather than only raw totals.
- Supports more review-oriented views such as combat logs, death records, and card-flow-related information.
- Tracks longer-term records such as highest single hit, highest fight total, best turn output, most cards played, most block gained, and cumulative fight stats.
- Supports panel position, scaling, opacity, and row-count settings so the HUD can fit different layouts and preferences.
- In practice, it is useful both for multiplayer post-fight review and for single-player analysis when you want to understand whether a build is actually delivering the performance you expected.

### Roadmap

The next phase for `DamageMeter` is not just "more numbers", but "more interpretable numbers" that are easier to compare and better suited for long-term analysis.

- Near-term work will keep improving readability of the current category panels, especially chart presentation, row control, data emphasis, and stability across different combat tempos.
- A strong future direction is finer attribution, such as breaking damage, block, or resource gains down by card, relic, power, or turn window.
- Another obvious expansion is moving from current-fight tracking toward full-run review, including comparisons by room, elite, boss, character, or even entire run.
- If chart work continues to mature, `DamageMeter` is a good fit for richer trend views that make it easier to spot ramp turns, resource droughts, and burst windows.
- Over time, it can grow from an in-run HUD into a more complete analysis tool that not only reports the result, but also helps explain why the result happened.

## 🛠️ Local Build Notes

- The current project files assume the repository lives under the game's `modding/` directory.
- `build-combatquill.ps1` builds or publishes `CombatQuill`.
- `build-partyobserver.ps1` builds or publishes `PartyObserver`.
- `DamageMeter/build.ps1` builds `DamageMeter` and can optionally install it into the local `mods/` directory.
- `CombatQuill` and `PartyObserver` currently build with the local toolchain stored in the repository; `DamageMeter` currently builds with the system `dotnet`.

## 🚧 Current Status

All three mods are ready for continued public iteration, but the overall project is still in an early stage.  
The most exciting part is not any single isolated feature, but the way these three directions can gradually combine into a fuller multiplayer support experience:

- clearer in-run tactical markup
- better awareness of teammate state and choices
- deeper combat and build review

That broader combination is the main long-term thread of this repository.
