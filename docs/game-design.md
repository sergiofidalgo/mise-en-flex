# Game Design — Mise en Flex (working title)

The full design. `CLAUDE.md` is the condensed technical version; if the two conflict, raise it.

**Status:** first draft, 2026-11-02. Built from `CLAUDE.md` and early design conversations. Anything marked **Open** is undecided; anything marked **Proposal** comes from `ideas.md` and still needs a decision. Settled calls live in `decisions.md`.

---

## 1. Vision

A local co-op restaurant game in the spirit of PlateUp, where cooking is a hands-on craft and the restaurant grows over time instead of resetting. Two friends on a couch run a kitchen they built themselves, and get visibly better at it, both as cooks and as a team.

### Design pillars

Every feature must serve at least one.

1. **Cooking is a craft, not a button.** Food changes continuously. Skill shows in timing and judgment, not in holding a button until a bar fills.
2. **Your restaurant, your identity.** Cuisine, size and service style produce genuinely different games, not reskins.
3. **Chaos you can plan for.** Service is frantic, but preparation, layout and staffing visibly pay off.

### Hard rules

- **No automated pipelines.** No conveyors, auto-cookers or grabbers. Help comes from hired staff, who are people with skill, speed, fatigue and mistakes. Staff never perform signature techniques.
- **No roguelite resets.** Each restaurant is a persistent save slot. Failure costs money, reputation or staff morale, or forces debt or downsizing, but never all progress.

### What we're reacting against

- In PlateUp and Overcooked, cooking is mostly "put item on station, wait for bar." We want the bar replaced by food you read and judge.
- Carrying is dead time: items are tokens, one per trip, with no skill involved (see §6).
- PlateUp's runs reset. Ours don't: the restaurant is yours.

---

## 2. Players and platform

- **1–2 local players** (decision 2026-11-02). 3–4 players is a stretch goal.
- macOS (Apple Silicon) first. Default: player 1 on keyboard, player 2 on an Xbox controller over Bluetooth. Every action has both bindings, and all controls can be rebound from a Controls screen in the settings.
- Movement is snappy (full speed in about 0.08 s), with no dash for now. **Interact** (A) picks up and places; **Use** (X) works a station or enters its close-up. The keyboard mirrors the face buttons as W/A/D/X with arrow keys to move, and is mainly for testing.
- Join by pressing a button (drop-in during Plan/Prep). **Open:** can a second player join mid-service?
- Performance target: steady 60 fps on a MacBook with two players, including both close-up views.

**Open:** how solo play scales. A solo player can't be in two places, so either service pacing scales with player count, or solo players lean on staff earlier. Recommendation: scale customer arrival rate and patience via tuning per player count, and keep staff as an option rather than a requirement.

---

## 3. Camera and views

### 3.1 Kitchen view

A **shared overhead camera** frames the whole restaurant. Both players see the full kitchen so they can read bottlenecks, plan routes and see their layout pay off. Readability from this camera is a constraint on every asset and food state.

**Decided:** fixed camera for Phases 0–3. A camera that frames both players and zooms is deferred to Phase 4, only if larger size tiers need it.

### 3.2 Close-up station view

Approaching a station and interacting opens an **overhead close-up of the station's surface** in that player's own screen panel, picture-in-picture by default. Techniques are performed here with button combos and precise stick input. **The world keeps running**: food on other stations keeps cooking and customers keep waiting.

