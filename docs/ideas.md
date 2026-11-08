# Ideas

Ideas worth keeping that aren't decided or scheduled yet. When one is accepted, record it in `decisions.md` and note which roadmap phase picks it up.

---

## Dishes that ask for onion rings

*Added 2026-11-08 · Relevant phase: 7 (content) · Pillars: cooking is a craft*

An onion now cuts into rings and then on into dice (`decisions.md`, 2026-11-08), but no dish on the menu uses rings yet, so for now there's no reason to stop at rings. A dish that asks for rings (a burger, a steak with fried rings) would make stopping a real choice. A customer asking for rings and getting dice, or the reverse, should cost quality.

---

## Appliances and kitchenware that wait for their dishes

*Added 2026-11-07 · Relevant phase: 7 (cuisines), with grill demand in 6–7 (expectations) · Pillars: cooking is a craft; your restaurant, your identity*

Kinds of heat replaced equipment tiers (`decisions.md`, 2026-11-07). These need dishes or systems that don't exist yet, so each arrives with the content that uses it:
- **Electric oven:** cakes and bread. Mostly passive cooking, which the simulation doesn't model yet.
- **Wood oven:** pizza and bread. Likely the pizzeria's signature mechanic.
- **Pots:** pasta and sauces. They need water and liquids in the cooking simulation.
- **Skewers:** kebab-style dishes, on the charcoal grill.
- **Oven trays:** with the ovens.
- **Customers asking for a grill:** reviews in a restaurant that specialises in meat mention the missing flames. Needs cuisine and service expectations.

---

## Players reshape the rooms inside their restaurant

*Added 2026-11-08 · Relevant phase: 6 (with decoration) · Pillar: your restaurant, your identity*

