# Decisions

Design and technical decisions, newest first. Each entry: date, decision, reason.

---

## 2026-11-06 — The campaign, the boss and the first day

**User's calls (full picture in `game-design.md` §15):**

*Story and failure*
- **The boss** funds the player's restaurants to build a gastro-empire, believing reviews, social media and word of mouth make restaurants more profitable than ever. He takes a cut. His physical description isn't part of the joke.
- **Failing to pay escalates visibly:** the boss visits and warns you, his bodyguards repossess appliances on screen, he takes a restaurant, and finally you're erased. Money and restaurants go, and the recipe book stays in his office for the next chef. This replaces the "never all progress" half of the no-resets rule.
- **The ending:** with every restaurant on the city miniature unlocked, you take over the empire. The bodyguards follow player 1 and stand guard outside the door in restaurants. This is when players really make money and can hire and decorate more heavily.

*Saves and time*
- **One campaign at a time, with no save slots.** Progress on all restaurants saves continuously. The recipe book lasts across campaigns. Replaces "each restaurant is a persistent save slot".
- **A restaurant only runs while you're in it.** Others sit idle, and players choose between focusing on one and jumping around.
- **Fees are due every 7 days played in that restaurant.** Leave on day 3 and nothing is due until players return and play 4 more days there.
- **Collection day also sets the week's goal.**

*Food and recipes*
- **The first restaurant is a grill**, serving onion steaks.
- **No test kitchen in the office.** A new ingredient and its mechanics are introduced when it's first bought.
- **Style decides what vendors sell. Buying a new ingredient unlocks one or more recipes,** and customers can order them from then on. Harder recipes pay more.
- **Vendors are outside and stay reachable during service,** so running out means leaving the kitchen to restock. Prep is where players plan around the approximate number of customers expected.

*The first day*
- **Dry start:** players look round the restaurant and clean up, with very light mechanics.
- **The boss phones** and asks them to get ready for him and his friends: enough plates and onion steaks.
- **The restaurant has no plates at all.** The plate pile is an appliance, bought in management mode (Select), and it's the first purchase.
- **Street vendors near the restaurant** sell the food.
- **The boss arrives alone,** because his friends made a cheap excuse. Only he sits and eats while the bodyguards stand behind him. When he's done he invites them to sit, which brings two more orders, and both bodyguards leave a review.
- **Tasks appear one by one in the top right corner,** each with its own control hints.

*Cleaning, devices and decoration*
- **Dirt is on floors, tables and counters,** cleaned the same way: approach it, hold X and move the stick to match a pattern. The chef uses a mop or a cloth depending on the surface.
- **Boss calls come through a personal mobile,** shown in the bottom right corner.
- **A restaurant laptop, phone or tablet station is a separate, bought appliance,** introduced around day 2. It opens a shared-screen listing, like a Google listing, and that's where reviews are explained in more depth. It also offers quick-restock delivery and switching restaurants without visiting the office, and may gain other perks.
- **Appliances are bought from management mode,** with no need to go anywhere.
- **Decoration affects reviews and customer profile:** the food can be delicious while the place doesn't feel original, clean, or in keeping with its style.
- **Vendors and scenery should suit the location** chosen in the office. The camera zooms out when players split up, and vendors stay close.
- **Co-op and repeat play:** any player can complete any task, and returning players can skip the tutorial.

**Details (proposed while writing this up; flagged for review):**
- **An idle restaurant is frozen:** food doesn't age, and nobody is paid or changes morale. Nothing in it moves until players return.
- **The bought plate pile comes with its plates,** 6 as now.
- **Washing up first appears after the boss's meal,** since no dirty plates exist before then.

**Replaces:** starting with plates, and with no tables (two 4-seat tables are now inherited); the plate pile not being for sale; the free daily delivery, once vendors exist; the review app as a phone app (§7.3).

**Still open:** see "Questions from the campaign design" in `ideas.md`.

**Reason:** Gives the game a reason to exist beyond each service, and teaches its systems through a story, one light step at a time.

---

