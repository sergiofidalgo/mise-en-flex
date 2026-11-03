# Decisions

Design and technical decisions, newest first. Each entry: date, decision, reason.

---

## 2026-11-03 — Patience and walk-outs (Phase 2, milestone 3)

**Decisions (proposed while building; flagged for review):**
- **A seated customer waits 35 in-game minutes** (about 35 real seconds) for their food before walking out. Patience **drains linearly** from when they sit down until they're served; walking to the stool doesn't count.
- **Queueing by the door drains patience at half the rate.** One meter carries over from the queue to the stool, so a long queue means less patience at the counter.
- **Walking out** frees the stool for the next in the queue and counts as a lost cover. The log separates walk-outs from customers turned away at a full door.
- **Realism dial `Customer patience`** (in `RealismSettings`) multiplies the wait: 2 = twice as patient, 0 = never walk out. It applies live.
- **On screen:** a patience meter under each ticket (green → amber → red), a meter alone over queueing customers, and "walked out!" in red over a customer leaving angry.
- Patience left when served is recorded for tips in milestone 4; it doesn't change the dish score.

**Reason:** Adds the time pressure milestone 3 is for, while keeping the steak's 6–8 s cook plus rest comfortably inside one customer's wait.

---

## 2026-11-02 — Services last 5 hours; satisfaction is the dish score for now

**Decisions:**
- **A service lasts 5 in-game hours** (about 5 real minutes; was 6). The arrival pace stays the same. The rush stays at 35–60% of the service, so the quiet tail after it is about an hour shorter.
- **Satisfaction** for each served customer is, for now, **their dish's score against their order** (stars). The Service log shows the average, how many got what they ordered, and the last four dishes with what was off. Patience and the wait join it in milestones 3–4.

**Reason:** Playtest: the pace was right, but the end of the service dragged.

---

## 2026-11-02 — Orders, tickets and pacing (Phase 2, milestone 2)

**Decisions (proposed while building after the milestone 1 playtest; flagged for review):**
- **Slower service, more stock:** a customer about every **25** in-game minutes (was 12), with a rush at **2×** (was 2.5×), which makes about 18 covers instead of about 30. The fridge holds **2 × 9 steaks** (was 6) and the shelf **8 onions** (was 6). Running out is still possible on purpose; restocking comes with Prep in Phase 3.
- **Every customer orders when they arrive:** steak at a doneness, weighted **Rare 2 · Medium-rare 4 · Medium 3 · Well done 1** (no medium-well on the menu), and **onions with 40%** of orders. Tunable in `ServiceConfig`. Orders use their own random generator, so changing the menu doesn't move the arrivals.
- **Each doneness level has an order target** inside its band (steak: 48 / 54.5 / 60 / 65.5 / 71 °C core). The ordered level's target replaces the house standard when the dish is scored.
- **Scoring with an order:** the steak counts **1** and the side **0.5**. Anything ordered but missing counts as a zero at its weight, so a perfect steak without the onions it came with scores about two thirds.
- **Ticket above each seated customer** ("Steak · Medium-rare + Onions") until they're served.
- **Doneness tag on the food** each player carries or faces (in a pan, on a plate, on the counter): the level's name, hanging below the food so it doesn't cover the station's prompt.
- **One HUD colour per doneness**, shared by tickets and tags (in `HudStyle`), close to the steak's own band colours.
- **The pass prompt checks the plate:** "serve: as ordered", "serve: wanted Medium", "serve: no onions". You can still serve a wrong plate on purpose.

**Reason:** In playtest, demand outran stock, and nothing told the cook what to aim for. Orders make doneness the core decision of each dish, and the shared colours make it a quick check.

---

## 2026-11-02 — Customers and seating details (Phase 2, milestone 1)

