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
- **No roguelite runs.** There is one campaign at a time, saved as each Review starts. Failure costs money, reputation, staff morale, appliances or restaurants. A campaign ends only after the boss's full escalation (§15.3), and the cookbook survives it.

### What we're reacting against

- In PlateUp and Overcooked, cooking is mostly "put item on station, wait for bar." We want the bar replaced by food you read and judge.
- Carrying is dead time: items are tokens, one per trip, with no skill involved (see §6).
- PlateUp's runs reset as routine. Ours don't: the restaurant is yours, and losing it takes a long, visible slide.

---

## 2. Players and platform

- **1–4 local players** (decision 2026-11-09; it was 1–2, with 3–4 a stretch goal).
- macOS (Apple Silicon) first. Default: player 1 on keyboard, player 2 on an Xbox controller over Bluetooth. Every action has both bindings, and all controls can be rebound from a Controls screen in the settings.
- Movement is snappy (full speed in about 0.08 s), with no dash for now. **Interact** (A) picks up and places; **Use** (X) works a station or enters its close-up. The keyboard mirrors the face buttons as W/A/D/X with arrow keys to move, and is mainly for testing.
- Join by pressing a button (drop-in during Plan/Prep). **Open:** can a second player join mid-service?
- Performance target: steady 60 fps on a MacBook with four players.

**Open:** how solo play scales. A solo player can't be in two places, so either service pacing scales with player count, or solo players lean on staff earlier. Recommendation: scale customer arrival rate and patience via tuning per player count, and keep staff as an option rather than a requirement.

---

## 3. Camera and views

### 3.1 Kitchen view

A **shared overhead camera** frames the whole restaurant. Both players see the full kitchen so they can read bottlenecks, plan routes and see their layout pay off. Readability from this camera is a constraint on every asset and food state.

**Decided:** the camera follows the action, keeping every player and customer in view and zooming as they spread apart or gather (decision 2026-11-05).

### 3.2 Close-up station view (dropped 2026-11-02)

> **Dropped:** too busy for co-op on a shared screen. Cooking happens at the station in the kitchen view. See `decisions.md`. The text below is kept for reference.

Approaching a station and interacting opens an **overhead close-up of the station's surface** in that player's own screen panel, picture-in-picture by default. Techniques are performed here with button combos and precise stick input. **The world keeps running**: food on other stations keeps cooking and customers keep waiting.