## 2026-11-04 — Hints only ever show Xbox buttons

**Decision (user's call):** nothing players see names keyboard controls, whatever they're playing with. Hints are always Xbox buttons. The keyboard still works, mainly for testing.

---

## 2026-11-05 — The floor plan, a camera that follows the action, and tables (Phase 4, milestone 3)

**User's calls:**
- **A real restaurant floor plan:** customers come into the dining room from the street, not past the kitchen, and a door lets people move between the rooms.
- **A customer sits and shows their order even at a dirty table;** players clear it before they can put the dish down.
- **Customers and players collide,** just as hires do.
- **The camera follows the action,** zooming out as players or customers spread apart and in as they gather. The restaurant can grow as much as players want, so a fixed camera won't do. Replaces the fixed camera decided for Phases 0–3 and moves this up from size tiers.

**Details (proposed while building; flagged for review):**
- **The building is 10 × 12 m:** the kitchen as it was, and a 5 m deep dining room to its south. A low wall runs between them, so the camera sees over it. The door is 1.2 m wide, west of the hatch, and swings open for whoever's near. It never blocks anyone.
- **Tables are bought with none at the start,** and only go in the dining room; kitchen furniture stays in the kitchen. Within a room, layouts are still free. Tables bought in Plan or Prep count from that day's service.
- **Only players clear tables.** The dishwasher still clears the pass, leaving the tables as the waiter's job.
- **A customer with no way to their seat gives up:** a lost cover, no penalty, and the seat goes to the next. Someone leaving always gets out, and someone blocked by people for 2 seconds walks through, so a bad layout costs money but can't freeze a service.
- **Pushing a seated customer moves them,** and they walk straight back.
- **The camera** keeps its 55 degree tilt, keeps 2 m of floor around everyone, and backs away faster than it closes in. Staff don't count towards the framing, as they stay in the kitchen. Tunable on `FramingCamera`.

**Later the same milestone (2026-11-05 and 06).**

*User's calls:*
- **The street door moves like furniture** and counts towards the camera's framing, so someone walking in doesn't make it zoom out. It only goes on the dining room's outer walls; anywhere else it turns red. It's framed during Service only, so a chef working alone in Plan or Prep still gets a close view.
- **Furniture against the wall between the rooms works from both sides,** like a hatch, whether it's a table or an appliance.
- **Two table types for now:** a bar table seats 1 and is narrower, and its chair costs nothing. A regular table seats up to 4, and players choose how many chairs it has.
- **Customers come in groups of 1 to 4,** following the tables. With at least one table for 4, expect groups of up to 4.

*Details (proposed and accepted as a plan; tunable in `FurnitureCatalog`):*
- **Prices:** bar table €30, regular table €50 with one chair, each further chair €10, refunded at half. RB and LB add and remove chairs while hovering over a table.
- **One chair per side,** and only where there's room in the building, so a table against a wall seats at most 3. Your biggest groups always need the long walk.
- **Each table votes on group size:** a stool or bar table votes for one person; a table for four splits its vote across 1, 2, 3 and 4.
- **A group takes the smallest free table that fits,** never shares, and stools are for people alone. The longest-waiting group that fits gets the next free table.
- **A plate goes to the diner it matches,** else the first waiting. Each starts eating when served and pays for their own dish, and they all leave together after the last has finished.
- **A group shares one patience meter.** If it runs out, everyone unserved walks out, and anyone eating pays for what they've had.

*Details (proposed while building; flagged for review):*
- **A pushed customer goes along with it** and returns 0.8 s after contact ends, as hires do. Seated customers can be pushed off their seat.
- **Routes keep clear of chairs** where there's room, so a diner doesn't walk through a friend already sitting. Someone who makes no real progress for a second squeezes through people for a moment, and walks through furniture only if pinned, so a crowded corner can't freeze a service.
- **Staff don't yet work across the wall;** only chefs do.

**Known limits:** chairs and queue spots aren't protected, so a table can be placed over one; the customer then walks through it and sits inside it. Walking speed may make distance matter too little (see the milestone's playtest).