**Decisions (proposed while building; flagged for review):**
- **Until orders exist (milestone 2), any plate with food** served on the pass in front of a seated customer counts: they eat it and leave. The quality card still shows.
- **The service starts automatically with the scene** for now; Start, the clock and the summary come in milestone 5.
- **One stool per pass counter** (three); customers come in through a **door at the east end** of the dining strip. With all stools taken they wait in a **queue of up to 3** by the door, the longest-waiting taking the next free stool; beyond that they **walk away (a lost cover)**.
- **Starting numbers (tunable in `ServiceConfig`):** a customer about every 12 in-game minutes (±35%), a rush from 35% to 60% of the service at 2.5× the rate, 15 in-game minutes to eat, walking at 1.6 m/s.
- **Fixed random seed**, so a service plays the same way each time while tuning.
- **Customers have no physics**; they never get stuck on chefs.

---

## 2026-11-02 — Phase 2 shape: counter service, steak to order, ~6-minute services

**Decisions:**
- **Counter service at the pass** for the first service: customers sit on stools along the dining side and are served on the pass in front of them; no front-of-house work until staff (Phase 3).
- **Menu:** steak to order (rare / medium-rare / medium / well done), sometimes with sautéed onions. **Orders set the doneness target**, replacing the medium-rare house standard.
- **A service lasts ~6 real minutes** (~6 in-game hours at 60x), with waves and a rush. Resolves the open question in `game-design.md` §4 (6–8 min recommended).
- **Wrong dish:** eaten unhappily and scored low; no send-backs.

**Reason:** Keeps the first vertical slice about cooking, timing and serving temperature, using everything Phase 1 built. Plan in `plans/phase-2.md`.

---

## 2026-11-02 — RT opens slot menus; cutting is a radial timing dial

**Decisions (yours, plus details flagged for review):**
- **Slot menus open on RT** (keyboard E), not by approaching. Facing storage shows only "RT open" (and "A put back" when holding food that belongs there); while RT is held, the radial menu shows the four slots and A/B/X/Y take from them. **The prep counter works the same for its slots**; putting on / taking from the board (A) and cutting (X) need no RT.
- **Cutting is a dial, replacing the beat:** a needle swings across a **half arc (170°, tunable) like a pendulum, far right to far left and back** (Early/Late follow the direction of travel); X is pressed as it crosses the ingredient's sweet spot. Centre = Good, edges = Early/Late, outside = Miss (still cuts, less evenly). **Each ingredient has its own sweet spot** (position along the sweep, width, speed); onion: 110° from the right end, ±14° Good, ±28° Early/Late, 0.7 s per sweep, **5 cuts**. (First built as a full circle, then a one-way sweep; changed to a pendulum the same day.)
- **The needle's speed reacts to each cut:** Good speeds it up progressively (×1.15), Early/Late slow it subtly (×0.95), a Miss slows it significantly (×0.75), within 0.5×–2× of the base speed. Tunable in `TuningConfig`.
- The dial is uGUI: a dark half-arc fan with an amber Early/Late band and a green Good core on its rim and a white needle. **Every cut is made obvious:** a marker where you pressed, the fan flashes in the grade's colour (pop on Good, shake on Miss), the grade (bigger for a Miss) with "faster"/"slower", a dot per cut coloured by its grade, and a cut-quality bar that drops when you're penalised.

**Supersedes:** "follow a beat" and the 8-chop onion (milestone 6′).

---

## 2026-11-02 — HUD direction

**Decisions:** a mix of **in-world information on stations** (heat ring, bin counts, doneness gauge) and **flat button badges in Xbox colours**; prompts are **badge + short verb**; doneness reads as a **gauge with a target band and a needle**; built with **uGUI + TextMeshPro**. Plan in `plans/hud-pass.md`.

**Reason:** Least clutter over a shared screen and readable from the overhead camera, without waiting for the art phase.

---

## 2026-11-02 — Knife, mise en place counter, rhythm chopping, resting (milestone 6′)

