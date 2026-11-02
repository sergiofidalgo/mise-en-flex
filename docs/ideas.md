# Ideas

Ideas worth keeping that aren't decided or scheduled yet. When one is accepted, record it in `decisions.md` and note which roadmap phase picks it up.

---

## Tooling: evaluate the Unity CLI

*Added 2026-11-02 · Relevant phase: any · Supports: verification*

Unity Hub 3.22 advertises a "Unity CLI" which, with a pipeline editor package, can drive the Editor from the terminal (e.g. running EditMode tests with JSON output while the Editor is open). If it works as described, Claude Code could run tests and Editor menu items without closing the Editor, which would make every step easier to verify. Evaluate once the project is set up: what it needs installed, whether it works with Unity 6.3 LTS, and whether it replaces our batch-mode test command.

---

## Close-up transition (entering/leaving a station)

*Added 2026-11-02 · Relevant phase: 1 (close-up framework) · Pillar: craft*

The risk is that the close-up feels like a mode switch that steals control and loses track of the kitchen. The aim is a continuous transition:

- **The panel grows out of the station.** On interact, the close-up opens from the station's screen position and slides into the player's panel (~0.2–0.3 s). On exit it shrinks back to the station.
- **No dead input.** Switch from the `Gameplay` to the `CloseUp` action map at the *start* of the transition and buffer presses during it. Leaving should be cheap: pull the stick away from the station, or press B / Esc.
- **The world still talks to you.** The main view keeps running, and the panel's edge pings for events that need the player (pan smoking, new ticket, partner calling).
- **The avatar performs in the world.** The player's character visibly chops/sears at the station so the partner can read what they're doing without looking at their panel.

**Tuning values:** transition duration, panel size and position, whether the main view dims during close-up, which events ping.

**Playtest:** put a steak on the hob, send the player to dice an onion, and count how often they notice the steak in time. Frequent misses mean the pings or panel size need work.

---

## Cutting board station with a mise en place rail

*Added 2026-11-02 · Relevant phase: 1 (board station) · Pillars: craft, plannable chaos*

> **Accepted 2026-11-02.** See `decisions.md` and `game-design.md` §6.2. The open questions below are settled: 3 slots to start, one ingredient and cut per container, a full rail leaves the cut on the board.

The board station is the board plus a **rail of small containers** (ninth-pans), as in a real kitchen:

- Cut product goes from the board into a container. From the overhead camera, each container visibly shows its contents and cut (diced vs. sliced onion, minced garlic) by shape and colour, with no UI needed.
- Containers are grabbable items: the partner can take the diced onion to the hob without entering the close-up.
- The rail is the station's inventory. Its state at the start of service shows directly how well the team prepped.

**Open questions:** how many slots per rail (likely an equipment-tier upgrade), whether containers mix ingredients (probably not), and whether a full rail blocks further cutting.

---

## Carrying, inspired by real restaurants

*Added 2026-11-02 · Relevant phases: 0 (pick up/carry/place), 1–3 · Pillar: plannable chaos*

PlateUp/Overcooked carrying feels lame because items are tokens, it's always one item per trip, and there's no skill or decision in it. Borrow from real kitchens and simplify:

| Real kitchen | In-game version |
| --- | --- |
| Lowboy fridges under each station, stocked before service | Stations have small built-in storage, filled during Prep, so service needs few trips to the walk-in |
| Mise en place in ninth-pans | One container = many portions of one ingredient, carried as one item |
| Sheet trays / speed racks | A tray carries several different items but is slower and can tip on sharp turns |
| Servers stacking plates up their arm | Up to 3 plates, with movement penalties as the stack grows |
| "Behind!", "Hot!", "Corner!" | Automatic barks and soft collision avoidance when passing the partner with something hot |
| The pass, the bell, "hands please!" | A handoff surface: put food down, ring, partner or runner takes it |
| Bus tubs for dirty dishes | One container for clearing tables, emptied at the dish station |

**Three ways to carry:**
1. **Hands:** one item, fastest.
2. **Container:** one ingredient, many portions.
3. **Tray / stack:** mixed items, slower, can spill.

**Food keeps simulating while carried:** a hot pan keeps cooking and cools as you walk, plated food loses serving temperature on the way to the pass, raw meat on hands cross-contaminates what you touch next. Walking is never dead time.

**Runners** (Phase 3 staff) remove trips. That's a visible payoff from a person, not a conveyor, so it respects the no-automation rule.

**Tuning values:** tray speed penalty, spill threshold (turn rate × load), plate stack limit, station storage capacity.

**Playtest (Phase 0/1 greybox):** does a loaded tray feel tense or just slow? If players avoid trays entirely, the penalty is too high.