**The idea (user's):** players customise the restaurant to their needs and taste, including how the floor plan is split into rooms, as well as the walls' and floors' look (the look is already planned as decoration, §15.9).

**Proposed shape:**
- **The outer walls stay fixed:** they're the building that was bought, so size still comes from which restaurant you buy.
- **Inside them,** in management mode: move the wall between kitchen and dining room (a bigger kitchen means a smaller dining room), and move the door between them.
- **Later:** extra rooms (a storeroom), and knocking through an outer wall to grow a restaurant you own (see "Expanding a restaurant you own" below).

**What it touches:** the floor plan is two fixed rectangles today, and placement rules depend on which room something is in; customers and staff route through the one inner door; the camera assumes the kitchen faces the dining room to the south (`decisions.md`, camera notes).

**Answered (user's calls, 2026-11-08):**
- **Moving or building walls is free.**
- **Furniture in the way is the players' to deal with:** a wall can't go through an appliance, so they move appliances first to avoid clashes.
- **Walls can run at 45°,** as furniture can turn by 45°, so rooms aren't limited to rectangles.

- **Only in management mode,** like moving furniture.
- **Every room is at least 4 × 4 floor slots** (4 × 4 m).
- **The camera keeps its angle and behaviour whatever the layout.** Players see their layout from the same view as always and judge for themselves whether it plays well from there. That's deliberate: it's part of the choice, not something the camera adapts to.

**Still open:** how 45° walls meet customers' and staff's routes, and what the camera's "kitchen faces the dining room to the south" assumption becomes when the kitchen is elsewhere (it keeps its angle; only what it frames needs the floor plan's data).

---

## The prompt card hides a hob's heat pips

*Added 2026-11-07 · Relevant phase: 9 (HUD polish), or sooner if it gets in the way · Pillar: cooking is a craft*

**The problem (user's finding, from play):** while a chef faces a hob, the prompt card (heat, flip, take pan) and the flip gauge cover the hob. The card sits right over the pips along its back, so heat can't be read just when the player is adjusting it. The chef's body isn't what hides them.

**Options to weigh when the HUD is reworked:**
- **Move the card** clear of the station, for example to the side, or shrink it once the controls are familiar.
- **Show heat on the card or gauge** while facing a hob, so the reading is where the player is already looking.
- **Move the pips** to the hob's front edge, which the card doesn't cover.

**To check at the same time:** whether the card also hides the pan and steak on stations further up the screen, and what happens with two players' cards at neighbouring hobs.

---

## Test scenarios: start at any restaurant on any day

*Added 2026-11-06 · Relevant phase: 5 (built with the campaign save) · Supports: verification, tuning*

**The idea (user's question):** to test the experience of "restaurant C on day 7" without playing the campaign up to that point.

**How it could work (proposal):**
- **A scenario is a campaign save file:** layout, staff, stock, money, reputation and day number, plus a random seed.
- **Launcher:** a small Editor tool lists the scenarios. Pick one, press Play, and the game starts at that day's Plan phase.
- **Two ways to create them:** a debug key captures the current moment from play, or a file is written by hand for situations not reached yet ("broke, with a tired crew and a critic due").
- **Automated checks:** EditMode tests load a scenario to check the simulation, and a PlayMode smoke test checks the scene runs without errors. Fast-forwarding through days to check the money curve needs a simple stand-in for how well players cook. Whether a day is fun can only be judged by playing it.

**Risks:**
- **Stale scenarios:** save migration keeps old files loading, but a day 7 tuned for today's balance may stop representing a real day 7 after retuning. Keep only a few named scenarios, and regenerate them instead of patching them.
- **Repeatable days:** a scenario needs the same customers and orders each run, or before and after can't be compared. All randomness must come from the stored seed. Not yet checked for the current customer and group spawning.

**Suggested phasing:** build the launcher together with the save system, as it adds little work then and tests saving from day one. Until then, a config asset could set the starting day, money and tables when pressing Play in the greybox.

---

## Customers take real photos that appear on the listing

*Added 2026-11-06 · Relevant phases: 6 (listing, first test with dish photos), 7 (influencers), 9 (looks good once there's art) · Pillars: identity, plannable chaos*

**The idea (user's):** customers raise their phones when things go very well or very bad, which is a signal during service. The restaurant's listing then shows those photos beside the reviews, taken from what really happened: plates, decoration, even a player cutting fish in a focused state.

**How it could work (proposal):**
- **Real moments:** a one-frame render from a second camera near the customer, with the HUD hidden. Food already gets its look from the simulation, so a burnt steak photographs as burnt.
- **Triggers:** a very high or low dish score, decoration far from what the style expects, a player on a streak of perfect cuts, or a fire. The customer's reaction shows which kind it is, as a raised phone alone could mean either.
- **Photos back up reviews:** "Perfect pizza, but €40 on a plastic stool?" comes with a photo of the stool.
- **Limits:** a few photos a day at about 512 px, saved with the restaurant, keeping the best and worst over time. An asynchronous readback should keep it from causing a hitch at 60 fps.

**Risks:** the overhead game view hides things a close-up won't, so photos will look rough until Phase 9's art, and chefs need a focused pose or animation. Automatic framing can also clip through walls or furniture, so each subject needs a set of safe camera angles, with no photo if none is clear.

**Not exported (user's call, 2026-11-06):** photos stay inside the game.

**Possible later:** an album of the campaign could be kept after an erasure.

---

## Questions from the campaign design

*Added 2026-11-06 · Relevant phases: 5 (saves, economy), 6 (campaign), 7 (content), 8 (guided first day) · Pillars: all · See `game-design.md` §15*

**Entering a name on a controller.** Needs a letter grid, plus a few suggested names so nobody has to use it.

**How are cuisine and service style combinations judged?** Agreed in outline (`decisions.md`): rules for all 18 combinations would be too much, so service style sets most expectations, each cuisine adds a few, and only combinations that earn it get custom lines. Still to settle: the actual expectations and their weights, how much an unusual combination changes who comes, and which combinations count as unusual. Examples discussed: decoration pieces carry tags such as formal, functional, eclectic or warm; reviews keep commenting on the worst thing, so each fix surfaces the next problem ("Perfect pizza, but €40 on a plastic stool?"); about 10–20 custom lines, added late ("I came to laugh at fine-dining pizza. I've booked again.").

**What's at stake after the takeover?** With the boss gone, debt and erasure stop mattering. Could be enough as a victory lap with bigger goals, or could need a new pressure such as a rival.

**How is the customer forecast shown?** Prep needs an approximate number. Recommendation: a range based on reputation, seats and day, shown in words or as a range and never exact, and a good review should visibly raise it the next day.

**Where do loan repayments fall?** Each restaurant has its own collection day, but a loan belongs to the campaign. Recommendation: a collection covers that restaurant's cut plus an instalment of the loan. Leaving before day 7 only delays a fee, by at most 6 days per restaurant, so it isn't an exploit, and it's a fair breather for a failing place.

**The cleaning pattern.** Recommendation to try first: rotate the stick in circles, with tougher stains asking for back-and-forth or a change of direction. It should take a second or two per stain, any motion should count, and it should be impossible to fail. The chef stays put while doing it, as at the sink.

**How many restaurants, and how bespoke?** Scenery that suits each location and a full miniature get expensive quickly. Recommendation: about 5–6 to start, with streets built from a modular kit.

**Expanding a restaurant you own.** Size tiers were dropped from Phase 4, so a bigger place means buying one. Could return in Phase 6 as knocking through a wall, for players who love the place they have.

**Decoration model.** Could work as one "ambience" part of each review, kept apart from the dish score, with review lines using the existing "about the worst thing" rule: "delicious, but the place felt generic". Which customers it attracts needs its own design pass.

---

## HUD and signifiers pass

*Added 2026-11-02 after the milestone 5′ playtest · Relevant phase: a focused pass before Phase 2's playtests, polish in Phase 7 · Pillars: all (readability is what makes the craft and the chaos legible)*

**Playtest verdict:** the logic is in place and feels good; the HUD and signifiers need much more love. Fine-tune later.

**What's greybox today (inventory to redesign):**
- Station prompts (IMGUI boxes above the faced station, device glyphs, storage diamond).
- Quality card above the pass (text, half-star count).
- Doneness chip (a plain sphere above the pan).
- Heat pips (three cubes on the hob top) and burner glow.
- Steak cues: top colour, three edge bands; pan colour; smoke; steam.
- Targeting highlight (tint towards the player's colour) and the "can't" shake.
- Debug overlay (F1), for tuning only.

**Directions to explore:**
- One visual language for all prompts and states: icons and glyph art instead of text, consistent placement, player-colour framing.
- World-space UI that sits on the station (e.g. a dial ring around the burner instead of pips; a doneness ring around the steak instead of a floating ball).
- Juice for actions: sizzle on placing food in a hot pan, a punchier serve moment at the pass, stars that animate in.
- Sound as a signifier (sizzle tracks pan temperature, alarm before burning): probably the biggest readability win once there's audio.
- Sound still to do (two sets are in, decisions 2026-11-07): the prep assistant's chopping (only players' cuts make a sound so far), footsteps for staff and customers, different footsteps per floor, a clack that suits what's being set down, customers reacting to the fire detector (perhaps with health inspections, Phase 7), music, a volume setting, and real recordings to replace the synthesized clips, the door creak and footsteps first.
- Accessibility: colour-blind-safe doneness and player colours; text size.

**Suggestion:** a short dedicated UX pass at the end of Phase 1 or before Phase 2's first service playtests, because readability decides whether playtests give useful feedback; full polish stays in Phase 7.

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

**Update 2026-11-02, your direction:**
- **Buying more storage** as the business grows: extra fridges and shelves are purchases, competing with other spending and with floor space. This fits Phase 4 (placing) and Phase 5 (economy).
- **Each unit holds up to 4 ingredients**, with a **button prompt** when you approach it. For example, on a shelf: A tomato, B onion, Y flour, X cheese. One press takes that ingredient.

**Assessment of the 4-slot prompt:** I recommend it. It solves the "which ingredient" problem in a single press, and 4 matches the face buttons exactly. Details to get right:
- **It's a button prompt, not a radial.** A radial suggests aiming with the stick, which is slower. Here every face button *is* a slot.
- **Face buttons change meaning near storage.** A is normally Interact and X is Use. That's fine because storage has no other use, but the prompt must make the change obvious, and it should only appear when the unit is your current target.
- **Physical layout matches the buttons.** The four bins sit on the unit in a diamond that mirrors A/B/X/Y (Y top, A bottom, X left, B right). The prompt and the model then say the same thing, and the unit reads from overhead even without the prompt.
- **Keyboard:** settled by the keyboard decision: X = A slot, D = B slot, A = X slot, W = Y slot (a diamond mirroring the face buttons, arrows to move).
- **Putting things back.** With full hands, Interact puts the item into the slot assigned to that ingredient, so no choice is needed. Which ingredient goes in which slot is set while stocking, or in Plan.
- **Two players at the same unit:** each sees their own prompt, in their colour.
- **Show stock on the prompt,** e.g. "A Tomato ×6", so running low is visible before it's empty.

---

**Suggested phasing:**
- **Phase 1:** the fridge station (already on the roadmap) is built as a 4-slot unit with capacity and the button prompt, refilled automatically at the start of each test run. It replaces the Phase 0 crates.
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

## Hide the flip gauge (2026-11-03)

The flip gauge could later hide behind a realism or assist setting, for players who want to judge the sear by eye.

## Doneness signifiers at the hob (2026-11-03)

After the first flip, players read the doneness from the tag when close to the hob. Better visuals, icons or signifiers come at a later stage (user's call).

## Picky and easy-going customers (proposed 2026-11-03)

Each customer gets a pickiness that widens or narrows what they accept and how readily they complain, with perfectionists tipping best. It only feels fair if players can tell who is who (a mark on the ticket, say), and it needs care alongside the steep tip curve. Could grow into the critics of Phase 6.

## Errands: leave the restaurant to hire and buy stock (proposed by the user, 2026-11-03)

A later stage, and potentially ideal for co-op: hiring and buying stock happen at a separate place players physically go to, instead of on a menu. When a player leaves the restaurant, the camera zooms out, so one player can go for groceries while the other decorates, improves the layout or prepares mise en place. A hired helper can run the errand instead, and then the camera doesn't zoom out.

To work out when we get there: staying readable when the camera has zoomed out, whether split-screen suits it better, and how long an errand takes compared to prep. Keep hiring and stock rules separate from their screens in Phase 3 and 5, so the Plan menus can be swapped for this. Connects to the framing camera deferred to Phase 4.

## Taking out the trash (proposed by the user, 2026-11-08)

The bin fills after 8 things (`BinStation._capacity`), then refuses more and shows a 🚫 until it's emptied; for now a fresh bag goes in each morning (decision 2026-11-08). Once the restaurant's outside exists, players tie off the full bag and carry it out to the street containers round the building, like any carried item, and put a fresh bag in. The morning reset goes then.

To work out when we get there: whether a full bin left standing smells and costs hygiene or reputation (the hygiene realism dial, health inspections), and whether staff can take it out.