**Decisions (yours, plus building details flagged for review):**
- **The knife lives in its own tool slot**, separate from the hands: a chef can hold an ingredient and the knife at once. It's taken from and returned to a **knife rack** with A; **A always deals with the ingredient in hand first**, the knife second.
- **The mise en place counter replaces the board-with-rail idea:** a cutting board in the middle and **four slots** for prepared ingredients in the face-button diamond. A slot takes the first ingredient that arrives and is freed when empty; each holds 4 portions (equipment tier).
- **A** puts a whole ingredient on the board (or a prepared one back into its slot); with empty hands, **A takes from the board first, then from slot A**; **B / X / Y** take from their slots.
- **Holding the knife, X chops** whatever is on the board, **following a beat**: the first chop sets it, each chop is graded Good / Early / Late / Miss (±0.07 s good, ±0.14 s early/late), and a long pause restarts the beat with one miss. **Feedback:** a pulsing beat light, a progress bar of cuts and the last grade in colour.
- **An onion takes 8 chops at a 0.28 s beat** (≈2 s) and becomes a portion of **diced onion**, which goes into a slot (or stays on the board if none can take it). Diced onion is its own ingredient: it softens (80 °C) and cooks (92 °C core) in ~3–4 in-game minutes at full heat and browns slowly; scored on doneness (92 °C target), crust, heat and **cut evenness** (the rhythm score).
- **Steak rest** is scored: in-game seconds out of the pan until served, full marks at 180 s (3 real seconds). Slicing steak waits for orders in Phase 2.
- Two new score parts, **cut** and **rest**, each weighted 0.15.

**Supersedes:** the 3-slot rail on the board (`game-design.md` §6.2) and question 1 of the board design.

---

## 2026-11-02 — Pans are picked up with their food

**Decision:** With empty hands, A at a pan takes **the pan with its food** (no lifting food out on its own). At a plate, A again moves the food onto the plate and the empty pan stays in hand; the hot pan then goes back on a hob (or any free counter). A hob's prompt shows "take pan" / "put pan".

**Reason:** Your call after playtesting: there was no way to carry a pan that had food in it. Plating now reads as a real-kitchen move: take the pan, plate, put the pan back.

**Supersedes:** "empty hands take food out of a pan first" (milestone 2a).

---

## 2026-11-02 — The pass, serving temperature and dish scoring (milestone 5′)

**Decisions (proposed while building; flagged for review):**
- **Putting a plate with food on the pass serves it.** It's scored on the spot, a quality card shows above the pass, and the plate is collected after 4 s (tunable). Nothing can be taken back or added to a served plate.
- **Score per item, averaged over the dish:**
  - **doneness:** peak core temperature against the target; full marks within the window, falling to 0 over 8 °C beyond it;
  - **crust:** average browning of both sides; pale → 0, about 0.7–1.4 → full, then falling towards burnt; burnt → 0;
  - **serving temperature:** the food's average temperature on arrival; ≥ 52 °C full marks, ≤ 38 °C nothing;
  - **freshness**, as is.
  - Weights 0.4 / 0.25 / 0.25 / 0.1; parts that don't apply (a raw tomato has no doneness) are left out. Shown as 0–5 stars in halves.
- **Doneness target is a placeholder house standard, medium-rare (54.5 °C core),** until customer orders choose it in Phase 2.
- **The cooking precision dial sets the doneness window:** ±5 °C at its most generous, ±1.5 °C at its strictest (±3.25 °C at the default 0.5).
- **Steam** rises from hot food off the heat and fades as it cools, so a waiting dish visibly loses its heat.

**Reason:** Serving temperature is one of the game's differentiators; scoring it at the pass makes plating and walking promptly matter. The card stands in for customers until Phase 2, and lets us tune the maths first.

---

## 2026-11-02 — Storage units (milestone 4′)

**Decisions (built as agreed, plus details flagged for review):**
- A storage unit has **four slots, one per face button**, with bins on the unit in the same diamond (Y back, A front, X left, B right, as seen from the camera).
- **Empty hands: a face button takes one portion** from that slot (oldest first). **Full hands: A puts the food back** into the slot for its ingredient if there's room. Anything else gets the "can't" shake.
- **Stock is real, simulated food:** a **fridge keeps it at 4 °C**, a **shelf at kitchen temperature**, so fridge food stays fresh longer. Leftovers put back keep their state (temperature, doneness).
- **Starting setup (placeholder):** a fridge with steak ×6 and a shelf with tomato ×6 and onion ×6 on the west wall, replacing the crates. Stock refills when Play starts.
- **B and Y joined the Gameplay controls** (keyboard D and W) so storage can use all four slots; at other stations they do nothing yet (the "can't" shake).
- **Button prompts:** a small box in the player's colour above the station they face shows what each button does, with glyphs for their device (A/B/X/Y and LT/RT, or X/D/A/W and Q/E). Greybox UI for now.