With two players, each close-up panel can be large (roughly a third of the screen, on that player's side).

**Proposal — the transition** (`ideas.md`):
- The panel grows out of the station's screen position (~0.2–0.3 s) and shrinks back on exit.
- Switch to the `CloseUp` action map at the start of the transition and buffer inputs; no dead frames.
- Exit is cheap: stick away from the station, or B (keyboard D / Esc).
- The panel edge pings for events that need the player (pan smoking, new ticket, partner calling).
- The avatar visibly performs the technique in the kitchen view, so the partner can read it.

**Decided:**
- While a player is in close-up, the kitchen view dims slightly on their side of the screen (strength is a tuning value). The rest stays fully visible.
- One player per station close-up. A second player sees the station marked busy and is gently blocked.

---

## 4. The daily loop

Each in-game day runs **Plan → Prep → Service → Close → Review**, then the next day.

| Phase | What happens | Time pressure |
| --- | --- | --- |
| **Plan** | Set the menu and prices, order from suppliers, hire/fire staff, edit layout (each player has their own cursor in a shared view) | None |
| **Prep** | Mise en place: stock station storage and board rails, pre-cut, make stocks and sauces, preheat | Soft (a clock, but no customers) |
| **Service** | Customers arrive, order, wait, eat, pay and review | High |
| **Close** | Clean, store leftovers, deal with waste and spoilage | Low |
| **Review** | Money, reputation, reviews, staff morale, what went wrong | None |

**Open:** length of a service in real minutes (recommendation: start around 6–8 minutes and tune); whether Prep has a hard time limit or ends when players ready up; whether Close is played or summarised.

---

## 5. Cooking simulation

### 5.1 Ingredient state

Every ingredient instance tracks, continuously:
- **Form:** whole, sliced, diced, minced, etc. Changes through techniques.
- **Doneness:** a continuous value (raw → overdone/burnt), per ingredient, possibly per side.
- **Temperature:** internal and surface; drives doneness and serving temperature.
- **Freshness:** decays over time, faster out of refrigeration (affected by the spoilage dial).
- **Seasoning:** amount and distribution.

### 5.2 Heat is a resource

- Hobs are **dial-controlled**, not on/off.
- **Preheating** matters: a cold pan doesn't sear.
- **Pan crowding** drops the pan's temperature.
- **Carryover cooking:** food keeps cooking after leaving the heat.
- **Resting:** meat needs rest before cutting/serving; cutting too early costs quality.

### 5.3 Active vs. passive processes

- **Active:** the player is in the close-up performing a technique (chopping, flipping, basting).
- **Passive:** something cooks on its own (simmering, roasting) and must be checked and pulled at the right time.

Good play means juggling a few passive processes while doing active work, which is where the close-up transition and its pings matter most.

### 5.4 Techniques

Each ingredient brings its own mechanic in the close-up view. First techniques (Phase 1):
- **Steak:** sear, flip, baste, rest, slice. Skill: reading doneness and managing heat.
- **Onion:** slice, dice, mince on the board. Skill: rhythm and precision; cut size affects cooking.

**Decided:** techniques are forgiving by default. Sloppy input still completes the technique but lowers quality (uneven dice, weaker sear), and outright failure is rare. The minigame complexity dial (§12) can make them stricter. The exact default still needs playtesting.

---

## 6. Stations, items and carrying

### 6.1 Core stations (Phase 1)

Hob, board, fridge, pass, bin. More stations arrive with cuisines and equipment tiers.

**Proposal — storage with capacity** (`ideas.md`): ingredients come from fridges and shelves with limited capacity rather than endless crates, so where and how things are stored is planned in Plan/Prep. The Phase 0 crates are placeholders for this.

### 6.2 Board station with mise en place rail

The board station is the board plus a **rail of small containers** (ninth-pans). Cut product goes from the board into a container. From the overhead camera, each container shows its contents and cut by shape and colour. Containers are grabbable, so the partner can fetch diced onion without entering the close-up. The rail's state at the start of service shows directly how well the team prepped.

**Decided:**
- The starting rail has **3 slots**; equipment tiers add more (rough guide 3 → 4 → 6, to tune). Slot count comes from data.
- **One ingredient and one cut per container.** Mixes may return with a cuisine that needs them.
- **Full rail:** the cut product stays on the board, and the board can't start a new cut until it's cleared.

### 6.3 Carrying (Proposal)

Inspired by real kitchens, simplified into **three ways to carry**:
1. **Hands:** one item, fastest.
2. **Container:** one ingredient, many portions, carried as one item.
3. **Tray / plate stack:** mixed items or up to 3 plates; slower, and can spill on sharp turns.

Supporting ideas:
- **Station storage** (like lowboy fridges) filled during Prep cuts walk-in trips during Service.
- **Food keeps simulating while carried:** hot pans keep cooking and cool, plates lose serving temperature, raw meat on hands cross-contaminates.
- **Kitchen calls:** automatic "Behind!" / "Hot!" barks and soft collision avoidance when passing with hot items.
- **The pass as a handoff:** put food down, ring the bell, partner or runner takes it.
- **Bus tubs** for clearing tables.

---

## 7. Quality, orders and customers

### 7.1 Quality score

Each dish gets a score from:
- Doneness (vs. target, including customer requests)
- Serving temperature
- Seasoning
- Freshness
- Plating

Quality drives payment, tips and reviews. **Open:** the scoring formula and weights, and how much of the score the player sees during service (recommendation: no numbers during service, readable food states instead; full breakdown in Review).

### 7.2 Customers

- **Custom orders:** "medium-rare", "no onion", etc.
- **Allergies and cross-contamination:** a real risk from shared boards, hands and pans.
- **Patience:** decreases while waiting; the patience dial scales it.

### 7.3 Reputation

- An **in-game review app** shows what customers say, line by line.
- **Incognito critics** visit unannounced and weigh heavily.
- **Influencers** amplify a visit, for better or worse.

---

## 8. Restaurant identity

### 8.1 Cuisines

Each cuisine has a **signature mechanic** that staff never perform:

| Cuisine | Signature mechanic |
| --- | --- |
| Steakhouse | **Open** — likely heat management and doneness reading |
| Sushi | **Open** |
| French bistro | **Open** |
| Pizzeria | **Open** |
| Spanish home cooking | **Open** |
| Wok | **Open** |

Cuisines arrive one at a time in Phase 6. The first cuisine should come out of the Phase 1 steak and onion work.

### 8.2 Service styles

- **Fast casual:** high volume, low prices, low patience for waiting, low expectations for plating.
- **Bistro:** the middle ground.
- **Fine dining:** few covers, high prices, demanding quality and plating.

**Open:** whether service style is chosen per restaurant or per day.

---

## 9. Restaurant building

- **Size tiers**, from a counter up to a full dining room. Bigger isn't strictly better: more covers, but more rent, more trips and more need for staff.
- **Kitchen zones:** layout matters. Stations, storage and the pass can be placed to cut trips.
- **Equipment tiers:** better hobs, bigger rails, more storage.
- **Health inspections:** hygiene, storage and cross-contamination practices (scaled by the hygiene dial).

Layout editing happens in Plan, with a per-player cursor in a shared view. **Everything in the kitchen can be moved there**: stations, counters, storage units and the pass. The greybox layout is only a starting arrangement.

---

## 10. Staff

Hireable staff (Phase 3): **dishwasher, waiter, runner, prep assistant**.

- Staff are people: they have skill, speed, fatigue and make mistakes.
- Staff never perform signature techniques.
- Staff morale responds to workload and how service goes.
- Staff are the answer to "the restaurant is too big for two people," and the only form of automation.

**Open:** how staff are directed (fixed roles vs. assigned tasks); wages and how morale affects them.

---

## 11. Economy and progression

- **Suppliers:** cheap vs. premium. Ingredient quality affects dish quality and freshness.
- **Menu pricing:** set by the players; affects demand and expectations.
- **Costs:** rent (scales with size tier), wages, equipment.
- **Debt:** failure can force borrowing or downsizing, never a reset.
- **Chef profile:** shared across all restaurants. Keeps learned recipes and techniques, so starting a new restaurant isn't starting from zero.

**Open:** what exactly carries over in the chef profile (recipes, technique unlocks, cosmetics?), and whether the two players share one profile or have their own.

---

## 12. Realism dials

Separate settings, read by the simulation through `RealismSettings`:

| Dial | What it scales |
| --- | --- |
| Cooking precision | How narrow the doneness and timing windows are |
| Spoilage | How fast freshness decays |
| Customer patience | How long customers wait |
| Minigame complexity | How demanding close-up inputs are |
| Hygiene | How strict cross-contamination and inspections are |

**Open:** presets (e.g. Relaxed / Standard / Chef) on top of individual dials. Recommendation: yes, with individual dials under an "advanced" option.

---

## 13. Feel and feedback

- Inputs respond instantly; interactions forgive slightly imprecise timing and positioning.
- Every action gives feedback: animation, sound and a visual cue.
- **Food states are read from the food itself**, not from progress bars: sear colour, char, moisture, sauce coverage, driven by shader parameters.
- Sound carries information: a sizzle that changes with pan temperature, a smoke alarm before burning.

---

## 14. Open questions summary

Collected from above, for discussion:

1. How solo play is balanced (§2)
2. Service length; Prep time limit; is Close played? (§4)
3. Quality formula, weights and in-service visibility (§7.1)
4. Signature mechanic for each cuisine (§8.1)
5. Service style per restaurant or per day (§8.2)
6. How staff are directed (§10)
7. What the chef profile keeps; shared or per player (§11)
8. Realism presets (§12)

Also still open: whether a second player can join mid-service (§2). The close-up transition (§3.2) and the three ways to carry (§6.3) are proposals waiting for a decision.

Settled 2026-11-02 (see `decisions.md`): kitchen camera, close-up dimming, one player per station, technique forgiveness, board rail slots, mixing and overflow.
