# Decisions

Design and technical decisions, newest first. Each entry: date, decision, reason.

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