---

## 2026-11-02 — In-world hob controls, one piece per pan, doneness chip, revised Phase 1 order

**Decisions:**
- **Hob controls in the kitchen view:** while facing a hob, the **triggers** turn the heat smoothly (keyboard **Q** down / **E** up) and **Use** (X; keyboard A) **flips** the food in the pan.
- **One piece of food per pan.** Simpler to read and control; Use always knows what to flip.
- **Doneness chip:** while a player faces a hob, a small chip floats above the pan in the colour of the food's middle (doneness) band. No numbers.
- **Revised Phase 1 milestones:** 3′ in-world hob controls (and remove the picture-in-picture code) → 4′ storage (4-slot fridge/shelf with the A/B/X/Y prompt) → 5′ pass with serving temperature and a quality card → 6′ board and onion (rhythm chopping, rail, sautéing; resting and slicing the steak). See `plans/phase-1.md`.

**Reason:** Your choices after dropping the close-up: one press per action, readable from the shared camera, with storage and serving temperature brought forward as the differentiators.

---

## 2026-11-02 — Drop the picture-in-picture close-up

**Decision:** No close-up panel. Cooking is done in the kitchen view, directly at the station. The game's differentiators are instead:
1. **Storage with capacity** (fridges and shelves that players buy, place and stock; see `ideas.md`), the main one.
2. **Serving temperature:** how fast a dish gets from the pass to the table matters, because food cools.
3. **Food keeps cooking** off the heat (carryover, resting), as the simulation already does.
4. **Hobs and grills with adjustable power**, not on/off.

**Reason:** A panel per player makes co-op busier and harder to manage on a shared screen. The four points above keep "cooking is a craft" through timing, heat and planning rather than through minigames.

**Supersedes:** the close-up station view (`CLAUDE.md`, `game-design.md` §3.2, Phase 1 milestones 3–5) and the close-up details below. The milestone 3 framework code (commit 5e7a1c0) is to be removed or reduced once the in-world replacement controls are agreed.

---

## 2026-11-02 — Close-up framework details (milestone 3)

**Decisions (proposed by me while building; flagged for review):**
- **Keyboard triggers:** Q = left trigger (heat down), E = right trigger (heat up), either side of W.
- **Heat is continuous:** the hob dial moves smoothly with the triggers (off to full in ~1.25 s at full trigger). Use at a hob now opens its close-up; the temporary off/low/medium/high steps are gone.
- **Flip moved into the close-up now (A)**, with the stick choosing which piece. The temporary "put it back to flip" rule is removed. Basting, resting and slicing stay in milestone 4.
- **Leaving is B only** (keyboard D / Esc). "Pull the stick away from the station" from the transition proposal doesn't work while the stick aims inside the close-up.
- **No kitchen dim yet.** "Dim your side of the screen" assumed fixed side panels; with the panel near the station there's no clear "side", and dimming would hide the partner. The panel has a border in the player's colour instead. Revisit after playtesting.
- **Edge ping:** the panel border pulses orange while food in any *other* pan is close to burning.
- **Second player at a busy station** gets the "can't" shake on Use.

**Reason:** Needed to make the framework playable; each is a tuning value or easy to change.

---

## 2026-11-02 — Phase 1: steak and onion controls, fridge replaces crates