---

## 2026-11-04 — Buying and selling furniture (Phase 4, milestone 2)

**User's calls:** you start with **4 counters and 1 hob**. Everything else about the milestone was proposed and accepted as a plan.

**Details (tunable in `FurnitureCatalog`):**
- **Y opens a shop in management mode,** during Plan and Prep: counter €30, hob €80, shelf €60, fridge €120. These are first guesses, not yet checked against a full day's earnings.
- **You pay when it goes down,** so B backs out for free. If two players are placing at once and the money runs out, the second gets "not enough money". Unlike wages, a purchase can't cause debt.
- **X sells for half the price,** free starting furniture included, so a starting counter is worth €15.
- **Only empty pieces sell,** a hob only with its pan home, and not while it's carried or a helper is headed to it. The pass, sink, piles, board, racks and bin aren't for sale.
- **The last hob, fridge and shelf can't be sold.** This is the exception to learning by playing: with no hob and no money the restaurant couldn't recover, and deliveries need a fridge and a shelf to arrive in. Selling a fridge or shelf hands its delivery to the one that's left.
- **A bought hob comes with its own pan,** and a sold one takes its pan with it.

**Not built:** bought furniture and layouts last from day to day but not after quitting, until saves in Phase 5.

---

## 2026-11-04 — Layout editing and management mode (Phase 4, milestone 1)

**User's call:** during Plan and mise en place, players can switch to a cursor view with the Select button.