With two players, each close-up panel can be large (roughly a third of the screen, on that player's side).

**The transition** (from `ideas.md`; built in Phase 1 milestone 3, see `decisions.md` for what changed: exit is B only, no kitchen dim yet):
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
| **Prep** | Clean up yesterday's mess. Mise en place: stock station storage and board rails, pre-cut, make stocks and sauces, preheat | Soft (a clock, but no customers) |
| **Service** | Customers arrive, order, wait, eat, pay and review | High |
| **Close** | Store leftovers, deal with waste and spoilage | Low |
| **Review** | A read-only dashboard scene, starting as soon as the last customer leaves: the service, customers, dishes, money lost and earned, overall progress | None |

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

- Hobs are **dial-controlled**, not on/off. **Kinds of heat** differ in how they're controlled (decided 2026-11-07): a **gas hob** turns smoothly and the pan takes time to heat and cool; an **induction hob** has three steps the pan reaches almost at once; a **charcoal grill** has a hot and a cool zone, always lit, and food is moved between them rather than turned down. None of them changes the quality score: they change how you cook, not how good the result is.
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

Hob, board, fridge, pass, bin. More stations arrive with cuisines and as kinds of heat (§9).

**Since 2026-11-07:** the cutting board and the knife rack sit on counters and take each one over; they're moved only in management mode. The kitchen starts with one knife lying on a counter and no rack, and chopping needs the knife.

**Proposal — storage with capacity** (`ideas.md`): ingredients come from fridges and shelves with limited capacity rather than endless crates, so where and how things are stored is planned in Plan/Prep. The Phase 0 crates are placeholders for this.

### 6.2 Board station with mise en place rail (superseded 2026-11-02)

> **Superseded** by the mise en place counter (four slots, knife held in a tool slot, chopping to a beat), then by a board that only cuts (2026-11-04) and sits on a counter (2026-11-07); see `decisions.md`. The rail slots below, and equipment tiers growing them, no longer apply.

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

- A **listing, like a Google listing,** shows what customers say, line by line. It opens on a shared screen from a station players buy (§15.6).
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

Cuisines arrive one at a time in Phase 7. The first cuisine should come out of the Phase 1 steak and onion work.

### 8.2 Service styles

- **Fast casual:** high volume, low prices, low patience for waiting, low expectations for plating.
- **Bistro:** the middle ground.
- **Fine dining:** few covers, high prices, demanding quality and plating.

Service style is chosen per restaurant, separately from cuisine, and any combination is allowed. Reviews and tips judge how well it works, so fine-dining pizza is possible. Service style sets most expectations, and each cuisine adds a few of its own. Unusual combinations attract fewer regular customers but more influencers (to try in play).

---

## 9. Restaurant building

- **Size comes from which restaurant you buy**, from a counter up to a full dining room, not from upgrading one. Bigger isn't strictly better: more covers, but more rent, more trips and more need for staff.
- **Kitchen zones:** layout matters. Stations, storage and the pass can be placed to cut trips.
- **Kinds of heat, not equipment tiers** (decided 2026-11-07): gas hob, induction hob and charcoal grill now; electric and wood ovens with the cuisines that need them. Each is bought in the shop and has its own energy cost, paid daily: electricity cheapest, then gas, then charcoal. The grill's glow is also part of the room's look.
- **Kitchenware:** cutting boards, knife racks, knives and pans are bought too. Boards and racks use up a counter each; knives and pans appear on a free counter. Pots, skewers and oven trays arrive with their dishes.
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

- **Suppliers:** street vendors near the restaurant, cheap vs. premium. Ingredient quality affects dish quality and freshness. Style decides what they sell (§15.5).
- **Menu pricing:** set by the players; affects demand and expectations.
- **Costs:** rent (scales with the restaurant's size), wages, equipment.
- **Debt:** players can sell, fire, downsize or borrow more from the boss. Falling behind on what he's owed starts the escalation in §15.3.
- **Cookbook** (replaces the chef profile): sets the menu (§15.5), lasts across campaigns and stays in the boss's office for the next chef, whose carried-over recipes work at any restaurant.

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
5. How reviews and tips judge cuisine and service style combinations (§8.2, `ideas.md`)
6. How staff are directed (§10)
7. How customer forecasts are shown in Prep (§15.5, `ideas.md`)
8. Realism presets (§12)

Also still open: whether a second player can join mid-service (§2). The close-up transition (§3.2) and the three ways to carry (§6.3) are proposals waiting for a decision.

Settled 2026-11-02 (see `decisions.md`): kitchen camera, close-up dimming, one player per station, technique forgiveness, board rail slots, mixing and overflow.

---

## 15. Campaign, story and the first day

Decided 2026-11-06 (see `decisions.md`). Questions still open are in `ideas.md`.

### 15.1 The premise

The boss, a Texas oil oligarch who always has two bodyguards with him, is convinced restaurants are more profitable than ever: young people photograph their food, review it and post it, content creators compete to feature places, and word of mouth has never been stronger. He wants a gastro-empire in town. He lends the player money to buy old restaurants and turn them around, and takes a cut.

### 15.2 The boss's office

The game opens in the office, a lobby where players join, walk around and change their character. A city miniature there is used to buy restaurants and move between them. Each business on transfer shows its price, size, previous style and decoration (which players can change), old customer reviews and inherited appliances. At first players can only afford the smallest, a grill. When buying, players choose its style and either a new name or the previous owner's. Keeping the name keeps the old reviews, and a new name starts with none. The first restaurant is the exception: it's a grill with a fixed service style, and players choose only its name. Vendors and scenery around each restaurant should suit its location.

### 15.3 Money trouble

Players can sell things, fire staff, downsize, or go back to the office and borrow more. His collection is worked out as Review starts and shown on the dashboard, and anything he does in person happens the next morning. If they can't pay what he's owed, it escalates, one step per missed collection; paying in full resets it (decided 2026-11-10):

1. The boss visits and warns them.
2. His bodyguards repossess appliances, on screen.
3. He takes a restaurant.
4. They're erased. Money and restaurants go, and the cookbook stays in his office for the next chef.

### 15.4 Time, saves and the ending

- **Three campaign slots,** each a separate campaign (decided 2026-11-08). The game saves as Review starts and whenever anything is done in the office, such as buying a restaurant or borrowing. Leaving a restaurant at any other point in the day loses that day's progress, and players are clearly told so first.
- **Each chef's look lives in a player profile that outlives campaigns,** like the cookbook. Players change it at the tailor in the boss's office, and it saves at once. Player colour is fixed by slot, and crazy outfits and hats are earned (decided 2026-11-09, see `decisions.md`).
- **A restaurant only runs while players are in it.** The others sit idle.
- **Fees come due every 5 finished days played in that restaurant** (decided 2026-11-08), and that collection day also sets the period's goal. A collection covers the restaurant's cut plus one instalment of the loan (decided 2026-11-10).
- **The ending:** once every restaurant on the miniature is unlocked, players take over the empire. The bodyguards follow player 1 and guard the door from outside in restaurants. This is when players really make money, and can hire and decorate more heavily.

### 15.5 Recipes and stock

The **cookbook** opens from the management menu. A restaurant's style decides which ingredients the market sells and which recipes the cookbook offers. Players browse recipes freely and pay to unlock the ones they choose. That configures the menu, and customers can order those recipes from then on. Each recipe shows its complexity, the appliances and ingredients it depends on, its approximate cost and the value it generates. Harder recipes pay more. New mechanics are introduced progressively as new recipes and ingredients arrive.

Players must keep what their menu needs in stock. A customer who orders something out of stock loses patience as usual. Players can leave during service to buy more, at the cost of time, so Prep is where they plan around the approximate number of customers expected.

Recipes carried over from an earlier campaign work at any restaurant, ignoring its style's limits.

### 15.6 Two devices

- **The personal mobile** is how the boss calls, shown in the bottom right corner.
- **The restaurant station** (laptop, phone or tablet) is a bought appliance, introduced around day 2. It opens the listing on a shared screen, explains reviews in more depth, offers quick-restock delivery of a limited range, and switches restaurants without a trip to the office. It may gain other perks.

### 15.7 The first day

Every step should be fun from the start and very light. Tasks appear one at a time in the top right corner, each with its own control hints, and any player can complete them. Returning players can skip this.

| Step | What happens | What it teaches |
| --- | --- | --- |
| **Office** | Join, change character, meet the boss, buy the grill on the miniature | The goal and the debt |
| **Clean up** | Look round the inherited restaurant and clean dirt on floors, tables and counters | Moving, Use, what's in the restaurant |
| **The call** | The boss phones: he's coming with friends, so they'll need enough plates | The goal for the day |
| **First appliance** | There are no plates at all, so players open management mode (Select) and buy a plate pile | Management mode and buying |
| **The cookbook** | The boss asks for the dish he wants, onion steak. Players find it in the cookbook and see it needs steaks and onions | The cookbook and the menu |
| **Vendors** | Leave the restaurant and buy food from the street vendors | Shopping and budget |
| **Getting ready** | Stock the fridge and shelf, and prep the onions | Storage and prep |
| **The boss arrives** | He's alone, as his friends made a cheap excuse. He sits and eats while the bodyguards stand behind him | Cooking, orders, serving |
| **The bodyguards** | He invites them to sit, and two more dishes are ordered. Both leave a review | Parallel orders on one hob, reviews |
| **Review, then day 1** | The day closes and a new one starts, with players on their own | The daily loop |

**Starting kit:** a couple of regular 4-seat tables, a hob, cutting board, shelf and fridge, plus basics such as the sink, bin, counters and pass. No plates.

### 15.8 Cleaning

Dirt can be on floors, tables and counters, and is cleaned the same way everywhere: approach it, hold X and move the stick to match a pattern. The chef uses a mop or a cloth depending on the surface. It should be fun, light, and different from the sink's balancing game. Review starts as soon as the last customer leaves, so a service's mess waits until the next morning's Prep, and how much there is depends on how the service went.

### 15.9 Decoration

Decoration affects reviews and the profile of customers who come. The food can be delicious while the place doesn't feel original, clean, or in keeping with its style.

It covers furniture and objects as well as floor patterns, wall colours and windows.

Decoration should match the style, and players aren't told what that means. They work it out from reviews such as "great steak, but I expected candles", shown in Review and in the tablet's history during Prep. Fine dining expects bigger tables, lamps, plants, art, solemn colours and more staff. A burger place values functionality such as bar tables, bins and game stations, and more eclectic decoration.

### 15.10 Changing style

Players can change a restaurant's style at any time from the tablet station. It's a real investment: furniture that doesn't suit the new style has to be sold, new decoration bought, and reviews suffer until the place matches.
