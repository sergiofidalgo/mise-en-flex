# Ideas

Ideas worth keeping that aren't decided or scheduled yet. When one is accepted, record it in `decisions.md` and note which roadmap phase picks it up.

---

## Storage with capacity instead of endless ingredient crates

*Added 2026-11-02 · Relevant phases: 1 (fridge station), 3 (Prep restocking), 4 (placing storage), 5 (suppliers) · Pillars: plannable chaos, identity*

**The idea (from playtesting Phase 0):** unlike PlateUp's one-ingredient crates that never run out, ingredients come from **fridges and shelves with limited capacity**, from day one. Where storage goes, what goes in it and how much fits becomes something players plan around.

**Why it's strong:**
- It's a clear point of difference from PlateUp.
- It makes the Plan and Prep phases matter: ordering, putting deliveries away and stocking the line are real decisions with visible payoff in service.
- It connects systems we already want:
  - **freshness and spoilage:** a fridge keeps things fresh, a shelf doesn't, and over-ordering wastes money;
  - **hygiene:** real kitchens store raw meat *below* ready-to-eat food to avoid drips, which could be a light cross-contamination rule behind the hygiene dial;
  - **layout:** storage near the stations that use it means fewer trips;
  - **economy:** bigger and colder storage costs money, and delivery size trades against shelf space;
  - **staff:** a prep assistant restocks the line.
- Running out mid-service creates good, plannable chaos: "who runs to the walk-in?"

**Risks and pushback:**
- **Inventory busywork.** If managing storage turns into menus and spreadsheets, it fights the frantic service. It should stay *spatial and visible*: you see what's in a fridge from the camera, and you stock it by carrying things there.
- **Picking a specific ingredient from a mixed fridge is the core UX problem.** One button can't say "the onion, not the tomato." Options:
  1. **Visible bins (recommended):** a shelf or fridge is a row of bins, each holding portions of one ingredient. Each bin can be targeted on its own with the existing Interact targeting, so there are no menus and it reads from overhead. Capacity = number of bins × portions per bin.
  2. **Close-up picker:** Interact opens the fridge in the close-up panel and you grab with the stick. This fits the close-up framework but adds a step to every grab, which is slow for the most common action in service.
  3. **A cycle button** to choose the ingredient before grabbing. This is quick to build but fiddly under pressure.
- **Solo players** may need more generous capacity or restocking help; this could be a realism dial.

**Suggested phasing:**
- **Phase 1:** the fridge station (already on the roadmap) is built as bins with capacity, refilled automatically at the start of each test run. It replaces the Phase 0 crates.
- **Phase 3:** stock carries over between days, and restocking from a storeroom/walk-in becomes a Prep task (and a job for the prep assistant).
- **Phase 4:** storage is placeable in layout editing, with sizes and tiers (shelf, reach-in fridge, walk-in, freezer).
- **Phase 5:** suppliers and deliveries: order sizes, delivery timing, cost, spoilage.

**Open questions:**
- Do bins hold one ingredient type each? Recommended yes, which matches the board-rail rule.
- Are portions counted, or is each bin a single item?
- Is there an off-floor storeroom (unlimited but far away), so running out costs time rather than ending service?
- Does cold storage matter from Phase 1, or only once spoilage exists?

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