**Details (proposed while building; flagged for review):**
- **Keyboard:** Tab, though hints only ever name Xbox buttons. **While managing:** the chef waits; A picks up and places, B puts what's held back where it was, and Select returns to the chef, the same button that enters (user's call). You can't ready up while managing.
- **No grid (user's call):** furniture goes exactly where the player puts it, snapping when within 0.15 m of lining up with a neighbour (edges touching or centres level), a wall, or where it came from. Replaces the 0.5 m grid first built.
- **RB and LB rotate by 45 degrees (user's call),** easing round in about 0.1 s, though the new angle counts straight away (user's call: smooth it now). Keyboard: C and Z. Turned stations take up their own shape, can't poke through walls, and snap to neighbours turned the same way.
- **Furniture under a cursor lights up,** just as when a chef walks up to it (user's call). It keeps its own colour and only gets brighter: tinting it towards the player's colour was confusing. This replaces the player-colour tint from Phase 0 for chefs too.
- **Anything can be moved,** and what's on a station moves with it. Two players can't hold the same station.
- **Placements are refused** outside the piece's own room, on another station, or on a person. The reason shows at the cursor.
- **A layout is never refused for being bad (user's call):** a blocked hob, a cut-off counter or a walled-in chef are all allowed, and players find out by walking it. This replaces the earlier rules that work stations keep a clear front and that nothing gets cut off. Learning by playing beats being prevented. A helper with no way to a station stands and waits rather than working from a distance.
- **The pass doesn't move:** it's the hatch in the wall between the rooms, so it's part of the building (replaces sliding along its row).
- **What's carried turns red where it can't go (user's call),** and the floor frame only shows while choosing what to pick up.
- **What's carried stays on the floor (user's call):** lifted, the angled camera drew it away from where it would land. Being carried shows through brightness, or red.
- **Service stops rearranging:** management mode isn't available during Service or Review.

---

## 2026-11-04 — One waiter, no runner; a limited pile of dirty dishes

**Decisions (user's call):**
- **The waiter does both jobs:** takes food to the table and clears the table afterwards. There's no separate runner. Replaces the roadmap's and design doc's waiter and runner.
- **Dirty dishes get a pile with a maximum,** just like clean plates. The pile holds 6, the clean pile holds 6 and the sink holds 2, as three separate things. Plates go from the pass to the dirty pile, to the sink, to the clean pile, in hands or on a tray.
- **Management mode:** during mise en place, players can switch to the cursor view with the Select button on the Xbox controller.

**Note:** the kitchen owns 6 plates, so the dirty pile can't actually fill up until there are more plates. The sink's limit of 2 is what bites for now.

**Reason:** One clear hire instead of two similar ones, and a full pile makes falling behind on washing up visible.

---

## 2026-11-04 — Morale (Phase 3)

**Decisions (proposed while building; flagged for review, tunable in `StaffConfig`):**
- **Everyone starts at 60%,** where it neither helps nor hurts. Morale carries from day to day, while fatigue resets overnight.
- **It changes at the end of each day:** a good rating lifts it (up to +10% at 5 stars, down to -10% at 1 star, 3 stars neutral), walk-outs lower it (up to -15% if everyone walked out), and ending a day past 50% fatigue wears on it (up to -20%).
- **Its effects:** speed changes by 3% per 10 points away from neutral, in both directions. Mistakes get likelier only below neutral, because being happy doesn't add skill.
- **Below 10%, they quit** at the end of the day, in the Review. Wages for that day are still paid.
- **Players see words, not numbers:** new, happy, doing fine, unhappy, fed up, on the Plan card and in the Review. The debug overlay shows the numbers.

**Reason:** Makes staff feel like people, and ties how a service goes to how the team works the next day. Pay and how you treat them (pushing them around, say) don't affect it yet.

---

## 2026-11-04 — People push each other; onions burn visibly

**Pushing (user's call):** chefs and staff can push each other. Someone idle gets moved along by whoever walks into them, and two walking into each other with equal force don't move. A helper pushed away from their work stops, walks back when there's space, and carries on.

**After play:** bodies have weight: an idle body soaks up 80% of a push (`_resistance` on `PushResolver`), so it moves at a fifth of walking pace (first 40%, too light in play). A pushed helper doesn't fight back while pressed, so you can keep pushing them as far as you like; they head back 0.8 s after nobody's touching them. Helpers slide along furniture when pushed into it, as chefs do.

**Details (proposed while building):** the stronger push wins by the difference. Someone pinned against furniture can't give way, so the pusher stops. The food stays on the board while a helper is pushed off it, and a chef can take it in the meantime. A chef pushed while washing keeps washing for now.

**Onions burn (user's call):** food left on the heat after it's cooked burns, onions included. Diced onion browns faster (1/14000, was 1/30000). It wasn't showing a burn at all, because the view showed the side facing up and onions don't get flipped; loose food now shows its darkest side. Food tags say "Burnt" for any burnt food.

**Measured:** at full heat, diced onion is cooked at 4 real seconds and burnt at about 6.5; at 70% heat, cooked at about 6 and burnt at about 23.

---

## 2026-11-04 — The prep assistant (Phase 3)

**Agreed:** the assistant keeps the containers players set out stocked. A container with some food in it says what it's for; when it runs low, they bring more from storage by tray, cutting at the board if what's stored is still whole. Empty containers are left alone: only players decide what a container is for. They never touch the hob or the steak.

**After playtest (user's calls):** assistants don't restock storage; they only keep containers filled, going by the first ingredient players store. A tray only comes out to move several portions and goes back afterwards; food to cut is carried by hand. If they end up holding food with nowhere to put it, whole food goes back to storage and food already cut goes in the bin.

**Details (proposed while building; flagged for review, tunable in `StaffConfig`):**
- **Refills below half full,** emptiest first, up to 4 portions a trip and never past full.
- **With no tray free, they carry one portion at a time by hand.**
- **Cuts one item in 4 real seconds** at average speed, without the dial. Skill and tiredness set how even the cut is. A mistake drops a portion, which is lost.
- **A busy board:** they don't fetch food to cut while it's in use, and if a chef gets there first they wait 5 real seconds, then put the food back. A chef can take food off the board mid-cut.
- **Candidates:** every day offers at least one person per role. Four assistant types, €40–80 a day.
- **Leftovers** go to a matching container first. Whole food with no room in storage also goes in the bin.
- **Staff walk around furniture,** by pathfinding, and stand on the free side of a station nearest the middle of the room. 
- **Staff take up space (user's call):** they arrive at separate spots, and give way to each other and to chefs, keeping right to pass. If someone's standing on their spot when they start a job, they stop close by and carry on from there.

---

## 2026-11-04 — Any ingredient can go in any storage

**Decision (user's call):** food isn't tied to a fridge or shelf. Refrigerated food can go on the shelf and anything can go in the fridge, and the cost is freshness: meat on a shelf spoils much faster. Replaces the fixed slot for each ingredient.

**Details (proposed while building):** each unit still has four slots, one ingredient per slot at a time, free again when empty. A portion goes with the same ingredient if there's room, or else into a free slot. Slots hold up to 6 (user's call), so each unit holds 24. Deliveries fill the same way (18 steaks to the fridge, 8 onions to the shelf), and tomatoes and diced onion no longer get a reserved place.

**Measured by the existing model:** a night on the shelf costs a steak about 86% freshness, against about 25% in the fridge. Stored food below 10% gets thrown out, so a steak left on the shelf won't survive much more than a night.

---

## 2026-11-04 — Containers, trays and restocking through shopping

**The user's design:**
- **Restocking happens through shopping.** Players start with a fridge and a shelf, and purchases go straight there. This replaces the storeroom and crates built earlier the same day. Shopping itself comes later; until then, a free delivery arrives at the fridge and shelf each morning.
- **The cutting board only cuts.** The finished cut stays on it and blocks the next one until someone moves it; the board's four slots are gone.
- **Containers** fill a counter and hold up to 6 portions of one ingredient at a time (first 10, changed after play). They keep food fresher than a tray, but not as well as a fridge. Counters become precious, and players plan their kitchen around them.
- **Trays** fill a counter when put down and hold 4 things in any mix: ingredients, or empty, filled or dirty plates. Food loose on one ages faster. They can serve customers (RT chooses what to give) and move ingredients, including restocking a fridge.
- **Usual flow:** with a tray, go to the fridge or shelf, then put ingredients in containers for quick access during service or mise en place.

**Details proposed while building (flagged for review, in `EquipmentTier`):**
- **6 containers and 4 trays,** on racks, carried to any counter by hand until layout editing in Phase 4.
- **Ageing:** food in a container ages at half the normal rate and on a tray at 1.5×. A fridge still beats a container because of its cold.
- **Containers give one portion at a time,** oldest first, to hands, plates and pans. A tray loads as many as fit. A container can only be picked up when empty.
- **RT with a tray** opens a four-button menu of what's on it. Not at storage, where RT loads the tray from the slot menu, or at a hob, where RT is the heat. A with a tray tries the last thing put on.
- **Daily delivery:** 18 steaks and 8 onions, in fridge slots of 20 + 20 and a shelf slot of 12 onions. Food stored starts at 0, then the first delivery arrives on day 1.
- **Food in a container doesn't count as "left out" overnight.**

**Returning things:** clean plates, empty trays and containers go back to their piles, and ingredients back to storage. A tray returns everything on it that fits, wherever it is on the tray. Cut food can be put away too.

**More counters for testing:** 8 free counters instead of 3 (a longer island and three along the walls). The final number is a design question, since counters are meant to be precious.

**Not built:** shopping and choosing what to buy, so deliveries are still fixed, and leftovers can pile up.

---

## 2026-11-04 — Storeroom, crates and the night (Phase 3; storeroom and crates since replaced)

**Decisions (proposed while building; flagged for review):**
- **Storeroom on the east wall,** across the kitchen from the fridge and shelf, kept cold. The trip is the cost of not prepping. It holds 40 steaks and 20 onions.
- **A free delivery each morning,** as Prep starts: 18 steaks and 8 onions, up to capacity. The Prep card says what arrived. Tomatoes aren't delivered, since nobody orders them.
- **The line starts light on day 1** (6 steaks, 4 onions, 2 tomatoes) and no longer refills by itself. This replaces the temporary morning top-up.
- **Crates:** the storeroom's menu hands out a crate of up to 6 of the oldest portions in that slot. Interact empties it into the fridge or shelf, and what doesn't fit stays in the crate. An emptied crate disappears rather than needing to be carried back.
- **Closing runs as the service ends:** 18 in-game hours pass, hobs turn off, and food ages at the temperature around it. Stored food below 10% freshness is thrown out. Food left out stays where it is, aged at room temperature. The Review lists both, which is the Close line agreed in the plan.

**Known effect, to judge in play:** a night in the cold costs a steak about 25% freshness, and storage hands out the oldest first. That alone takes a day-old steak from a perfect score to about 98%, which costs most of the top tip on the steep curve. So overstocking hurts, but with fixed deliveries the player can't yet order less. Buying comes in Phase 5.

---

## 2026-11-04 — Wash game: less inertia, interface above the chef

**Playtest:** the mechanic feels good, but the point had too much inertia and the interface was intrusive.

**Decisions:** the interface is smaller and see-through, and sits above the washing chef. The point keeps moving far less after you let go (damping 5, was 2.5), runs away more gently (instability 4, was 5) and gets smaller nudges (1.4, was 1.8). In the simulation, the coast after a full push drops from about 1.6 circle radii of travel to about 0.4.

**Then, after more play (too easy):** damping 4.2, instability 4.5, nudge 1.6, between the first and second tuning.

**Trade-off:** ordinary over-correcting used to be heavily punished and now mostly isn't; only slamming the stick around with slow reactions is. This softens "small movements shift it drastically" in the earlier entry. Doing nothing still never cleans a plate.

---

## 2026-11-04 — Plates can wait on the pass; washing by hand is a balancing game

**User's direction:** a blocked stool feels good. A plate on a free stool can be taken back, and goes to whoever sits down next, fitting their order or not. Washing becomes a stick game: keep a point in the centre of a circle, small movements shift it a lot, and not reacting never cleans a plate.

**Details (proposed while building; flagged for review, tunable in `TuningConfig`):**
- **Plates wait indefinitely** on a free counter, cooling, including overnight. This replaces clearing them after a delay. They can also be set out during Prep.
- **The point behaves like balancing a stick on your palm:** it accelerates away from the centre the further off it is, with small random nudges. A plate takes 2.5 s at full speed; full speed inside 25% of the circle, fading to nothing at 55%. Each plate starts with the point off-centre at 70%.
- **The nudge is kept below what the instability can overcome,** so a point left alone can't drift back into range. A test guards this.
- **One chef at the sink at a time,** with empty hands. Use starts and stops, and the chef stays put. The dishwasher can wash alongside, sharing progress, and isn't subject to the game.

**Measured with simulated players, not real ones:** doing nothing cleans 0 plates; a steady hand with a 0.15 s reaction takes under 4 s per plate; over-correcting is several times slower.

---

## 2026-11-04 — Hiring and the dishwasher (Phase 3)

**Decisions (proposed while building; flagged for review, all tunable in `StaffConfig`):**
- **Hiring happens on the Plan card:** A, X and Y hire the three people listed, and B lets your dishwasher go. Any player can press them. Candidates change daily and are described in words.
- **Starting money €100; wages €30–70 a day,** paid when the service ends. You can't hire what you can't afford, but a day's wages can still push the balance negative.
- **One person per role,** and only the dishwasher exists so far.
- **The dishwasher follows the player's rules:** one plate at a time, from pass to sink to stack, and a player can beat them to a plate. Order of work: clean plates to the stack, then clearing stools, then washing. A plate takes 2.5 real seconds at average speed.
- **Fatigue and mistakes:** each plate adds fatigue, which slows them down and makes mistakes likelier. It resets overnight. A mistake breaks a plate.
- **Broken plates are replaced free each morning** until they can be bought in Phase 5.
- **Chefs can't pick things up or use stations during Plan and Review,** so the hiring buttons don't also do things in the kitchen. They can still walk around.
- **Staff have no collision** yet, so they walk through chefs, and they don't path around counters; their routes along the pass happen to be clear.

**Reason:** Makes a hire a real decision, with a visible payoff: someone else clears the stools and does the washing up.

---

## 2026-11-04 — Plates and washing up (Phase 3)

**Order changed (user's call):** plates and hiring come before the storeroom, to reach the first hire sooner.

**Decisions (proposed while building; flagged for review):**
- **The kitchen owns 6 plates** (in `EquipmentTier`), and the stack can run out.
- **A used plate stays dirty on the pass until someone clears it, and blocks that stool.** A customer can sit there, but can't be served until it's cleared.
- **The sink:** drop dirty plates in, press Use 4 times per plate (in `TuningConfig`), then carry clean plates back to the stack. Everything is carried by hand, one plate at a time.
- **Plates left dirty stay dirty overnight.**

**Reason:** Gives washing up a real cost in time, so that hiring help pays off.

---

## 2026-11-03 — Phase 3 shape and the daily loop (milestone 1)

**Agreed with the Phase 3 plan (`plans/phase-3.md`):** the waiter and runner wait for tables in Phase 4; Prep has no time limit and ends when all players ready up; Close is summarised; staff have fixed roles; restocking is physical, with a storeroom and crates of up to 6; hiring is a Plan menu with candidates described in words. Progress lasts for the session until Phase 5.

**Decisions while building milestone 1 (flagged for review):**
- **Every phase but Service ends when all players ready up** with Menu (keyboard: Esc); pressing again takes it back. This replaces the Phase 2 restart, so `PlayerRejoin` and the scene reload are gone.
- **Close has no phase of its own yet;** its waste line arrives with stock in milestone 2.
- **The simulation pauses during Plan and Review,** so food on a hob doesn't burn while players read. Players can still walk around.
- **Prep's card sits near the top of the screen,** because players work through that phase.
- **Each day has its own customers,** from the seed plus the day number, so a given day replays the same while tuning.
- **Balance starts at 0** until hiring needs starting money.
- **Temporary:** line storage tops up by itself each morning, until the storeroom replaces this in milestone 2. Food left on counters and in pans stays there.

**Reason:** Turns one service into days that follow each other, so the later milestones have a loop to plug into.

**`game-design.md` §4 is now partly out of date:** it lists Close as a phase and leaves open whether Prep has a time limit and whether Close is played.

---

## 2026-11-03 — Slightly easier crust; picky customers after Phase 2

**Decisions:**
- **Crust scoring relaxed a little** (values proposed, now in `TuningConfig`): no crust at an average browning of 0.1 or below (was 0.2), full marks from 0.6 (was 0.7). The "pale" comment now needs an average below 0.35.
- **Per-customer pickiness waits until after Phase 2** (user's call; see `ideas.md`).

**Reason:** Playtest: the "pale" comment came up often. By calculation, flipping on green and then giving the second side about a real second shouldn't draw it even under the old rules, so if it keeps happening, look for early flips, or for a bug, before relaxing further.

---

## 2026-11-03 — One gauge, for the first flip only

**Decision (user's call; band proposed, tunable in `TuningConfig`):** a steak gets a single gauge, under the pan. It appears as soon as the steak touches the pan, its needle moves left to right, and it disappears at the first flip. After that, players judge the doneness themselves, reading the tag when close to the hob. Better signifiers come at a later stage. A second gauge for doneness was proposed and rejected as too much.

**How it reads:** the needle follows the actual browning of the side facing down, not a timer, so a cool pan makes it creep and a hot one makes it race. The green band is browning 0.7–1.1, and the scale ends at burnt. It lights up by growing slightly while in green. Other food gets no gauge (`NeedsFlip`, steak only).

**Measured (preheated pan at full heat):** green opens about 2 real seconds in and lasts about 1.2 s. Flipping anywhere in green still allows a best score of 98.8% or more for every order on the menu. The first side barely depends on the order; the second side and the heat decide the doneness: roughly 2–3.25 s for rare and medium-rare, 3.5–4.75 s for medium, and 8–9.5 s at 80% heat for well done.

**Reason:** Playtest: it wasn't discoverable how to get a steak right, and pale steaks felt unfair.

---

## 2026-11-03 — The service loop (Phase 2, milestone 5)

**Decisions (proposed while building; flagged for review):**
- **The kitchen is live before opening:** the cooking simulation runs, so players can preheat and prep, but no customers come until someone presses **Menu (keyboard: Esc)**. There's no time limit on this. It stands in for the Prep phase until Phase 3, and free prep time makes the first rush easier.
- **Last orders 30 in-game minutes before the end** (22:30, tunable): the door closes to new customers, and those inside can still be served.
- **Closing time (23:00):** the service ends once everyone has left. Anyone still waiting keeps losing patience and walks out, with the penalty. With the patience dial at 0, patience starts draining at closing time, so a service can always finish.
- **The summary** shows money (meals, tips, walk-outs), the rating over all reviews including walk-outs, how many were served and as ordered, walked out and turned away, the best review line and the most common complaint.
- **Restart reloads the scene,** with the same button, and players come back on their own devices without joining again. A small static (`PlayerRejoin`) carries this across the reload, as a deliberate exception to "no singletons". Stock, money and everything else start fresh; persistence comes in Phase 5.
- **Pause does nothing else yet:** the button is only used to open and restart.

**Reason:** Completes the first vertical slice: open, cook through a service, see how it went, go again.

---

## 2026-11-03 — Steep tips; walk-outs cost money

**Decisions (the user's direction; numbers proposed, tunable in `ServiceConfig`):**
- **Tips reward perfection,** based on the menu price: perfect → 100% of the price, 99% → 50%, 95% → 20%, 90% → 10%, 80% or below → nothing, blended between. Replaces the earlier "up to 25%, from a 60% score".
- **Walk-outs cost half their order's menu price** (€12 for a steak, €14 with onions), including those who give up in the queue. Money can go negative. Customers turned away at a full door cost nothing.
- **The wait only reduces the tip when patience drops below 50%,** falling to 40% of the tip at zero. Otherwise some waiting is unavoidable and 100% could never be reached.

**Perfect means 99.5% or above** (user's call): the top stop of the curve is at 99.5%, because nothing reaches exactly 100% with the real assets.

**Measured (open question):** the best scores found with the real assets, searching heat, time per side and rest but with a freshly stocked steak: medium-rare 99.78%, medium 99.64%, rare 99.12%, well done 99.01%. With the 99.5% stop, the best tips are 100% of the price for medium-rare and medium, and about 62% and 51% for rare and well done, so the harder orders still tip less. Revisit after playtesting; a fix would change how crust is scored. Tests hold each doneness at 99% or above.

**Reason:** Playtest feedback: tips should matter much more, and letting a customer walk out should hurt.

---

## 2026-11-03 — Payment, tips and reviews (Phase 2, milestone 4)

**Decisions (proposed while building; flagged for review):**
- **Prices:** steak €24, onions €4 (the currency is a field in `ServiceConfig`). Customers pay only for what reached the plate.
- **Payment** = price × (50% + 50% × dish score), rounded to whole units. A bad dish still earns something.
- **Tip** = payment × up to 25%. It rises from nothing at a dish score of 60% to the full amount at 100%, then shrinks with the wait, down to 40% of itself if served with no patience left. Quality earns the tip; speed protects it.
- **Customers pay and review when they finish eating.** Walk-outs pay nothing and leave a 1-star review. Customers turned away at a full door leave no review.
- **One review line per customer, about the worst thing:** no main, burnt, wrong doneness, no side, the weakest part of the dish, then a long wait; otherwise praise (from 4.5 stars) or "decent". Lines are editable in `ServiceConfig`.
- **On screen:** a money counter at the top centre, with each payment flashing beside it, and a 4-second card where the customer sat, showing stars, payment and the review line.
- **Money is income only for now.** Ingredient costs, rent and wages come in Phase 5.

**Reason:** Turns the dish score and patience into money and words, so a good dish is rewarded and a mistake can be understood.

**Departs from `game-design.md` §7.1**, which recommends no numbers during service: the cards show stars and money as an instant reward. The dish's score breakdown stays out of them. Revisit after playtesting.

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