**Decisions:**
- **Steak close-up controls:** triggers turn the heat down / up, **A** flips, **hold X** bastes, **B** leaves. Keyboard equivalents for the triggers are still to choose (the face buttons follow the W/A/D/X mirror).
- **Onion close-up controls:** the **stick** moves the knife along the onion, **A** chops, **Y** rotates 90° to turn slices into dice. Even spacing gives better quality; rhythm is rewarded but you can't fail.
- **The 4-slot fridge** with the button prompt and limited stock **replaces the crates in Phase 1** (milestone 6). Stock refills each time Play starts; restocking during Prep comes in Phase 3.

**Reason:** Recommended in the Phase 1 plan and accepted.

---

## 2026-11-02 — One time scale for everything: 1 real second = 1 in-game minute

**Decision:** The whole game runs on a single clock at **60x**: one in-game minute passes in one real second, so a 12–15 hour shift plays in 12–15 minutes. **Cooking follows the same scale:** a steak that takes ~8–10 minutes in reality cooks in ~8–10 seconds, resting 5 minutes takes 5 seconds, a 30-minute caramelised onion takes 30 seconds. More than ~15 seconds for a steak is too long.

**How it's built:** the simulation uses real-world units (°C, in-game seconds) and realistic-ish constants; gameplay feeds it `realDeltaTime × TimeScale`. The scale is one tuning value (default 60).

**Consequences:**
- Techniques are short and punchy. The steak's skill is a few well-timed decisions (when to flip, when to pull, when to baste), not a long minigame.
- Windows are tight at 60x, so the "forgiving by default" decision and the cooking precision dial matter more: precision sets how wide the doneness windows are.
- Long passive processes (stocks, braises, slow onions) become 30 s–few-minute tasks, which suits juggling them during service.

**Reason:** Your call: compressed skill games, consistent time. I raised the risk that 8–10 s leaves little room for sear/flip/baste. We'll judge it by feel in milestone 4; the scale is tunable if needed.

**Supersedes:** question 1 in the Phase 1 plan (I had proposed a separate, slower cooking speed).

---

## 2026-11-02 — Phase 1: pans and plates, reading doneness, close-up panel placement

**Decisions:**
- **Pans and plates are carryable containers.** A pan normally sits on a hob but can be carried to a plate; plates come from a plate stack.
- **Doneness is read mostly by eye:** sear colour, smoke and (later) sizzle. Exact numbers only in the debug overlay; a probe thermometer may come later as an easy-mode option.
- **The close-up panel sits close to the station being used** (rather than fixed in a screen corner), so it stays connected to the station it grew out of. When the other player moves behind it, the panel **fades to partly transparent** so both players can still see what's underneath. To be judged by feel; a fixed bottom-corner panel is the fallback.

**Reason:** Pans and plates make plating a physical step, like a real kitchen. Reading by eye keeps cooking a craft (pillar 1). Keeping the panel near its station makes the transition read naturally, and fading keeps the partner visible.

---

## 2026-11-02 — Greybox kitchen layout (Phase 0)

**Decision:** The greybox kitchen is 10 × 8 m, split into zones that follow the food: **storage** (west wall) → **prep** (back wall, left) → **cooking** (back wall, right) → **pass** (front, facing a dining strip along the south wall). A two-counter **island** in the middle is the hand-off point, and a **bin** sits on the east wall. Hob, pass and bin are plain counters until Phase 1. Each zone has its own greybox colour so it reads from the overhead camera.

**Reason:** Storage to hobs is about 8 m (~1.6 s at 5 m/s), long enough that carrying matters and short enough not to drag. The food path crosses the middle, so the two players naturally meet at the island. That's where coordination should happen. This is a starting point to playtest, not a final layout. Playtested and accepted the same day.

**Constraint for all code from now on:** players move stations around during Plan (Phase 4), so nothing may assume fixed station positions or a fixed set of stations. Today `KitchenCompositionRoot` collects stations once at scene start, and the builder places them; both need replacing in Phase 4 with stations that register and unregister as they're placed, and layouts loaded from the save. Targeting already reads positions every frame.

---

## 2026-11-02 — Keyboard: arrows to move, face buttons as a W/A/D/X diamond

**Decision:** The keyboard is mainly for testing; the controller is the main way to play. Keyboard bindings mirror the Xbox face buttons as a diamond around S:

| Xbox | Keyboard | Gameplay meaning |
| --- | --- | --- |
| A (bottom) | **X** | Interact, Join, UI Submit |
| B (right) | **D** | Close-up exit, Build/UI cancel |
| X (left) | **A** | Use |
| Y (top) | **W** | (free; storage slot later) |
| Left stick | **Arrow keys** | Move, UI navigate |
| Start | **Esc** | Pause (Esc also cancels/exits) |

Enter also joins and submits. WASD no longer moves.

**Reason:** One mental model for both devices: "press A" always means the same key, which matters once storage uses all four face buttons. Supersedes the E / F keys in "Interact and Use are separate buttons" and the WASD movement in "Player 1 on keyboard". Rebinding in Controls is still planned.

---

## 2026-11-02 — Phase 0 interaction rules

**Decision:** For Phase 0 (accepted after playtesting pick up and place):
- Interact is contextual: empty hands pick up, full hands put down.
- Crates don't take items back; food goes down on counters.
- No swapping: full hands at a full counter does nothing (and the counter gives a small "can't" shake).
- Items can't be put on the floor.
- The targeted station is tinted towards the targeting player's colour.

**Reason:** Simple and readable to start with. Swapping is the rule most worth revisiting once there's more to juggle. The crates are placeholders anyway (see the storage idea in `ideas.md`).

---

## 2026-11-02 — Unity 6.3 LTS (6000.3.27f2)

**Decision:** The project uses Unity 6.3 LTS, 6000.3.27f2, created from Unity's 3D URP template. We stay on LTS releases and only move to the next LTS once it's out and stable, as a deliberate step.

**Reason:** `CLAUDE.md` asks for Unity 6 LTS. Unity Hub recommends 6.6, but that's a "Supported" release with a much shorter support window; LTS gets two years of fixes, which suits a long project.

---

## 2026-11-02 — Simulation has no UnityEngine references; designer data lives in `Game.Data`

**Decision:** `Game.Simulation` is compiled with no `UnityEngine` references at all. ScriptableObject definitions (`TuningConfig`, `IngredientDefinition`, etc.) live in a thin `Game.Data` assembly that references Simulation and converts its assets into plain C# settings objects passed into the simulation. Dependencies: Presentation → Gameplay → Data → Simulation.

**Reason:** Keeps the simulation pure, deterministic and fast to test, and the compiler enforces it. `CLAUDE.md` asked for "as little `UnityEngine` as possible"; this makes it none. The cost is one extra assembly and a small mapping step per definition.

---

## 2026-11-02 — Player 1 on keyboard, player 2 on controller; controls are rebindable in game

**Decision:** The default setup is player 1 on keyboard and mouse and player 2 on an Xbox controller. Any player can still join on a controller instead. There is no split-keyboard (two players on one keyboard) mode. A **Controls** screen in the in-game settings lets players rebind every action for keyboard and gamepad, with a reset-to-defaults option. Rebinds are stored in a small settings file, separate from restaurant saves.

**Reason:** Matches how the game will actually be played on a MacBook. Rebinding helps accessibility, and players can see what every button does. The Input System supports interactive rebinding and JSON binding overrides, so the cost is mostly UI.

---

## 2026-11-02 — Movement is snappy, no dash for now

**Decision:** Player movement reaches full speed in about 0.08 s and turns fast, Overcooked-style. All values live in `TuningConfig`. No dash in Phase 0; revisit after Phase 2 if walking feels slow during service.

**Reason:** Inputs must feel instant (`CLAUDE.md`). A dash is easy to add later but changes kitchen pacing and layout, so we judge it once service exists.

---

## 2026-11-02 — Interact and Use are separate buttons

**Decision:** **Interact** (gamepad A; keyboard X since the keyboard remap below) is a contextual pick up / place. **Use** (gamepad X; keyboard A) uses a station or enters its close-up. Use is bound from Phase 0 but does nothing until Phase 1.

**Reason:** Keeping the two apart stops a player who wants to put something down from accidentally entering a close-up, and Phase 1 slots in without remapping.

---

## 2026-11-02 — Board rail: full rail leaves the cut on the board

**Decision:** If every rail container is full (or none matches) when a cut finishes, the cut product stays on the board surface. The board can't start a new cut until it's cleared.

**Reason:** The pressure is visible from the overhead camera and fair, and nothing moves into the player's hands without them asking. The other options either surprised the player (auto-pickup) or felt like the game blocking them (refusing to start a cut).

---

## 2026-11-02 — Board rail: one ingredient and one cut per container

**Decision:** Each rail container holds exactly one ingredient in one cut (diced onion and sliced onion need separate containers).

**Reason:** Easy to read from overhead, easy to score, and it avoids cross-contamination edge cases. Mixes (e.g. a soffritto) can be revisited with Spanish home cooking in Phase 6 if a cuisine needs them.

---

## 2026-11-02 — Board rail: 3 slots to start, more via equipment tiers

**Decision:** The board station has a rail of small containers for cut product. The starting rail has 3 slots; equipment tiers add more (rough guide 3 → 4 → 6, to tune). The slot count comes from data (`StationDefinition` / `EquipmentTier`), not code.

**Reason:** Three slots is tight enough that prep choices matter in the greybox, and upgrades give a visible payoff later. Accepts the board-rail idea from `ideas.md`.

---

## 2026-11-02 — Close-up techniques are forgiving; precision raises quality

**Decision:** By default, sloppy input still completes a technique but lowers its quality (uneven dice, weaker sear). Techniques are hard to fail outright. The minigame complexity dial can make them stricter.

**Reason:** Keeps "cooking is a craft" (precision is rewarded) without punishing new players with ruined food from a slightly off stick input, in line with the forgiving-input rule in `CLAUDE.md`.

---

## 2026-11-02 — One player per station close-up

**Decision:** Only one player can be in a station's close-up at a time. A second player who tries to enter sees the station marked busy and is gently blocked.

**Reason:** Simple to build and easy to read. It also makes layout matter: more boards and hobs mean more parallel work. A shared surface (one cuts, the other plates) would be a fun co-op moment but complicates input and rendering a lot; we can revisit it later.

---

## 2026-11-02 — Kitchen view dims slightly on the close-up player's side

**Decision:** While a player is in close-up, the kitchen view gets a subtle dim or vignette on that player's side of the screen. The rest of the kitchen stays fully visible. The strength is a tuning value.

**Reason:** Makes it clear where the player's focus is, while keeping pings, the partner and the rest of the kitchen readable, since the world keeps running.

---

## 2026-11-02 — Fixed kitchen camera now; framing camera only if size tiers need it

**Decision:** Phases 0–3 use a fixed overhead camera on the greybox kitchen. A camera that frames both players and zooms is deferred to Phase 4, and built only if larger size tiers don't fit a fixed view.

**Reason:** A fixed camera is the most readable and the least work, and a small kitchen doesn't need framing. Framing can feel floaty, so we add it only once we know it's needed.

---

## 2026-11-02 — Local co-op is 1–2 players; 4 players is a stretch goal

**Decision:** The game targets 1–2 local players. Support for 3–4 players is a stretch goal, not a requirement for any roadmap phase.

**Reason:**
- The close-up station view (picture-in-picture) can be large with two players (roughly a third of the screen per player) and stays readable. With four players, panels would be too small for precise stick and combo techniques.
- The 60 fps target on a MacBook is easier with two close-up cameras rendering to RenderTextures instead of four.
- Co-op gets tighter with two players, with natural roles (e.g. line cook vs. pass/front of house).
- Larger restaurants have to lean on hired staff, which makes the staff system matter more.

**Cost:** We lose the 3–4 player couch-party audience that PlateUp and Overcooked serve. Revisit once the 2-player game is fun.

**Implications:** Code shouldn't hard-code a limit of two where that's cheap to avoid (e.g. `PlayerInputManager` max players comes from config), but UI layout, camera framing and performance budgets are designed and tested for two.
