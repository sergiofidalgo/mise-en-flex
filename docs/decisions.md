# Decisions

Design and technical decisions, newest first. Each entry: date, decision, reason.

---

## 2026-11-12 — The light of the day follows the opening times

**The user's request:** "players can decide the restaurant opening times now... I wish that their choice would also impact the scene, so they can play during the day, night, or both; if they would play a full day we should also figure light transitions from day to night."

- **The light goes by the service's clock** (`Daylight`, `DaylightView`). Lunch (12:00–16:00) plays in daylight. Dinner (18:00–24:00) opens in the gold of early evening and is in full night by 20:45. All day (12:00–24:00) goes through all of it. The turns: day until 16:30, golden by 18:15, a mauve dusk by 19:30, deep blue night by 20:45; dawn between 5:30 and 8:30 is there for a clock that ever needs it. Before the doors open it's the opening hour; after they close, the hour they closed at, through Close and Review. A new day's morning eases in over about 1.5 s.
- **Outdoors and indoors are lit differently, on purpose.** Outdoors the sun and sky take the hour's colour, so the night street is dark blue and its lamps, windows and shopfronts are what light it. Inside the building's walls the restaurant has its own light: it warms slightly as night falls and never drops below nine tenths of daylight. Reason: the kitchen must read the same at any hour; cooking by a blue gloom would punish players for choosing dinner.
- **How:** two colours and the building's box on the ground, set as shader globals; `ToonCommon.hlsl` multiplies what the sun and sky gave by one or the other, blending across the walls' thickness, and leaves lamps alone. Unset (the office, the menu), nothing changes. The sun's shadows soften to half strength at night. The sun doesn't move.
- **The street's lamps and windows** go by the same hour (on from 19:00, as before).
- **To judge the look without playing a whole service:** `DaylightView` on the Street object has "Fix Hour" and an hour slider.

**Verified:** EditMode tests on the curve (white at lunch, blue at dinner, only ever darker through an all-day service and never a jump; indoors always above 0.9). Stills at 13:00, 18:15, 19:30 and 22:00 by `StreetLifeCapture`.

**To judge in play:** how dark the night street is for a chef walking to the stalls; the seam where the two lights meet at the doorway and along the cut walls; whether indoors should dim more for mood; whether the sun should also sink, for long evening shadows (not done: it would change how every counter and chef is shaded).

**Design doc:** §opening times (if there is one by now) should say the slot sets the light as well as the guests.

---

## 2026-11-12 — The street's art pass: real surfaces, modelled houses and props, and a façade that rises when the chefs are outside

**The user's notes:** not convinced by "the chimney, trees, the restaurant menu, branding or surface textures across the scene"; "the houses behind the restaurant don't feel great"; and "it'd be cool to show some parts of the restaurant façade as players go out".

- **Surfaces** (`Surfaces`, the room shader's world-space patterns; no new shader): the pavement is small bricks in rows, each a shade of its own; the plaza 0.6 m slabs; the far pavement 0.8 m slabs; the road and the parking lot mottled asphalt with grit; the grass patched lighter and darker.
- **New models** (`SM_Outside`, `build_outside` in the Blender script, put together by `Outside`):
  - **Houses** in two kinds, turn about: a cottage with its ridge along the street, and a taller one gable-on with an attic window. Brick walls in four colours, slate or tile roofs, shutters, panelled doors under little roofs, chimneys with pots. Their panes are what lights at dusk, two houses in three.
  - **Trees:** a broad tree and a poplar, leaves in two tones, the crown stirring. Behind the restaurant and in the park.
  - **No chimney; an air conditioning unit instead.** The extractor went through a two-metre steel flue ("doesn't really fit in the scene") and a squat copper cowl before the user dropped it: "remove the chimney, instead let's have some sort of AC outdoor unit sitting by the floor", with a reference photo, "on the right side of the restaurant". A cream box on two feet with a fan behind a round grille, louvres down one end and two pipes to the wall, on the ground against the east wall's north end, facing the parking lot. Its fan idles and turns faster the harder the kitchen cooks (`OutdoorUnitView`, `StreetHours.Extraction`). The steam, and the dark smoke of a pan on fire, went with the chimney.
  - **The menu:** chalked on an A-board (the lit stand is gone).
  - **The sign:** a cream board in a copper rim between two dark posts, a cloche on top as every restaurant's emblem.
- **The façade** (`FacadeView`): a pilaster at each front corner, a fascia right across with the restaurant's name on it, and a striped awning under that, all in the sign's red-brown and cream. It stands on the front wall's top and **rises only when every chef has been outside for 0.35 s**, in 0.4 s, and sinks as soon as one steps in. Reason: indoors nothing may hide the kitchen or the dining room, which is why the walls are cut low; outside, the building should look like a restaurant. With one chef in and one out it stays down. Nothing in it depends on where the door is, since players can move it; the awning runs over it.


**The user's later notes the same day, and what they changed:**

- **"Buildings could also use some texture."** The neighbours' walls are brick, or larger stone blocks for the cool colours, in the colour the city gave each (`OfficeBuilder.StreetBuilding`). Materials of the street's own, so the night city's flat walls are untouched.
- **"The parking lot is not technically accessible by cars... the banner goes on the left, connect the parking lot with the road, and put a pedestrian walk in between blocks."** The sign stands on the plaza's side now (1.9 m west of the building's corner, mirrored, so the bin still clears it). A driveway of asphalt crosses the pavement from a dropped kerb into the lot, between the restaurant and the next block, with a zebra crossing where the pavement carries on over it. The bay by the driveway stays empty; the two parked cars are in the bays beyond. No lamp or hydrant stands in the driveway's mouth. **"Pedestrian walk" was read as that zebra crossing; to be confirmed.** Chefs still can't step onto the road there: the kerb's invisible edge is unchanged.
- **"The menu should go right next to the main door... if the door moves, the menu should be like attached to it."** `DoorMenuView`: the A-board stands 1.2 m along the wall from the door's middle and 0.45 m out, on the side the queue doesn't use (`CustomerDirector.QueueSide`), and follows the door wherever players move it, turning with its wall. Passers-by still stop to read it, at a spot that moves with it, but only while it's on the street front: nobody walks round to a door on a side wall.
- **"The houses look nice, but the scenery around them doesn't feel great."** The houses moved 1.5 m back to make room: a cobbled lane behind the restaurant's fence; a front garden for each house behind a low hedge, with a path to its door and flowers under its ground-floor windows; a tree in the gardens between each pair of houses; hedges between the plots; and behind the houses a tall hedge the whole length with a line of large trees over it, where there was bare lawn and then nothing.

- **"The trash bin shall go on the left side of the restaurant, aligned with the green stall."** The wheelie bin starts against the west wall, level with the stall at the back of the plaza. It still rolls round the west, front and east walls, but **its track now stops 1.35 m short of the east wall's north end** (`BinTrack`'s `eastEndKept`), where the outdoor unit stands, so it can never be parked on it.
- **"The fire hydrant more noticeable, and the banner closer to the restaurant."** The hydrant is half as big again, on a pale pad (a yellow kerb beside it was tried and taken out: "remove the yellow lines"). The sign straddles the building's plaza-side front corner: one post in front of the wall, one by the plaza, 1.7 m out, beyond the queue and a bin against the wall, and just outside the bin's sweep round the corner.
- **Graffiti: "only by the parking, older and grimier."** Four old tags low on the wall by the parking lot, under the sunset mural: bleached towards the wall's grey, flaked off in patches, streaked with soot (`StreetArt.OldTags`). The city's own tags on the neighbours' shutters and fronts are cleared here. The two murals stay; **whether they count as graffiti to go is the user's to say.**
- **Steam from the road**, as in the user's reference of New York: two manholes with steam rising, one in the near lane off the plaza and one in the far lane beyond the driveway (`OfficeBuilder.StreetManhole`, the city's own).
- **The ground is laid in pieces up to 6 m across** and the street lamps are eight times as strong, both for the night: only four lamps can light any one object in this pipeline, and a lamp tuned as daytime dressing gave almost nothing three metres below it.

**Verified:** stills by `StreetLifeCapture` (which puts the façade up by hand: no chef plays in a batch run). Both suites pass.

**A test that turned out to depend on luck:** `CleaningPlayTests.HoldingXWithNothingToCleanPlantsTheChefAndTheStickTurnsThem` began failing in most full-suite runs once the street's scenes were rebuilt, while passing when run by itself. Logged inside the suite, the cause was the test, not the street: it stood the chef at "the first clear counter", stations register in no fixed order, and when an island came first the chef had the kitchen's dividing wall 4 cm behind them and nowhere to walk the 0.2 m it asks for. With the wall counter there's open floor and it passes. The test now picks a clear counter with a metre of open floor behind whoever stands at it (`HasRoomBehind`); what it asserts is unchanged. This is the controls-and-cleaning work's test, changed here because this work exposed it.

**To judge in play:** the façade's rise and fall as chefs cross the doorway, and whether its awning hides the queue along the front wall from the game camera; the brick paving and asphalt grit at the kitchen camera's distance (they may be too busy); whether the sign hides a chef buying at the nearest stall; the menu board when the door is on a side wall, where the wheelie bin's track passes.

**Budget:** the exterior is well over the art bible's 60,000 triangles now (seven cars at about 5,500, four houses at about 4,300, some forty trees). No frame rate has been measured.

**Not done:** the other streets (Riverside) get the same pieces in the same colours; the stalls are untouched.

---

## 2026-11-11 — The street comes alive: passers-by, a menu board, pigeons, lamps at dusk, the extractor

**The user's request:** more detail outside the kitchen, "just like we do in the city, birds, people roaming around", and no delivery scooters yet (a mechanic for later). Asked which ideas to build first, the user left the choice to me; this is the first pass I proposed. The rest is in `ideas.md` ("Street life, next touches").

- **Passers-by** (`StreetWalkers`, `StreetWalkersView`, `PF_Passerby`): six people, the customers' own archetypes without an agent, each rolled anew every time one comes round. They walk two lanes of the pavement, east at 1.25 m from the front wall and west at 0.45 m from the kerb, either side of the lane guests arrive by (2.2 m), in and out at the same off-screen ends. One stays away 2–16 s between passes, so three or four are in sight.
- **They never block anyone.** They have no bodies to bump into. They step aside (0.7 m, within the pavement) for a chef, guest or staff member within 1.3 m and come back to their lane once past; they never stop for anyone, so nobody standing in a lane can hold the street up. Reason: a chef carrying a plate must never be stopped by scenery.
- **A menu board** stands by the sign, beyond the building's east corner and clear of the wheelie bin's way round it. About three in ten of the eastbound walkers stop in front of it for 3–7 s, facing it, and walk on. One reader at a time; the others walk round them. It isn't by the door because the door can be moved and the queue forms beside it.
- **Pigeons** (the terrace's `TerraceBirdsView`, at 0.65 of the city's size): thirteen, on the plaza, the parking lot, the back fence and the sign. A chef within 2.5 m sends them up and away north over the houses, the neighbours with them; they glide back 20–40 s later once nobody's near. `_flyOffDip` may now be negative, for birds on the ground with no roof edge to drop behind.
- **Lamps and windows at dusk** (`StreetLampsView`, `StreetHours.Dark`): five lamps on the kerbstone and two houses in three light up from 19:00 on the service's clock, one after another over six seconds, and stay lit through closing and the review. A lunch never sees them. **Only lamps and windows change:** the daylight stays as it is, because a darker evening is a change of look for the user to judge in play.
- **The extractor** (`ExtractorVentView`, `StreetHours.Extraction`): a steel stack off the back wall by the east corner. It steams as hard as the kitchen cooks (nothing with the hobs off, a wisp with them hot and empty, a full plume with two hobs cooking flat out, easing over two seconds) and pours dark smoke while a pan is on fire.
- **Dressing:** the street kit's own models (the city's bench, hedge planters, hydrant, and its lit menu stand for the menu board): a bench between two planters on the plaza's west edge, planters in the back corners, a hydrant on the kerb. Doors and windows on the houses, a breeze in the trees. Plain boxes stand in where the kit isn't there.

**The same day, the user's second note:** "model adjacent scenery to close the scene so the boundaries are justified organically", and the box cars were "very underwhelming".

- **The restaurant stands in a street now.** A block of the city's shops and flats (`RowBuilding`, through `OfficeBuilder.StreetBuilding`) stands on each side, fronts on the same pavement, running 22 m past the grounds either way. The plaza and the parking lot end at their side walls, which are solid, where they used to end at an invisible wall on open grass. The nearest building each side is two storeys (7.8 m), so the camera's view of the plaza and the lot stays open, and carries a mural: the steak over the plaza, the sunset over the lot. Their shops' own lamps are left out: this street is lit by its day.
- **A park across the road:** a far pavement, a clipped hedge with a gap every nine metres, trees behind it, benches looking over the road. Low on purpose, as it lies under the camera. The kerb is still the limit that way; the traffic is the reason.
- **The road, pavement and grass run on** to the ends of the neighbours' blocks.
- **Cars are the city's models** (`SM_Cars`: sedans, hatchbacks, taxis, vans), five on the road, and they keep right: the lane by the restaurant's kerb drives west. Two are parked in the lot's bays and are solid.
- **The cars are remodelled** (the user, of the slab-and-wedge models: "awful"; a tram instead was offered and kept for another restaurant). `build_cars` in `blender/build_pilot_models.py` now makes toy cars: a body lofted through rounded sections from nose to tail, fat fenders over bigger wheels (0.41 m), a cabin of glass all the way round under a separate roof, round headlights, wrapped bumpers, number plates. Shorter and wider than before (the sedan 4.25 m by 1.9 m, was 4.5 m by 1.84 m). Mesh names are unchanged, so the opening's street gets them too when the office is next built. About 5,500 triangles each.
- **The street is seeded** from the building's size, so each restaurant's cars and neighbours come out the same on every build.

**To judge in play, for these:** whether the neighbours ever come between the camera and a chef at the far edge of the plaza or the lot; whether graffiti tags on the neighbours' shutters suit this street; the parked cars' size in the lot.

**Where things stand, and why:** everything is placed from the building's walls, so every restaurant's street gets it. Nothing stands in the guests' lane, their queue along the front wall, or the bin's track; lamps and the hydrant are on the kerbstone, beyond where chefs can walk.

**Verified:** EditMode `StreetWalkersTests` (lanes, coming round again, stepping aside without stopping, staying on the pavement, the board, one reader, the dusk hours, the extractor). Stills by `StreetLifeCapture`, run by name.

**To judge in play:** whether six walkers distract during service (`_count`, `_awayMin`/`_awayMax` on Passersby); whether a walker is ever mistaken for a guest; the pigeons' flight from the ground; the lamps' pools on a street that's still in daylight; whether the stack reads from the kitchen camera.

**Design doc:** §customers and §economy (suppliers) don't describe the street beyond the stalls. A line on the street's life would belong there.

---
## 2026-11-11 — The opening, seventh pass: couriers that leave, a shorter climb that lands with the lift, the flat-band lamps on trial

The user's notes after playing the sixth pass:
- **Waiting couriers get their order and go** (`CourierDeparture`, begun when the camera stops at the entrance): 2.2 s after the stop the courier sitting on the first scooter puts the phone away, takes the bars and rides off east through the gap in the barriers; at 5.5 s the one standing between the scooters walks to the second, gets on and follows. Once only: they don't come back while the menu is up. The riders who come and go along the street carry on as before.
- **Someone walked through the lens.** The walkers on the south pavement had no rule keeping them out of the avenue's mouth, where the camera comes through and stops on that same pavement. They now keep 3 m clear of the mouth either side, as the north pavement's keep clear of the tower's front.
- **The climb is 9 s, from 12 s, and lands with the lift.** Three changes:
  - *Pace* (`OpeningPath.ClimbEase`): the same gentle start, but the second half is 1 − (1 − u)^1.5 where it was a smoothstep, so the camera no longer creeps the last quarter onto the landing.
  - *Turn:* the camera starts turning onto the floor's view from 55% of the path (it was 86%), while it is still climbing the top floors, so it comes over the cornice already looking down instead of rising first and tipping after.
  - *The lift* (`OpeningPath.LiftClimb`): beside the camera up to 14 m under the top; from half-way through the climb's time it covers those last floors on its own, quickly at first and slowing, and comes up into the floor's elevator at the moment the camera reaches the wide shot. Before, it arrived about three quarters of the way through and the camera spent three more seconds settling. **Cost:** for the last four seconds the cab is not beside the camera; it is inside the tower under the cornice, out of sight, until it comes up through the floor.
- **The helicopter flies low over the floor itself** as the camera lands: east to west over the middle of the floor, 5 m up, crossing the middle of the wide shot at about the moment of landing and gone a second or two after. Up in the sky it was in view for under a second at the cornice and the user never saw it. It passes over the play area while players get control; say if that's too much.
- **The lamps' flat bands are on for the street and the tower, in play** (3 bands), so the finish can be judged there: the user couldn't find it, because it was only in captured stills. Between half and four fifths of the way through the climb they fade back to the smooth falloff (`_GameLampBandMix`, `OpeningSequence` "Lamps"), so the boss's floor keeps the smooth lamps the user chose on 2026-11-09: tried on the floor itself, three bands left dark blotches on its carpets. On trial: Lamp Bands on the scene's LampLighting object, 0 for smooth everywhere. To be confirmed or put back by the user.

## 2026-11-11 — The opening, sixth pass: a lift that stays in frame, taller floors, the helicopter, couriers riding

The user's notes after playing the fifth pass ("overall feels good, with some notes"):
- **The lift stays beside the camera.** It chased the camera's height on a half-second spring, which at the climb's top speed (about 15 m/s) left it 7 m behind and out of the frame. Its height is now a function of the camera's (`OpeningPath.LiftHeight`): locked 1.1 m below it, with no lag. Only the first moment is eased (`_catchUp`, 0.35 s).
- **It arrives where the camera lands.** The cab's top stop is the boss's floor itself (it was the storey below): it comes up through the cornice into the floor's elevator and eases to a stop over the last 4 m either side (`_settle`), a parabola from the camera's pace down to none, while the camera crests the cornice and tips onto the wide shot. After landing, the chef steps out as player 1 as before; the empty cab sinks back down the shaft and switches itself off 12 m down (it used to be switched off with the street).
- **Taller storeys:** 4.6 m, from 3.4 m, in the same 120 m tower: 24 floors where there were 33, ceilings 3.7 m. Each floor stays in the frame about a third longer. The climb's 12 s and its path are unchanged. (The user offered either fewer floors or higher ceilings; in a tower of fixed height they are the same change.)
- **The helicopter is called over** (`OpeningFlyby`). It never left the scene, but on its 75 s lap of the skyline it was in the camera's view at the top only by luck. Now, halfway through the climb's time, it leaves its lap for one 7 s pass: in from the east, low past the terrace rail (a few metres over the floor's level), away to the west. The camera looks level only for a moment as it crests the cornice and then tips down, so a pass higher in the sky left the frame at once; this one shows against the skyline at the crest and over the roofs after. Then it goes back to its lap. A skip before that point leaves it on its lap.
- **Movement from edge to edge in the street** (`StreetTraffic`): a courier rides in from the east, brakes to the kerb by the restaurant, waits 5 s for the order and rides off west; another rides straight through from the west; eight people walk the whole street, four each way, some in a hurry. All start and end beyond the barriers, out of view, and come round again (`RouteMover`, `AmbientMotion.Route`). The couriers' lanes are the far side of the road, at least 6 m from the camera's stop, so nothing passes close to the lens (the 2026-11-10 note still holds). The portrait pair and the strollers in the road moved a metre or two south to clear the lanes.
- **Window cleaners on the tower's front:** a cradle hung from the cornice east of the lift, two cleaners wiping the glass, passed about two thirds of the way up.
- **Not decided: the finish of the city and tower** (the user: "too 3D" beside the kitchen). The likely cause is the lamps: on 2026-11-09 the user chose a smooth falloff for them in the office, and the street and the tower's rooms are lit almost only by lamps, so nearly everything there carries a gradient where the kitchen has flat bands. A trial dial (`LampLighting._lampBands`, the global `_GameLampBands`; 0 = smooth, as now) steps the lamps' light into flat bands. *Tools/Game/Capture Office* renders the street and the floors with 3 and 5 bands (`look_*_bands*.png`) beside the smooth ones, for the user to choose. Nothing changes until they do.

## 2026-11-11 — Into and out of service: a 4-3-2-1, a "Service!" band, and a shutter behind the dashboard

The day-transitions item of the playtest round (entry below, "Playtest round"). The look was approved from mockups (`Screenshots/day-transitions-mockup/`), with these calls by the user:
- **Countdown before service** (`ServiceCountdown`, `TuningConfig` "Service countdown seconds", 4): a 4-3-2-1 in a wooden badge at the top centre, over the back wall, with an orange ring running down each second. The last number is warning yellow. "Doors open in" sits over the badge (in the mockup it was under it; above reads better: "Doors open in… 3"). **Chefs move and work freely during it.** Pausing stops it.
- **Guests wait on the doorstep.** While the countdown runs, the service holds its doors (`ServiceSimulation.HoldDoors`): its clock stands still and nobody comes in or loses patience. The first party sets off up the street at once and waits at the door. At zero (`OpenDoors`) they come in and the clock starts; the next party comes a usual gap after that. Pulling the first party forward makes for about one more party a day than before.
- **"Service!"** at zero: an orange-and-wood band, tilted slightly, sweeps across the top third of the screen, the title stamps in, and it sweeps out after about 1.3 s.
- **Closing time:** as the service ends, a wooden roller shutter drops over the screen with a small bounce and a swinging "Closing time" sign. The dashboard rises over it, and **the shutter goes out of focus behind the dashboard** (the user's call: not the kitchen) for the whole Review. The dashboard's own blurred kitchen photo is turned off while the shutter is behind it. Its dark tint stays.
- **The next morning,** once Start day N is held: the dashboard drops away, the shutter comes back into focus with "Day N" on its sign (the user's call), then rolls up on the kitchen for Prep. A new restaurant's day 1 opens the same way.
- Timings and colours are on the `DayTransitions` component (shutter drop 0.55 s, sign 0.4 s, dashboard rise 0.4 s; morning 0.35 + 0.35 + 0.6 s). The shutter is one quad with a small UI shader (`SH_UiShutter`, slats with a blur setting).

**Not checked by playing:** whether 4 s feels right, whether the band hides too much of the back stations for its second, and whether the chefs moving under the shutter for about 1.3 s in the morning matters.

---

## 2026-11-11 — The kitchen HUD clean-up: debug behind F1, hints on the appliances, guests' bubbles, nothing over the dashboard

Built from the playtest round's HUD call (below).

- **Debug panels behind a developer toggle:** the cooking overlay and the served-dish quality card show only with `DeveloperHud.Visible`, off by default. F1 flips it. In the Editor it's remembered between Play sessions; a build always starts with it off.
- **No control cards on counters.** `PromptHud` now shows a card only while a menu is open: a fridge's, shelf's or stall's slot diamond while RT is held, or a tray's menu. Those are choices, not hints. The list cards ("LT RT heat / X flip / A take pan") are gone.
- **Button pictures on the appliances** (`ApplianceHints`): LT RT at a hob (heat) and at storage or a stall (browse), X at a hob or grill with food in the pan (flip) and at anything with dirt, a sink with plates, or a fire (clean). They use the control-glyph kit's dark trigger and button bodies, with the letters in the HUD's bold font. X is in Xbox blue.
  - **Where they sit:** on the appliance's top at the edge across from the chef, LT RT then X, facing the camera, so they read at all 8 rotations with the chef on any side. They're drawn over the chef, as the wash ring is (`Game/FX/HintOnTop`).
  - **When:** they fade and pop in (0.2 s) while a chef faces the appliance and the action is possible. Each stops showing for a player once that player has done it 3 times (`_usesBeforeFading`, tunable). The count covers this play session only and isn't saved.
- **Nothing of the kitchen over the dashboard:** the dashboard's canvas is nested in the HUD canvas, and without `overrideSorting` it drew under whatever the HUD made later (the cutting dial and "press X in the green", the guests' edge markers). It now sorts on its own. `ReviewHidesKitchenHud` also hides the whole kitchen HUD layer while forkcast is open: dials, gauges, tags, tickets, markers and the bell. Screens with their own canvas (the dashboard, shop cards, the boss's phone) are left alone. The cutting dial also ends for a chef once the dashboard is up.
- **Guests speak in bubbles** (the user's call, approved through Main build): the dark order ticket and its patience bar are replaced by a speech bubble over each seated guest. It's cream paper with an ink outline and a tail, showing the dish's picture (its name if there's no picture), the doneness word in its colour (darkened for paper), and "+" and the side's picture.
  - **Patience:** below 60% patience the bubble warms towards the patience colour, up to 60% of it. Below 35% it rocks about its tail, up to 9° at 3.5 rocks a second, each guest out of step. All are tunable on `OrderTicketHud`.
  - **Queueing:** a guest queueing in view gets a "…" bubble that warms and wobbles the same way.
  - **Never overlapping, never clipped:** bubbles nearest the camera are placed first; one that would overlap steps up above the other, or along the top edge if stepping up would leave the screen. All of them are kept inside the screen's margin, with room for the wobble. A guest out of view has their edge marker instead of a bubble.
- **The quality-score card over the pass** stays behind the developer toggle (above). The off-screen guest markers and the review cards where guests leave are unchanged.
- **Not covered:** floor spills (nothing to sit on), ringing the bell (X at the pass), and cutting (its dial still says "press X in the green" until the first cut). Each was left out on purpose and is an open question for the user.

**Reason:** the user found the kitchen cluttered and the floating panels abrupt. Hints built into the appliances keep the scene readable and go away once learnt.

---

## 2026-11-11 — Flipping and cutting get in-scene timing gauges: a crust strip and a knife dial

**User's calls:** the flip and cutting HUDs didn't match the scene, and the flip's X felt detached from its gauge. These stay timing games (X in a cadence to cut, X at the right moment to flip), not stick games like cleaning. Rings were rejected in favour of bars and gauges. From three mock-ups each, the user chose **flip C (crust strip)** and **cut A (half dial with a knife needle)**, judged in the real scene and then made about a third smaller ("size is right").

- **One family look, not one mechanic:** both are chunky toon pieces in the scene (`Game/ToonOnTop`, as the wash and cleaning rings) with an Ink outline. They face the camera, so they read at every rotation, are kept inside the screen, and sit on the far side of the station from the chef so the chef's body never covers them. They're sized in screen pixels (strip 150 px wide, dial 140 px at 1080p), so they look the same in every kitchen. The Xbox X sits on the window to press in, so there's no caption. Grades show as a shape and a colour: ✓ good, – off, ✕ bad (`TimingGaugeKit`).
- **Flip, the crust strip** (`FlipCrustGaugeView`, replacing `FlipGaugeHud`): the strip is the crust's own colours (raw, golden, brown, dark, charred). A white marker follows the browning of the side facing down. The golden window is framed in green with the X under it, and both pulse while the marker is in it. On the flip, the existing pan toss throws the steak, and a mark pops over the window: ✓ golden, – pale (early), ✕ scorched (late). The mark is feedback only, from the browning the moment before; scoring is unchanged. Then the strip becomes the doneness strip. Near burning, the dark end flashes red and the strip shakes.
- **Cut, the knife dial** (`ChopGaugeView`, replacing `ChopDialHud`/`ChopDialView`): a wood-dark half fan with the sweet spot (green core, amber edges) and the X just outside it. The needle is a knife. Each cut flashes the fan in the grade's colour (a pop for good, a shake for a miss), leaves a tick where the blade was and pops a mark over the X. Pips in an arc inside the fan fill with each cut's colour. The "press X in the green", "faster" and "slower" texts are gone; the knife speeding up is the only streak cue.
- **Wiring:** `TimingGaugeArt` (Tools/Game/Apply Timing Gauges, also called by the kitchen builder) adds the views and removes the flat HUD gauges. `GreyboxHud` no longer adds them. The old HUD classes are still in the code, unused, until main has the new views.
- **Verified:** EditMode (`TimingGaugeTests`: grading) and PlayMode (`TimingGaugePlayTests`: a steak flipped golden with X shows ✓ and then the doneness strip; X cuts at the board; a board on the middle island keeps its dial). `BoardAndKnifePlayTests` now counts the new dials. Captures with `TIMING_CAPTURE=<folder>`. **Not yet played by hand.**
- **Later:** a slice falling from the board on each cut (art phase); a thicker outline if the 2 px Ink reads too thin at this size.
- **The sweet spot steps along (user's call, options B + C):** after each **good** cut it moves one step along the arc, as if working down the ingredient. It starts towards the roomier end and turns back at an end, keeping the whole early/late band on the arc. A miss or an early/late cut leaves it where it is, so a slip can be recovered. The step is per ingredient (`IngredientDefinition._sweetSpotStep`): onion 24° (rough rings), onion rings 16° (fine dice); 0 keeps it still. The minigame complexity dial scales it (`MinigameComplexity.SweetSpotStepScale`): none up to 0.25 (relaxed: pure cadence), as tuned at 0.5, 1.5× at 1. The arc slides to its new place over about 0.12 s. Tests: `SweetSpotStepTests`, and the cutting play test checks that a good cut moves it.

---

## 2026-11-11 — Controls and cleaning from the playtest: gentler turning, a facing assist, hold X for every cleaning job, washing by circles

Implements the movement and cleaning calls of the playtest round (entry below, "Playtest round").

- **Turning has its own, slower speed** (`TuningConfig`, Player movement): 600°/s at full push (was 1080), and a light push turns at only 35% of that, rising straight to all of it at full push (`_lightPushTurnShare`). A tenth of a second's nudge at a fifth of a push now turns the chef under 30° (was 90°), so lining up at a counter doesn't swing them off it.
- **Facing assist** (`TuningConfig`, Facing assist): once the chef has stopped (no stick, and almost no speed left), they turn at 300°/s to face the station they're targeting if its centre is within 1.3 m and within 40° of where they face. It never acts while the stick is pushed. It works off the chef's current target, so walls and the indoor/outdoor rule still apply.
- **Hold X for every cleaning job.** Washing, wiping, mopping and putting out fires all start from an X press that isn't taken by the station's own action (flip, chop, ring the bell, a storage slot). While X is held the chef is **planted** (`PlayerInteractor.IsPlanted`): during a job the stick works it; between jobs it turns the chef on the spot, and whatever comes into reach (the sink with a dirty plate, dirt in front, floor dirt at their feet, a fire) starts by itself. Holding X with nothing to clean plants the chef too, so they can swing round to find it. Letting go of X, or B, stops and frees them. B also ends the hold, so a job backed out of doesn't restart. The sink no longer toggles on a second press. A finished patch no longer chains only to the same tool: the planted hold starts whatever is next.
- **Washing is circling the stick** (`WashGame`, replacing the balancing game). It's counted by the cloth's own spin counter: either direction, any rough shape (a square, a squashed oval, a small circle past the 0.4 push), and wagging, holding or slamming the stick does nothing. 4 circles a plate at up to 1.5 circles a second, about 2.7 s a plate, as before. The push and the turn-back are shared with the cloth and mop (one control). The complexity dial scales them as it does the cloth (lighter push and faster circles count at low complexity). The old balance tuning (stick force, instability, nudge, radii, pushes per plate) and the dial's "corrections span" are gone.
- **One look for the whole family.** The sink keeps its ring of 15 grime-to-water segments and its soap, but the soap now follows the stick round the ring, as the cloth does on a table. It turns green, the ring pulses and bubbles rise while the circling washes; the plate still tips towards the soap. The centre target is gone. The stick cue is the cloth's spinning cue with the same timings: it shows 0.15 s after starting until the stick first washes, and again after 0.8 s stalled. In turn the cleaning ring gains the sink's pop-in and its finishing flash: the full ring flashes white and pops when a spot comes clean or a fire goes out. Wiping and mopping now read like washing: hold X, circle, watch the ring fill, see the flash.
- **Verified:** EditMode tests (new: `WashGameTests` rewritten for circles; turning, planting and the assist in `PlanarMoverTests`; the dial in `RealismTests`) and PlayMode `CleaningPlayTests` (new: washing a plate by circling, with the chef planted; X held with nothing to clean plants and turns; turning onto dirt with X held starts wiping). **Not yet played by hand.**
- **To watch when playing:** whether 600°/s with the 35% light push feels responsive enough when walking normally; whether the assist ever turns a chef somewhere they didn't want; a chef still spinning the stick after a job ends, with nothing else in reach, turns round on the spot. If that feels wrong, a short pause before the stick turns them is easy to add. When the next patch starts at once, the finishing flash is cut short.

**Design doc:** §15.8 / the dirt paragraph still says cleaning is "different from the sink's balancing game". Washing and cleaning are now one family of hold X and circle the stick.

---

## 2026-11-11 — The Restaurant tab shows plates, chips and a price dial

**User's call, after seeing the first render:** the menu should be more exciting and more visual, with pictures of steaming plates rather than just text, ingredients as chips, the price as a dial with its neighbouring numbers, and a schedule selector that isn't just text. The "Suggested" chip covered the price change.

**Decision:**
- **Dish pictures:** each recipe is plated from the game's own models, cooked (a medium steak, golden onions), and photographed by *Tools/Game/Art/Render Dish Pictures* into `Art/UI/Dishes` and `HudStyle` (by recipe id, so no scene rebuild is needed). The list shows each plate; the chosen recipe's plate is large, top right. Steam rises from a dish while it's on the menu; a dish still to unlock is faded and cold.
- **Ingredients are chips** with their pictures, and the cost a serving follows them.
- **The price is a dial:** the price big in the middle, with neighbouring numbers either side, shrinking and fading like a wheel seen edge on. Each step rolls it to the new price (`_dialRollSpeed`). The suggestion, its % change and the X chip have their own line, so the chip no longer covers anything.
- **Opening times are tiles** with a sun (Lunch), a moon (Dinner) or both (All day). Under them, the day is drawn as a bar per half hour: flat while closed, as tall as the guests coming while open, with the rushes in gold. While the tiles have the focus, the detail panel shows the same day bigger, and the guests to expect as a crowd of little figures.

- **The unlock moment (the user's pick from the proposals):** A on a locked dish buys it, and the dish warms up over 1.3 s (`_unlockWarmSeconds`). Its plate's colour comes back as it swells a little, its steam fades in, gold sparks burst from the big plate, and its card lights gold while "On the menu" pops in. The money chip drops as it does for any purchase. `ForkcastPlanPlayTests.UnlockingADishWarmsItUpAndSettles` covers it; `DashboardCapture` holds it a third of the way in for a still.

**Placeholders:** the sun and moon are drawn in code, the steam is soft puffs, and the dish photos use the greybox models. All of them are for the art pass to replace (the art bible's 64 px icon grid).

---

## 2026-11-11 — Opening times: three slots, a faster clock, two rush hours

**User's calls:**
- **The Restaurant tab sets the opening times**, which set how long service lasts and how many guests come.
- **An hour on the clock is 30 real seconds** (it was 60). **Only the clock speeds up:** cooking, staff, guests' patience (35 s) and eating (15 s) keep their real-time feel, and `TuningConfig.TimeScale` stays 60.
- **A long service includes lunch,** from 12:00 to 00:00.
- **A longer service has two rush hours instead of one,** around 14:00 and 21:00.

**Our calls within those (flagged for play):**
- **Three slots** (`ServiceConfig`, "Opening times"):

  | Slot | Hours | Real length | Rushes | Groups expected* |
  | --- | --- | --- | --- | --- |
  | Lunch | 12:00–16:00 | 2 min | 13:00–15:00 | about 6.6 |
  | Dinner (default) | 18:00–00:00 | 3 min | 20:00–22:00 | about 9 |
  | All day | 12:00–00:00 | 6 min | both | about 18.6 |

  \*At the grill's base rate and suggested prices, before group sizes. Today's 5-minute 18:00–23:00 service brought about 15.
- **Rushes are clock hours,** an hour either side of each peak (`_rushHours` 14 and 21, `_rushHalfWidthHours` 1), at the existing `_rushMultiplier` (2×). Outside them guests come at the existing pace (a group every 25 real seconds, before prices and the restaurant's guest rate). The old rush, a fraction of the service, is kept only where no opening times apply (tests, old scenes).
- **Last orders are 30 minutes on the clock** (15 real seconds) before closing.
- **When it counts: the next service.** In Review that's tomorrow's; in the morning's Plan it's today's, since the doors haven't opened yet (`ServiceSimulation.SetOpening`, as the menu is set). The tab says which.
- **Saved per restaurant** (`RestaurantSave.Opening`, a slot id). An older save, or an id no longer in the list, opens for Dinner. Like prices, a change in Review saves after a moment; one made in Plan is kept with the day.
- **Player 1 sets it,** as with the menu.
- **How it reads:** an "OPENING TIMES" card sits over the recipes, with one chip per slot (the chosen one dark) and its hours underneath. Up from the first recipe reaches the chips; left and right choose at once, with no A press, the same way the Overview's filters work; down goes back to the recipes. While the chips have the focus, the detail panel shows the slot's hours, how many real minutes service lasts, its rush hours and the guests to expect at today's prices and tables (the same forecast as Prep's tile). The Overview's "Closed · Opens" card, the day card and Prep's tile follow the chosen slot.
- **The clock everywhere is the service's** (`ServiceClock`): the day card, the debug overlay, and the "left at" time in reviews. A walk-out's "waited N minutes" stays in patience minutes, the time the guest actually waited.

**Not done:** wages and rent don't depend on the hours open, so All day costs only the players' effort and energy (in `ideas.md`).

**Tests:** `OpeningTimesTests` (EditMode) covers the slots, the clock, the rushes, the guest counts, arrivals really doubling in a rush, and a choice made before opening retiming the service.

**Design doc:** §daily loop and §customers describe one 5-minute dinner service with a single rush. They should describe the opening slots and the two rush hours.

---

## 2026-11-11 — forkcast: the Restaurant tab, and the D-pad steps to the obvious neighbour

**User's calls (playtest round):** the Cookbook tab is now **Restaurant** (Spanish "Restaurante") and comes second: Overview, Restaurant, Books, Staff. Browsing should move between neighbouring cards instead of jumping.

**What made it jump:**
- **One repeat timer for everyone.** With a second cook joined and idle, the idle pad reset the timer every frame, so a held D-pad from the first cook stepped every frame and a short press raced to the bottom of the list.
- **Lists that moved under the highlight.** The reviews worked out their scroll from the top again on each step, and the recipes re-centred on each step.

**Decision** (`ForkcastNavigation.cs`, plain simulation code):
- **Each player's D-pad steps on its own timing** (`NavRepeat`): a fresh press steps at once, holding repeats (0.22 s, as before), and a new direction steps at once.
- **Explicit neighbours** (`ChipsOverList`): a tab is a row of chips over a list, or just a list. On the chips, left and right go to the next chip (no wrapping) and down to the top of the list. In the list, up and down go to the next item, and up from the top rises to the chip in force. Left and right on an item adjust it (a recipe's price) and never move the focus. The Overview's filters and reviews, the Restaurant tab's recipes and the Staff rows all follow it.
- **Lists scroll only as far as the choice needs** (`ScrollWindow`), and never leave a gap below the list's end.

**Tests:** `ForkcastNavigationTests` (EditMode); `ForkcastPlanPlayTests` checks the tab order and a short press with an idle second cook moving one row.

---

## 2026-11-11 — A ring in each player's colour at their feet, which goes with the cursor in rearranging

**User's call:** in the boss's office and during Prep, each chef shows a circle on the floor in their player colour (`CoopSettings`, the same colours as the HUD and player bar), so players can tell who is who. In Prep, a player who goes into rearranging takes their circle with them: it leaves their chef and shows under their cursor, and comes back to the chef when they leave.

- **Service and Review show no ring** (my default, as the request names only the office and Prep): in service the floor is busy with dirt, spills and staff, the tinted bodies already tell chefs apart, and a ring per chef would add clutter just when the floor needs reading. Easy to change if playtesting wants them (`FootRingView.Showing`).
- **Look (placeholder):** a band 9 cm wide with an outer radius of 48 cm, 90% opaque, round a faint 22% disc; unlit and transparent without depth write (`SH_FootRing`), 3 cm above the floor so it never z-fights with the floor or the dirt decals. The chef's body hides the middle of the ring from the overhead camera, so it reads as a halo round the feet. All of it is tunable on `FootRingView`.
- **Under a cursor the ring draws over furniture** (depth test off), so it still shows where the cursor is over a counter or table. The cursor's own floor frame hides under counters as before.
- **Built by the builders:** `FootRings.Add` puts a `FootRings` object in each kitchen (Tools/Game/Build Greybox Kitchen) and in the office (Tools/Game/Build Office). The ring isn't part of `PF_Player`, so the chef's portraits and stills don't pick it up.

**Reason:** four tinted chefs can still be hard to tell apart from the overhead camera, more so in the office's busier rooms; a coloured ring on the floor is the genre's usual answer. Moving it to the cursor makes it clear whose cursor is whose, and that the chef standing still is "parked".

---

## 2026-11-11 — Cleaning and fire: spin and sweep, with the sink's ring and stick cue

**User's call:** the traced-path game for the cloth, mop and extinguisher was barely playable, at times almost impossible. Of the two options (reuse the sink game, or keep "moving" but apply the sink's principles) the user chose spin and sweep.

- **Cloth and mop: spin the stick round, either way** (`TraceGame`). No path to stay on: any turning of a stick pushed past 0.4 cleans. Only new turning counts, so wagging back and forth adds nothing; spinning the other way counts once the stick has clearly turned back (270°). The mop's figure-eight is gone (its crossing was a trap): it spins too, slower.
- **Extinguisher: wag the stick from side to side.** Each leg from one side (0.55 out) to the other is half a loop, so a fire's three loops are three sweeps there and back. Jiggling in the middle adds nothing.
- **Faster cleans faster, up to the tool's speed** (cloth 1.1, mop 0.7, extinguisher 0.9 loops a second). Motion over that waits in a small buffer (half a loop) rather than being thrown away, so a sweep's quick middle still counts; a stick jumping faster than a hand can move it (30 rad/s turning, 25 units/s sweeping) was slammed and doesn't count.
- **Shown as at the sink:** a ring of 15 chunky toon segments round the dirt (or over the burning pan) fills clockwise from the top, from grime to water (flame to foam for a fire), drawn over the chef; the tool's head follows the stick, so the cloth visibly circles. The beads, chevrons and marker are gone.
- **The stick cue** (the sink's flat stick, now shared through `IStickHintSource`): spinning for the cloth and mop, wagging for the extinguisher; it shows 0.15 s after cleaning starts until the stick first cleans, and again whenever it stalls for 0.8 s.
- **Amounts are unchanged**, as are hold X to start and X or B to stop. The complexity dial now lowers the push needed and the sweep's width, and raises the speeds.
- **Verified:** the EditMode tests and the cleaning play tests (a fire put out with the extinguisher, a spill mopped, a table wiped), driven by a hand spinning or wagging a loop a second. Not yet played by hand.

---

## 2026-11-11 — Review cards: the customer who came, as a conversation

**Decision (user's calls):**
- **The guest's face is the customer they came as.** Each customer wears one of the customer prefab's 3D looks, picked by the simulation (`ServiceConfig` "customer looks", which a test keeps equal to the prefab's). The review saves it, and a returning guest is drawn only from guests who wore the same look, so they come back looking the same. Tools/Game/Bake Customer Portraits photographs each look into a sprite on `HudStyle`; reviews from older saves pick a look from the guest's name.
- **Option B, speech bubbles.** Left, the portrait, the name and the guest's type (regular, local, tourist, foodie, influencer, food critic; critics get a gold chip and ring). Right, what they said in a bubble, without quotation marks; the owner's answer in a dark green bubble from the other side, bigger and without a heading; then one line of facts: rating, order, what they paid, and "back again". The time of day is gone.

**Reason:** a drawn icon didn't connect the review to anyone the players saw in the room, and the old card's type sizes and spacing didn't sit well together. The guest type lets players spot a critic's or an influencer's visit.

**Cost:** cards are taller (about 190 px), so the list shows about two and a half at once. A returning guest is a little rarer, since they must have worn the same look. A guest's type and look are still picked separately (a "tourist" can wear gym clothes).

## 2026-11-11 — A full bin's flies are an urgent swarm

**Decision:** The bin's flies grow with it: from half full, a few small ones (6 cm), more and bigger as it fills. Once it's full they jump to a swarm: 14 a second plus a burst of 8 about every second, 11 cm each, circling twice as wide (0.32 m) and over twice as fast (`BinFillView`, "Full: a swarm"). Before, at most about ten 5 cm specks circled it, which went unnoticed.

**Reason:** The user's note that the flies weren't noticeable enough and that a full bin should feel urgent.

---

## 2026-11-11 — Full fridges and shelves show the same 🚫 as the bin

**Decision:** A fridge or shelf whose every slot is at capacity shows the bin's 🚫 sign over it, popping in, bobbing and rocking when a player holding something faces it (`FullSign`, shared by `BinFillView` and `StorageView`). A unit with room left in a slot isn't "full", even if it refuses what someone holds because all its slots hold other ingredients; there the "put back" prompt is already greyed out. The sign sits just above the unit's top.

**Reason:** The user's note that the same symbol should show when fridges or shelves are full.

---

## 2026-11-11 — Stalls sell one fresh portion at a time; old food shows its age

**User's calls:**
- **Each press at a stall buys one portion,** at the ingredient's own price (`IngredientDefinition.Price`), straight into storage as before. It's no longer up to 6.
- **No cheap and premium grades.** A stall sells each ingredient once; the butcher has steak, the greengrocer onion and tomato.
- **Brand-new stock is always 100% fresh.** `MarketConfig` and the grades' starting freshness are gone.
- **Old food is visibly old.** Below 35% freshness it looks stale: it dulls towards a greyish brown, with a clear step as it crosses the line and more as it ages. Below 10% it goes mouldy: a grey-green cast and fuzzy grey-white and green spots. It shows everywhere food is seen: loose, in pans, on plates, and on the storage bins' samples, which show the oldest portion (the one taken next).

**Details (proposed while building; flagged for review):**
- **The mouldy line is the existing spoiled line** (`TuningConfig.SpoiledBelow`, 10%), where stored food is thrown out overnight, so "mouldy" and "spoiled" mean the same thing. The stale line is a new tuning value, `TuningConfig.StaleBelow` (35%).
- **The money card shakes red for every purchase,** even when only the cents change (a $0.50 tomato). Other cent-only changes stay quiet, as before.
- **Mould is greybox spots** grown over each food's shape by the builder (`SpoilageArt`), to be replaced in the art pass.

**Reason:** Buying a portion at a time makes shopping a closer match to what the menu needs. Without grades, freshness is about how long stock is kept, which players can now see at a glance.

---

## 2026-11-11 — Customers walk in along the street and queue outside on the pavement

**User's notes:** customers appeared right in the doorway instead of walking the scene. They should walk in, or queue by the entrance. The user chose a queue outside on the pavement over the old spots inside by the door.

**Decision:**
- **Walking in:** new customers appear off screen beyond one end of the pavement, chosen at random per group. A group walks one behind another along the pavement to the doorstep.
- **The door:** the service only seats, queues or turns them away once the whole group is there (`CustomerState.Approaching`, `ServiceSimulation.GuestsWalkIn`, `ReachDoor`). Their patience and time inside start at the door, and their arrival time (the guests chart) is counted there.
- **Stuck walkers:** anyone still walking after 40 real seconds is put at the door, so a blocked street can't hold up a service.
- **The queue:** it forms outside along the front wall, from beside the doorstep towards the side with more wall. It never stands in the doorway or on the way out to the market. Someone waiting inside when the room filled walks back out to it.
- **Turned away:** turned-away guests, and queuers who run out of patience, walk off without coming in.
- **Leaving:** guests leaving after their meal are counted as gone at the door, as before, so a service still ends on time. They then walk off along the pavement to either end and disappear off screen.
- **Where it applies:** the street's two ends are set by `GreyboxStreet`. A kitchen without them keeps the old way: customers appear in the doorway.

**Reason:** a guest appearing in the doorway reads as a bug. A walk up the street shows who's coming, and a line outside reads at a glance from the overhead camera (chaos you can plan for). It also keeps the dining room clear for chefs and staff. Patience starts at the door because players can't do anything about the walk.

**Tests:**
- **EditMode:** `ServiceWalkInTests` covers approaching, patience starting at the door, seating, queueing and turning away at the door, and the service waiting for everyone still on the street.
- **PlayMode:** `CustomerPlayTests` adds the walk-in from off screen, the queue outside and the walk-off. Six existing dining-room tests open with the walk-in off, because they're about tables, groups, the waiter, pushing and leaving by the door, not the street: the two table tests, the group, the waiter, the push and the walk-out.
- **Capture:** `StreetQueueCapture`, run by name.

**Known limit:** the walk outside is straight lines, with no way round obstacles. That's fine for a door on the front wall. A door moved to a side wall would have guests walk through the plaza's stalls or the parked cars.

**Design doc:** §customers doesn't describe how guests arrive. It should say they walk in along the street and queue outside.

---

## 2026-11-10 — Washing cleans continuously, quicker nearer the centre; the rim turns from grime to water

**User's calls, after playing:** the ring should fill all the time, faster the closer the soap is to the centre; the LS cue was too small to notice; the target should be a little bigger; the rim should go from dark brown to blue, like water cleaning the grime.

- **Continuous scrubbing** (`WashGame`): the plate cleans every frame the soap is within `OkRadius` (0.6 of the circle, was 0.55), at full speed at the very centre and slowing straight down to nothing at that edge (`Rate`). 2.4 s of full-speed scrubbing per plate (`SecondsPerPlate`). The 0.3 s hold is gone.
- **Still three sides:** the plate is washed in three shares; as each third comes clean the soap is kicked to the plate's next side, about 120° apart, so every plate is still tipped every way. Measured with simulated hands: a steady hand takes about 4.1 s a plate; slamming the stick about almost never finishes one; doing nothing never cleans.
- **Bigger target:** 0.3 of the circle (was 0.25). Its arc goes green as the current third comes clean, and it's tinted in the washer's colour, which no longer shows on the rim.
- **Grime to water:** each rim segment blends from grime brown to water blue as the plate's progress passes through it.
- **LS cue:** 84 px (was 46), the cap travelling 18 px, popping in at 1.5× and settling, so it's noticed at once.
- **Then, after looking at it:** the target is bigger again and bolder: 0.4 of the circle, a thicker, taller ring in the washer's full colour (was a pale tint). The soap shrinks as it nears the centre and grows as it drifts out (60% of its size at the centre, 130% at the rim), so at the centre it fits inside the target and how far off it is reads at a glance. Scrubbing speed still depends only on distance.
- **Quicker, and bigger again (2026-11-11, the user's calls):** 1.4 s of full-speed scrubbing per plate (was 2.4), so a steady simulated hand takes about 2.7 s a plate (was 4.1); panicking still almost never finishes one, doing nothing never does. The ring is 1.05 m across its quad (was 0.8 m), about 0.88 m at the rim, spilling well past the basin onto the counter; the target and soap grow with it.
- **A louder soap and an early cue (2026-11-11, the user's calls):** the soap swells to 1.9× its size at the rim (was 1.3×; 2× was too big), quickly as it leaves the centre (size follows distance^0.6), and stays 0.6× in the centre. The stick cue now shows 0.15 s after washing starts and stays until the soap first reaches the target; after that it shows only when the player seems stuck, as before.
- The minigame complexity dial now passes `SecondsPerPlate` through instead of the removed hold time.

---

## 2026-11-10 — Menus and lists are browsed with the D-pad, not the stick

**Decision (user's call):** every menu and list steps with the D-pad, or the arrow keys on the keyboard. The left stick no longer browses them. This covers the main menu, the pause menu, the Review dashboard (browsing, filter chips, cookbook prices), the tailor, the accountant, the city miniature and the layout editor's shop list. A new `Navigate` action in the Gameplay map (D-pad + arrows, rebindable) drives them; the UI map's `Navigate` moved from the stick to the D-pad. Hints show a drawn D-pad cross instead of "LS". Walking, the layout editor's free cursor and the sink's balancing still use the stick.

**Reason:** the stick made menus hard to move around: it overshoots and repeats. The D-pad steps once per press.

---

## 2026-11-10 — A second restaurant: one kitchen scene per restaurant, Casa Lucía's dining room

**User's calls (Phase 6, milestone 3):**
- **Casa Lucía's building is wider and deeper:** 12 m wide (the grill's is 8), a 5 m kitchen and a 6 m dining room (the grill's are 5 and 4). Four tables of four chairs in a 2 × 2 grid, the 16 seats its definition already had. More covers, more walking: that's the trade.
- **Its kitchen keeps the grill's flow, wider:** storage at the west end of the back wall, then the board, the knife rack it inherits, the container rack, both gas hobs side by side and the tray rack; the bin in the north-east corner, the sink and a spare counter on the east wall, the counter with the knife on the west wall; a 6-counter island in the middle (the grill's has 4).
- **Guests come 2.4 times as often as at the grill** (its fee scale), and up to 6 wait at the door (the grill's 3).
- **Riverside, its street, is the same street kit with its own dressing:** the butcher and greengrocer open, a third stall standing closed as the fishmonger until there's fish, and its own canopy colours (teal, saffron, plum). A river is a later art task.

**Our calls within those:**
- **One generated scene per restaurant with a kitchen.** `RestaurantDefinition` gains a kitchen scene name, the room's width and depths, a guest rate and a queue length, all with defaults (the grill's). *Build Greybox Kitchen* builds the shared assets once, then one scene per definition that names a scene: `Kitchen_Greybox` (the grill) and `Kitchen_CasaLucia`. *Build Casa Lucia Kitchen* rebuilds only the second.
- **The grill's layout is today's, value for value** (`KitchenLayout.Grill`), so rebuilding it changes nothing but the sign. Every other restaurant's is generated from its definition (`KitchenLayout.Generate`): size, then its inherited appliances placed in the grill's flow. Its floor plan (`FloorPlan_Kitchen_CasaLucia.asset`) is generated beside its definition and rewritten on each build, so it always matches the walls; the grill keeps `FloorPlanConfig.asset` and its tuning.
- **The kitchen door is in the middle of Casa Lucía's dividing wall,** so plates go straight down the aisle between the two columns of tables. The grill's stays at its west end, beside its bar tables.
- **A restaurant's own tables come with all their chairs** (a new `_startingChairs` on the table, 4 for Casa Lucía's); a table bought in the shop still starts with one. Selling an inherited table refunds its chairs like any other.
- **The street follows the building:** every outside position is now an offset from the outer walls, chosen so the grill's come out as before. The stalls are the definition's vendors in order; one with nothing to sell yet stands closed under its own name.
- **The sign has the restaurant's name and style,** lettered at runtime from the record (as bought or renamed). The dashboard's name, style and the reply sign-off use the same, not the Service Config's.
- **Loading the right scene:** the miniature's Open and the menu's Continue open the current restaurant's kitchen (`CampaignLaunch.KitchenScene`). A restaurant with no kitchen of its own yet (Forno Rosso and the larger lots) opens the grill's, as itself: its own fee scale, guest rate, name and record. Old saves open the grill's.
- **The restaurant being played sets the fee, guests and name,** read from its definition through the Miniature Config (`KitchenCompositionRoot.Played`), not from the scene, so a lot sharing the grill's kitchen still owes its own cut.
- **Opening a kitchen straight from the Editor** plays its own restaurant: if the last campaign owns it, it becomes current (as choosing it on the miniature would); if not, the day is a throwaway that never writes the slot, so another restaurant's record can't be overwritten with this room.
- **Idle restaurants needed no new code:** saving already replaced only the current record (milestone 1). The new tests play the grill, open Casa Lucía, save, and come back to the grill exactly as left, its fee clock where it stopped.
- **Generated, not in git:** `Kitchen_CasaLucia.unity` is built when missing, like the office (`GeneratedScenes`), offering to save the open scene first and reopening it after. The build's scene list (`BootOrder`) is the office, then every kitchen.

**To watch when playing:**
- Whether 2.4 times the guests feels like a busier place or just a harder grill, with two players and with staff.
- Whether the long walk from the pass to the far tables makes waiters worth hiring, or just feels slow.
- Whether the sign's lettering reads from the camera when it pulls out to the street.

**Not done yet:** Forno Rosso and the larger lots can be bought but play in the grill's kitchen; the miniature doesn't say so. The "{style}s near you" header reads oddly for "Spanish home cooking".

---

## 2026-11-10 — Chefs hold what they carry from outside it, and it meets their hands

**Decision:** Each item has a grip (`ItemView.CarryGrip`):
- **Under** for trays and plates: the palms are just below the base, and the item rests on them.
- **Sides** for everything else: the hands are just outside its left and right, halfway up.
- Pans keep their handle grip.

`ChefArmsView.Grip` works the hands out from the item's measured shape and the chef's reach.
- The arms are rigid and only turn, so a hand always ends one arm's length from its shoulder. The item is therefore moved to meet the hands, not the other way round.
- Its base stays at the carry point's height, so it shows as before.
- It's held nearer its back when holding it by the middle would push it into the chef's front.

**Reason:** The user saw hands going through a tray. Before, the hands aimed at fixed points 17 cm either side of the carry point, at the item's base, whatever its size. A tray's thin slab took the hands through it, and its back edge reached into the chef. `CarryGripTests` checks every carried item against a real player. `Tools/Game/Art/Capture Carrying` renders chefs holding each kind of item.

---

## 2026-11-10 — Merging milestones 2 and 4: one office campaign, and the starting loan is paid back

**Found when merging** (Main build): the two branches were built side by side and met in two places.
- **Two copies of the campaign.** The accountant read the save file and wrote it; the miniature kept its own copy and wrote it on every purchase. Borrowing and then buying would have lost the loan. Now the accountant borrows through the miniature's `OfficeCampaign` (`Lend`), the office's one copy, which saves it. `AccountantBooks` only changes the campaign it's given. The builder wires the desk to it.
- **The starting loan was never paid back.** A new campaign recorded 500 owed but no instalment, so each collection took nothing. Now it's paid back over the loan's 5 instalments (100 each).

**User's call (confirmed 2026-11-10):** the boss's 20% is added only to money borrowed at the accountant, not to the 500 he starts players with, so the starting loan is repaid at 100 a collection.

**Tests:** `AccountantTests` now borrow through `OfficeCampaign` and still check that a loan is saved at once, that the kitchen loads that slot next, and that an amount not on offer changes nothing; a new campaign now borrows on top of its starting loan instead of its starting money. `MiniatureBuyingTests` gained the starting loan's instalments and a loan then a purchase in one campaign.

---

## 2026-11-10 — The boss and money trouble: loans, collections, escalation

**User's calls (Phase 6, milestone 4):**
- **Loan terms:** the accountant lends in steps of 250, up to 2,000 borrowed in all. The boss adds a flat 20% when you borrow (borrow 1,000, owe 1,200), with no compounding. Each collection takes a fifth of what was owed right after the latest loan, so borrowing again spreads the whole debt over five more collections.
- **Tone: comic menace.**
  1. The warning: the boss and his two bodyguards walk in on the next morning's Plan and he says a few lines.
  2. Repossession: the guards carry out appliances, most valuable first, until they cover the debt, each credited at resale value.
  3. Losing a restaurant: he takes the restaurant that missed the collection.
  4. Erasure: the campaign ends.
- **Step 3 with only one restaurant:** he doesn't take it. The guards strip it to the bare minimum, and he gives a last warning. The ladder stays four steps.
- **Borrowing happens at the accountant's desk**, the room by the elevator. The boss's desk is kept for story beats. This changes `office-scene.md`, which is updated.

**Our calls within those:**
- **Partial payment:** he takes whatever is in the pocket, and the rest stays as money below zero, owed to him, as debt already was (2026-11-08). A collection is **paid in full only if the balance is at zero or above afterwards**, so older debt (stock bought on credit, an earlier shortfall) has to be cleared too. Paying part still helps: there's less to clear next time.
  - *Why:* one number to read (the balance), no separate arrears to track or explain, and "paid in full" means exactly what it says. A partial payment can't reset the escalation, or a token payment would dodge it.
- **The instalment is taken even when the money isn't there.** The loan goes down and the shortfall moves into the negative balance, so the debt is never counted twice.
- **The bare minimum is never taken:** one hob (or grill), one board, the pass, one storage and one table. The cheapest of each is kept. A repossession leaves the restaurant able to earn its way back, and stripping (step 3 with one restaurant) takes everything else.
  - Pieces with food, pans or plates on them stay put. Taking them would also mean throwing their contents away mid-save.
- **The visit happens the next morning played, in whichever restaurant that is,** so switching restaurants doesn't dodge him. A repossession is sized by the debt that morning: borrowing overnight to clear it means the guards take nothing ("Found the money, did ya?"). Step 3 takes the restaurant that missed the collection. If that's the one you're in, you're sent back to the office.
- **Nothing from the visit's morning is saved until the next Review** (the rule that leaving mid-day loses the day). The exceptions are losing a restaurant and erasure, which are written at once. Quit after a repossession, and it happens again next time from the saved state.
- **The phone needs no button.** It rings for 1.2 s, shows each line for 2.6 s and hangs up. It never competes with the dashboard or the kitchen for a press, and it never names a key. It rings:
  - the morning of a collection day, if the money isn't there yet;
  - as Review starts on a collection day, whether the collection was paid or missed.
- **Visitors walk the free floor** (the customers' pathfinding), and nobody readies up or opens management mode while they're in. A carried piece is packed down into the guard's arms (about 0.4 m), so it never sweeps through walls or furniture.
- **Erasure:**
  - the slot's file is set aside as a backup, as a new campaign does, so the slot is free;
  - a record goes into the cookbook file (`SharedCookbook.Erased`), which outlives campaigns;
  - the game goes back to the start.
  The hall of fame's portraits now show one per erased chef, so the wall starts empty and fills as players lose.
- **The save:** new fields with defaults (`LoanInstalment`, `BossStep`, `BossVisit`, `BossVisitAt`), so the save version stays 2. The Books tab's new lines (`Loan`, `Repossessed`) go at the end of the ledger, so older amounts keep their places.
- **Names:** the dashboard calls the fee "the boss's cut", and the next payment "the next collection" (cut plus instalment).

**To watch when playing:**
- Whether 20% over five collections bites hard enough to make borrowing a decision, without ruining a slow week.
- Whether the walk-in, the lines and the carry-out read from the game camera.
- Whether a packed-down appliance reads as "they took my fridge".
- Whether the phone at 2.6 s a line is long enough to read mid-Review.

---

## 2026-11-10 — The city miniature and buying

**User's calls (Phase 6, milestone 2):**
- **A new campaign borrows 500 and the grill costs 400.** The loan is recorded twice, as money in the pocket (`Balance` 500) and as what's owed (`Loan` 500), so after buying the grill the first morning starts with 100, as before. Milestone 4 takes the loan back in instalments.
- **The steep price ladder,** cheapest first:

| Lot | Id | Size | Seats | Price | Fee scale | Previous style | Street |
| --- | --- | --- | --- | --- | --- | --- | --- |
| The Copper Pan | `grill` | Counter | 3 | 400 | 1 | Grill (fixed) | Old Town |
| Forno Rosso | `forno-rosso` | Small | 8 | 2,500 | 1.6 | Pizzeria | Market Street |
| Casa Lucía | `casa-lucia` | Dining room | 16 | 6,000 | 2.4 | Spanish home cooking | Riverside |
| Le Petit Zinc | `petit-zinc` | Dining room | 24 | 12,000 | 3.5 | French bistro | Theatre Row |
| Harbour Lights | `harbour-lights` | Large | 32 | 25,000 | 5 | Sushi | The Harbour |
| The Grand Vesper | `grand-vesper` | Large | 48 | 50,000 | 8 | Steakhouse | Uptown |

  Casa Lucía is the mid-size dining room that gets a kitchen in milestone 3; the others are data until 6b or Phase 7.
- **One shared highlight** moved by any looking player's stick, and one card at the screen's right edge.
- **Naming:** the old name and suggestions over a letter grid.

**Our calls (to tune by playing):**
- **The table:** A at the table starts looking (the chef stops and faces the table). Browsing: stick or D-pad, one lot per push, to the nearest lot that way. A on a lot for sale starts buying, and that player alone names and styles it; B steps back, and from browsing stops looking.
- **The card** shows the name, street, size and seats, a chip with the price (or "Yours"), the previous style, two old reviews, what it comes with (catalogue names, "2 × Gas hob"), your money, and, if buying would go below zero, how much would be owed to the boss. Coloured dots show who is looking. It stays inside the space above the join slots, 48 px in from the right edge.
- **The name picker:** the old name on the top row ("keep the old name and its reviews"), three suggestions on the next ("a new name starts with no reviews"), then A–Z with & and ', then Space, Delete and Done. Choosing the old name or a suggestion buys at once (or goes on to the style); typed names end with Done. X deletes, Y rolls new suggestions. Letters are capitalised at the start of each word. Names are up to 18 characters.
- **Keeping the name:** a name that matches the old one, ignoring case and spacing, or an empty one, keeps it with its old spelling and its reviews (dated day 0). Any other name starts with none.
- **Style:** keep the previous style, or Fast casual, Bistro or Fine dining. These are labels only until service styles (6b). The grill skips this step.
- **Money:** buying is never refused for its price: money may go below zero, which is owed to the boss (decision 2026-11-08), and the card says so before A is pressed.
- **Current restaurant:** the first one bought becomes current; later ones wait until chosen. A on an owned restaurant makes it current, saves and starts the day there, once every joined player is at the table looking at the city (the card counts them).
- **Saving:** a new campaign is written only when something is bought, so leaving the office before buying leaves the slot empty. Every purchase and every choice saves at once (§15.4).
- **A bought restaurant that has never been played** opens its kitchen as built, on day 1, with the campaign's money and any kept reviews, and gets the free first-morning stock.
- **The grill now has two old reviews,** so keeping its name means something. Migrated saves already own it and are unaffected.
- **On the miniature** each lot replaces one block of the model city, sized by the restaurant's size, with a red flag once owned, a gold board while for sale, and a pulsing ring under a floating gold diamond on the highlighted one. *Tools/Game/Capture Miniature* renders it.

**Not done yet:** every restaurant still opens today's kitchen scene, with the grill's fee scale (milestone 3). Restaurant names, styles and old reviews are data and aren't translated.

**Reason:** the user's calls, and one shared cursor keeps a four-player table readable: one card, one highlight, one buyer at a time.

---

## 2026-11-10 — A campaign of restaurants: how the save is split

**Decision (Phase 6, milestone 1; our calls within the agreed plan):** the campaign file (version 2) holds a list of restaurant records and which one is current. Each field went where it belongs:

| Field | Where | Why |
| --- | --- | --- |
| Balance | campaign | One pocket: every restaurant pays in and out of it, and debt to the boss is the campaign's. |
| Loan (new, 0) | campaign | The accountant lends to the campaign; each collection takes one instalment (2026-11-10). |
| Played time, last played, cooks | campaign | They describe the players' time, shown on the slot card. |
| Recent review lines | campaign | The same players read every restaurant's listing, so lines shouldn't repeat across places either. |
| Unlocked recipes | campaign | Bought once with the campaign's money; cookable anywhere. The shared cookbook file is untouched. |
| Day | restaurant | Days are counted per restaurant, on days played there; the slot card shows the current one's. |
| Days towards the fee, period ledger, history | restaurant | Each restaurant has its own fee clock and cut (2026-11-10), paused while idle. |
| Stations, items, broken plates, dirt | restaurant | The room and what's in it. |
| Staff | restaurant | Staff work in a place; moving them between restaurants is a later call. |
| Reviews, dish tally | restaurant | Guests review a place, and a bought restaurant can keep its old reviews. |
| Prices | restaurant | The same steak can fairly cost more in a dining room than at a counter (service styles, 6b). |
| Name, style, area, definition id | restaurant | Who the place is. |

- **Restaurants are data:** `RestaurantDefinition` (id, name, size, seats, price, fee scale, inherited appliances, previous style, old reviews, street, vendors) becomes plain `RestaurantSettings` for the simulation. *Tools/Game/Build Restaurant Definitions* creates `RES_Grill.asset` for today's kitchen (Build Greybox Kitchen runs it too and wires it into the kitchen). The grill's fee scale is 1, so the fee doesn't change.
- **Migration:** a version-1 file is read twice, once as the campaign and once for the restaurant fields at its top (`Version1Restaurant`, frozen), and `SaveMigration.Upgrade` makes them restaurant 1 with the grill's id. The fixture `Tests/EditMode/Fixtures/campaign_v1.json` keeps a version-1 file for the test.
- **Idle restaurants:** saving replaces only the current restaurant's record; every other record is written back exactly as it was read.
- **Names:** a restaurant keeps the name and style in its record; only a record without one takes the Service Config's, so a renamed restaurant (milestone 2) stays renamed. The kitchen still shows the Service Config's name for now.

**Reason:** milestones 2 (the miniature) and 4 (the boss) both build on this, so the records stay flat and simple, and everything that differs per place lives in one record that can sit idle untouched.

---

## 2026-11-10 — Phase 6 plan: two halves, and how the campaign's money works

**Decision (user's calls, taking the recommendations in `plans/phase-6.md`):**
- **Two halves.** 6a, the empire: a campaign of restaurants, the city miniature and buying, a second restaurant, the boss and money trouble, the takeover. 6b, identity: service styles, decoration, the restaurant station. 6a is played before 6b starts.
- **The fee stays every 5 finished days,** counted per restaurant on the days played there. Idle restaurants' clocks are paused.
- **A collection covers that restaurant's cut plus one instalment of the loan.** The loan belongs to the campaign.
- **5 or 6 restaurants on the miniature,** with streets from a modular kit. Two sizes are playable in 6a, the grill counter and a mid-size dining room.
- **One kitchen scene per restaurant,** each generated by the builder from its `RestaurantDefinition`, as today's kitchen is.
- **Escalation moves one step per missed collection** (warning, repossession, a lost restaurant, erasure). Paying in full resets it.

**Reason:** each half ends with something playable, and the money model needs the empire before styles and decoration mean anything. Per-restaurant clocks keep leaving a place from being an exploit, and one step per missed collection makes the threat readable.

---

## 2026-11-10 — The opening, fifth pass: a cleaner sign that moves, the menu off the edge, couriers in uniform, floors that differ

**User's notes after the fourth pass:**
- With pops round the scene, the steak polaroid and the "chef's kiss" post no longer need to sit on the logo; they could pop round the scene instead.
- Re-distribute or resize the logo so it fits its space more naturally.
- Keep REC and its dot; the dot could blink, and every 2 or 3 seconds the like count could animate and change.
- Push the menu a little to the right, or the people behind it to the left, so they're in the background and still visible, not oddly cropped.
- Three parked scooters are too many: two, a bit more spread. The delivery rider, or at least the backpack, should be more noticeable.
- The floors each have their own scene but still feel repetitive: a plant in exactly the same place on every floor, groups spread too evenly, not enough hustle at times. And the glass may be too transparent.

**What we did (our calls within those notes):**
- **The sign is drawn in code** instead of being one picture, so its parts can move and nothing is stuck to it:
  - the board is as wide as the list under it, with the logo 8% bigger than in the mockup and centred;
  - the REC light is on for a second and off for half;
  - the like count starts at 12.4k and goes up by 100 to 300 every 2 to 3 seconds, with a small bounce;
  - the logo and the heart are cut from the Art Director's sign picture by `blender/cut_menu_sign.py`. `TEX_Menu_Sign.png` stays in the project as their source.
- **The menu stands 72 px in from the left edge** (the sign and the list together), and forkcast and the pages move 40 px right to keep clear of it. The selfie pair that stood in the road under the menu now stands back on the tower's kerb, in the strip left of the menu.
- **Player 1's chef stands on the lift's east side,** which is just clear of the menu's edge, so the chef shows waiting beside it.
- **Couriers:** two scooters instead of three, 2 m apart. One uniform, so they read as couriers at once: a lime cap and a lime bag a third bigger than before, with a white chevron patch on its back and sides, on a dark jacket. One sits on a scooter with their phone, one stands with their back to the street, one walks orders to and from the door. Lime was chosen because it's none of the four player colours.
- **The photo post** (a polaroid of a plate, with a tag) joins the cards that pop over phones. The review text itself doesn't: text in the world is built into the scene and couldn't follow the language setting, so reviews there stay a name and stars.
- **Floors:** each floor throws its own dice (seeded by its number, so a rebuild gives the same tower): mirrored or not, how many desks are taken, who stands over whom, one of seven wings, how many walk the window and how fast, where the plants are, and whether it has gone home for the night. Listed in `docs/opening-flow.md`.
- **Glass:** a new shader for the panes over the real rooms (`SH_PaneGlass`): a 24% cool tint and pale diagonal reflections laid out in world space, so they run on from pane to pane instead of repeating in each. The street floor keeps the clearer glass.

**A capture bug found on the way:** the stills freeze some camera flashes "on", and sized each flare by its parent's scale. Under a mirrored room that scale is negative, which blew one flare up to kilometres and washed the whole frame white. The game itself already used the absolute scale; the capture tool now does too.

**Reason:** the user's notes, one by one.

---

## 2026-11-10 — Customers roll a hat, and some have long hair

**User's call:** the in-game customers get the modelled hats and the long hair too ("Yes, do it", after the opening's fourth pass).

**What we did (our calls, to tune by playing):** a hat is now one of the things rolled per customer, like their hair, clothes and prop. Each archetype keeps its own mark as the likeliest and gets one or two that suit it:

| Archetype | Hats (most likely first) | Bare-headed | Hair added |
| --- | --- | --- | --- |
| Regular | cap, cowboy hat | 1 in 5 | none |
| Office | headphones | 4 in 5 | long |
| Date night | plumed hat | 4 in 5 | long (likeliest) |
| Tourist | sun hat, straw hat, captain's cap, cap | 1 in 10 | long |
| Retiree | cap, straw hat | 7 in 10 | long |
| Gym | headband, headphones, cap | 1 in 10 | none |

- **The crown, party hat, propeller cap, horns and headdress stay with the players:** they're the tailor's prizes, earned before they're worn (2026-11-09), and a room full of them would cheapen that.
- **The under-hat rule holds for rolled hats:** under a hat that covers the head, the top of the hair is hidden and only the back, the locks at the nape and long hair show (`CharacterRoll`, checked by `CustomerRollTests` on the built prefab).
- **Rebuilding:** *Tools/Game/Rebuild Customer Prefab* rebuilds only `PF_Customer`, without rebuilding the kitchen.

**Reason:** the user wanted customers told apart at a glance. Hats are kept to a few per archetype because the archetypes are read by their clothes, prop, hat and walk: a tourist in any hat still has to look like a tourist.

**To watch when playing:** whether archetypes still read from the game camera now that, say, a Regular can turn up without the cap. If not, lower that archetype's bare-headed share in `CharacterArt.CustomerLooks`.

---

## 2026-11-10 — The opening, fourth pass: hair that reads as hair, hats in the crowd, the chef already in the lift

**User's notes after the third pass:**
- Hair looks like a silicone swimming cap, on players too; customers should wear some of the modelled hats, so people can be told apart.
- Only six windows a floor look alive in the tower.
- The chef walking in to take the lift is hard to notice: have them in the lift already, and start the climb as soon as the option is chosen.
- The buildings round the tower feel like placeholders.
- A review popup in the first shot looks buggy.
- The delivery riders and their scooters are a good detail and should show more, also once the menu is up.
- Too long a gap between the end of the shot and the menu.
- Some people on the floors have their feet under the floor.
- Some lights are solid circles with no glow.
- The font of "mise en flex" on the restaurant doesn't feel right, and the branding looks empty.

**What we did (our calls within those notes):**
- **Hair (the whole game, not only the opening):** the hair meshes in `SM_Characters` are rebuilt. The old hair was a thin, even shell that followed the head, which is exactly what a swimming cap is. Now every style is a thick mass with locks:
  - short hair has a side parting, a fringe swept across the brow in three pointed locks, locks back over the crown and down the back;
  - whatever shows under a hat ends in uneven locks down the nape and a short lock in front of each ear;
  - the afro is a cloud of curls that stays clear of the face (it used to reach the eyes);
  - the retiree's fringe gains a puff over each ear;
  - **new: long hair** (`HairKind.Bob`, the `HairLengths` mesh), locks to the jaw round the back and sides that hang below any hat.
  The hairline's crisp lip from earlier today stays. Everything keeps within the reach limit (`CharacterProportionTests`, which now checks the new mesh too).
- **Hats:** `CharacterArt.HatKind` gains the wardrobe's hats (cowboy, straw, captain, headphones, plumed, party, propeller), so anyone can wear them, not only players. In the opening about four in ten street people wear a hat; the tower's staff only the odd cap or headphones.
- **Customers:** the in-game customers got the hats and the long hair in a step of their own, the same day (the entry above).
- **The chef and the lift:** the chef stands in the cab from the start. Choosing a campaign starts the climb at once; the walk-in stage is gone. The menu now fades in 0.25 s after the camera stops (it was 2 s).
- **The tower's floors:** an open-plan wing east of the modelled rooms on every floor, as far as the climbing camera sees; the painted windows are mostly lit (72%, from 45%) with a figure moving in most lit rooms. Floors are slabs 0.3 m thick and a stone apron closes the gap under the lowest one, which is what showed feet below the floor.
- **The neighbours:** windows down the full height of the tall buildings round the tower (one quad a face, drawn by `SH_InteriorCard` in a new facade mode), and framed windows on the avenue-facing side of the two corner buildings.
- **The review popup:** it was drawn mirrored, because a character's right arm is a mirrored copy of the left and the card hung from the phone. Cards now follow the phone from the street's own space. Phones the menu would cover send up hearts instead of cards.
- **Riders:** three scooters and three riders moved to the kerb between the two doors, right of the menu, one of them walking a bag back and forth.
- **Lights:** a halo (`SH_Halo`) on string bulbs, street lamps, pendants and stall bulbs; wires on the string lights.
- **Branding:** the fascia carries the logo's own lockup ("Mise en" in Lora italic, gold; "FLEX" in Fredoka, cream) between two fork-and-spoon roundels, a "RESTAURANT & BAR" strap line, a row of bulbs, and a burgundy and cream awning. The gooseneck lamps are gone: seen from below they read as yellow triangles.

**Reason:** the user's notes, one by one. The hair was fixed in the model rather than per scene because the same meshes dress players, customers and staff.

---

## 2026-11-10 — The office scene is built, not kept in git

**User's call:** `Office_Greybox.unity` is no longer committed.
- **Why it's big:** the office builder places about 23,000 objects for the tower and city (19,000 of them meshes), which makes a 55 MB scene. GitHub warns over 50 MB and refuses files over 100 MB.
- **Why not Git LFS:** every rebuild would store a new copy of about 50 MB, which uses up the free LFS quota (1 GB) within about 20 rebuilds.

**How it works:**
- **In git:** the scene and its `.meta` are in `.gitignore`.
- **In the Editor:** when the scene is missing (a fresh clone or a new worktree), `GeneratedScenes` builds it from `OfficeBuilder` the first time the Editor opens. That takes a minute or two.
- **In batch runs:** they build it explicitly with `-executeMethod Game.Editor.OfficeBuilder.Build` (Tools/Game/Build Office), as Main build already does before each test run.
- **Build settings:** `BootOrder` registers the scene by path, so a rebuilt scene slots straight back in. Its GUID changes on a fresh build, which only touches the build settings asset.

**Still worth doing later:** 19,000 separate meshes is also a lot for the 60 fps target with four players. Combining the city and tower's static pieces into fewer meshes would help performance as well as size. That's work for the 3D environment side, to raise with the user.

---

## 2026-11-10 — The opening, third pass: phones, a readable restaurant, busier floors; quit goes to the menu

**User's notes after the second pass:**
- The screen, the neon and the five stars on the tower's entrance don't sit with the scene: remove them or use something better integrated.
- Too many stalls in the first shot: mix stalls with actual restaurants.
- Every floor of the tower should be busier, with activity and movement.
- The hearts and pops read now, but make the scene too busy.
- Special emphasis on customers using their phones: model a proper phone, so it's clear what they hold and where the flash comes from.
- It should be clearer what goes on inside the ground-floor restaurant.
- "Quit the game" from the kitchen or the office should go to the main menu, not stop the scene.

**What we did (our calls within those notes):**
- **Sign:** a painted fascia with the name in the logo's lettering replaces the neon, the stars and the feed screen (`FeedScreen` is deleted).
- **Ground floor:** split into the tower's lobby (west) and the restaurant (east, to the corner). The dining room is on a 1.2 m platform so its tables show over the queue. People now sit (the figure's legs turned forward, its walk removed).
- **Phones:** a larger modelled phone, held clear of the face because the characters' arms are short: selfies on a stick, filming in both hands, scrolling at the chest. Flashes are a flare at the phone's LED.
- **Pops:** one phone in four, each rarely.
- **Stalls:** five in all (there were eleven); the corner buildings by the avenue's mouth are restaurants with terraces.
- **Floors:** seated workers at most desks, two walkers per floor, cooks crossing kitchens, flickering screens and flames.
- **Camera:** the stop moved 2.4 m east, so the restaurant sits beside the menu; the lift is left of centre on the climb.
- **Quit:** the pause menu's entry is now "Quit to the main menu" and loads the office's opening at the menu. Renamed because it no longer quits the game; the main menu's Quit does.

**Reason:** the opening should read as people living on their phones around a restaurant, without stuck-on signage, and the game should never dump players out of a session without passing the menu.

---

## 2026-11-10 — The opening, second pass: a still camera, a night market, the empire's floors

**User's notes after playing the first version:**
- The street's detail and realism weren't there yet: a car crossing the lens, the taxi's pattern, a scooter rider running while riding, the camera moving needlessly.
- The street didn't hustle enough: use the market stalls, other restaurants on the sides, people taking selfies.
- The social icons barely stood out.
- The menu should keep the save slots on a second level, like Settings, to leave the scene in view.
- The floors' detail was underwhelming: every object should be as crisp as the kitchen's appliances.
- The four player boxes should only appear once the office lands.
- Both the approach and the lift ride should be skippable.
- The vendor stalls may be improved in the same pass.

**User's choices among options:**
- **Menu:** New campaign goes straight in; Continue opens the campaigns page.
- **Social:** all four ideas: heart streams, like and rating pills, review bubbles, a giant feed screen on the tower.
- **Floors:** the boss's empire (test kitchen, photo studio, social-media room, call centre, accountants, canteen, uniform room).
- **Camera:** one straight push-in, completely still under the menu, no pan after the chef, then straight up. No vehicle near the lens; selfie-takers in the foreground instead.

**Our calls:**
- The tower's street is closed to traffic for a night market. That removes every moving vehicle from the shot and gives the stalls a place.
- The avenue is cut back to its mouth, because the camera never looks down it now.
- Furniture is a modelled kit shared by the opening and the game (`Kit.cs`), and the game's vendor stalls use its stall. The stall keeps the station's size and behaviour: a 1.8 m counter, a crate for each thing it sells (slabs for the butcher), and the closed stall under a cloth.
- The tower's rooms and lift switch off with the street after landing.

**Reason:** the opening has to sell the game's subject at a glance and hold up at the kitchen's level of finish.

---

## 2026-11-10 — The opening starts at the tower's door

**User's notes:**
- No long run up the road. The camera starts at street level near the tower and walks in to the main entrance, where the menu comes up.
- The ground floor is the boss's restaurant, with a queue, selfies and flashes.
- Stress social media and reviews.
- On a new campaign, the chef walks in and takes the lift. The lift is inside the building, where the boss's floor's elevator is.
- Each floor is seen through glass, as real floors.
- At the top, the view goes from outside to inside.

**User's choices among options:**
- Real rooms only where the camera climbs, with painted rooms (interior mapping) everywhere else. This keeps the 60 fps budget.
- Over the cornice onto the wide shot, with no cut.
- Hearts, stars and comments popping over phones, not just flashes.
- The chef is player 1's, in their tailor look; with no profile, the default chef.

**Our call:** a 2 s beat on the entrance before the menu, because the menu covers most of the frame. A brings the menu at once. The near lane of the tower's street has no traffic, because it runs under the camera's stop.

**Reason:** the opening sells the game's subject (restaurants, reviews, social media) in its first seconds, and the chef's walk to the lift makes the arrival upstairs one continuous story.
## 2026-11-10 — A pause menu with the realism settings; tap Menu pauses, hold Menu readies up (Phase 5, milestone 6)

**User's calls:**
- **Tap Menu (≡) opens the pause menu, everywhere players are:** the office, Prep, service and Review. **Holding Menu (0.6 s) readies up** in Plan, Prep and Review, and starts the next day from forkcast. A bar fills in the player's colour while they hold. This replaces "press Menu to ready up" (2026-11-03). In service and the office, where a hold means nothing, the menu opens on the press.
- **Pausing freezes the game for everyone, always.** Only the player who opened it drives it, shown in their colour with their P-number; everyone else's controls stand still, and nobody can join until it closes. B, or Menu again, resumes.
- **Presets Relaxed / Standard / Chef, with the five dials under Advanced** (game design §12). Standard is the tuning as designed, so play doesn't change unless you choose:

  | Dial | Relaxed | Standard | Chef |
  | --- | --- | --- | --- |
  | Cooking precision | 20% | 50% | 80% |
  | Spoilage speed | ×0.5 | ×1 | ×1.5 |
  | Customer patience | ×1.5 | ×1 | ×0.75 |
  | Minigame complexity | 25% | 50% | 80% |
  | Hygiene (dirt left) | ×0.5 | ×1 | ×1.5 |

  Changing any dial shows the preset as **Custom**; turning it back shows the preset again (the preset isn't stored, it's read back from the dials).
- **In the menu now:** Resume, Settings (preset, language, Advanced), Quit to the office (from the kitchen) and Quit the game. Mid-day, both quits say that today isn't saved and the day starts again from this morning.
- **Controller rebinding waits** (`ideas.md`). `CLAUDE.md` still says controls are rebindable in settings.

**How (proposed while building):**
- **Minigame complexity** bends the sink's balance game and the cleaning and extinguisher trace through one "ease" factor, 2^(0.5 − complexity): at 50% nothing changes; Relaxed is about 1.19× as forgiving, Chef about 0.81×. The sink's centre and OK ring grow by it and its run-away force shrinks by it (so a player doing nothing still never cleans a plate), and the pushes per plate go 1 (Relaxed), 2 (Standard), 3 (Chef). The trace's band round the path and its fastest loops a second grow by it. Strengths are on the `RealismSettings` asset. It applies to the next plate or patch started.
- **Hygiene multiplies how much dirt each event leaves** (`MessMaker`), not how often: a scuff still comes every 6 guests, it just takes more or fewer wipes. The design doc's §12 says hygiene is about cross-contamination and inspections; that part waits for Phase 7, and the doc is out of date there.
- **The dials change live:** cooking precision, spoilage, patience and hygiene apply at once; complexity from the next game started. Saved at once to `settings.json` with the language, shared by every slot; older files get the new dials' defaults.
- **The office's main menu shows the same Settings rows** (`SettingsModel`), so the old settings page is reused rather than removed. It's only reachable during the opening.
- **The pause and the lost-controller pause share one switch** (`DayLoop`: time scale 0 and `KitchenServices.Paused`). While a lost controller holds the service, Menu still means "carry on without them", not pause. If the player who opened the menu loses their controller, the menu closes and the lost-controller card takes over. In the office, which has no day loop, the menu stops the time scale itself.
- **Sound pauses with the menu** (`AudioListener.pause`). The lost-controller pause still lets the sizzle play on (2026-11-09).
- **Quit to the office** carries the players up (`PlayerRoster`) and skips the opening: the camera starts on the floor and everyone is in. The kitchen loads the slot's last save next time.
- **Fixed while capturing the menu:** the `LanguageTables` asset that links `es.csv` to `HudStyle` had never been committed, so in any fresh checkout Spanish didn't exist (the Language row offered only English and the test pseudo-language). It's committed now; `es.csv` is unchanged by it. Running Tools/Game/Update Language Tables also marks about 80 keys `unused` (some, like "Reviews {count}", may still be in use) and adds about 360 untranslated review-voice keys: that rewrite was left out and needs a look by whoever owns the translations.
- **The veil behind the menu is 85% black.** The project is in Linear colour space, where UI blends in linear light, so 60% kept about two thirds of the apparent brightness and barely read as dimmed.
- **Tools/Game/Capture Pause Menu** saves its pages at 1920×1080 to `Screenshots/PauseMenu/`.

**Reason:** the user's answers to milestone 6's open questions. Hold-to-ready also stops accidental readies, which a single press allowed.

---

## 2026-11-10 — Shop cards per player; forkcast becomes four tabs with the cookbook and staff

**User's calls:**
- **The shop gets the tailor's card treatment:** each player who opens it gets their own card, so up to four can shop at once. It's for furniture, appliances and (later) decoration.
- **The cookbook belongs in the forkcast dashboard at Review,** not in management mode. Only Player 1 uses it. This replaces "the cookbook is a second page of the Y panel" from the cookbook entry below.
- **forkcast goes from four tabs to four different ones:**
  - Overview and Reviews merge into **Overview**, which puts the boss's debt first;
  - Menu and Books merge into **Menu & Books**;
  - **Cookbook** and **Staff** are new.
- **Hiring stays on the Plan card for now,** so Staff is read-only.

**Done (shop, `ManagementCardsHud`):**
- **A 360 × 500 card per player,** in their colour, docked in a row from the top-left corner in player-slot order. It starts 96 px down to clear the clock and money, and cards slide along as others open and close. Four take about 78% of the width.
- **The pages sit between LB and RB,** with a dot per page, as the dressing card's sections do: Furniture, Appliances, Kitchenware, and Decoration once there is some (a page with nothing on it is skipped). `FurnitureCatalog.Entry` has a `Section`, which the builder sets; kitchenware is always on its own page.
- **The list shows each piece and its price,** red when it's out of reach. Under it, a line says how the chosen piece arrives: carried and paid for where it's put down, or paid now and put on a free counter.
- **Every press answers:** a new row pops, a purchase flashes the border white, and a refusal shakes the row with the reason in red.
- **The cursor's text label steps aside** while that player's card is open.

**Done (forkcast, `ReviewDashboard`):**
- **Overview:**
  - The boss card is the headline: "You owe him $76.40" in big orange letters when the money is below zero, otherwise the balance.
  - Under it: the next fee and its days as segments, and "Short by…" when the balance won't cover the fee.
  - Served, walked out and turned away sit beside it as small tiles.
  - Below come the review filters, then every review, today's first. The stick browses (up from the top review reaches the filters) and Y replies, as on the old Reviews tab.
  - The "Guests mention" chips are gone: the filters and the Menu & Books tab cover the same ground.
- **Menu & Books:** today's money in and out, beside tonight's menu. Each dish shows its price (and how far it is from the suggestion), how many ordered it, how many walked out and its stars. Below: where the money went and the trend, as before.
  - The old per-doneness order table and the "…are costing you" alert are gone. Walk-outs now tint the dish's row.
- **Cookbook:**
  - A list of recipes on the left: on the menu at a price, or what unlocking costs (red when out of reach), with dots for how demanding each is.
  - The chosen recipe on the right: what it needs, its ingredients with pictures, and the cost of a serving.
    - A dish on the menu shows its price between ‹ ›, the suggestion, and what the prices mean: "Guests: about 24% fewer" (the whole menu's demand) and "Stars: 0.5 fewer for the same dish".
    - A dish not on the menu shows its unlock price and the money in hand.
  - Only Player 1 (the lowest player slot present) can use it, and the hints say whose it is. The stick browses and, sideways, steps a price by 1 (repeating while held). A unlocks; X goes back to the suggested price.
  - An unlock saves the campaign at once. A price change saves 0.8 s after the last step, or on leaving the tab or Review. Both count from the next service.
- **Staff:** a card per member with their role, how they describe themselves, wage, skill and speed bars, mood and today's work, plus the wages per day. With nobody hired, it points to the Plan card.

**Review is no longer read-only:** replies and now the cookbook change things during Review, and each saves at once, so quitting can't undo them. This revises the 2026-11-06 "Review is read-only" decision, and `game-design.md` §4 is now out of date on that point.

**Not checked in play:** the card and dashboard sizes, the price stick's repeat timing, and whether Player-1-only feels right with four players.

---

## 2026-11-10 — The cookbook and menu pricing (Phase 5, milestone 4)

**User's calls:**
- **Each campaign unlocks its own recipes.** They're bought at that campaign's restaurant, so a new campaign starts again from the starter dish. Everything any campaign has unlocked is also kept in one shared cookbook, which the Free kitchen sandbox will offer once it's built.
  - This replaces "recipes carried over from an earlier campaign work at any restaurant" (2026-11-06; `game-design.md` §11 and §15.5, and `CLAUDE.md`'s "the cookbook survives it"). Those texts are now out of date, and they're the user's to update.
- **Four starter recipes** from what can already be cooked:
  - Steak: free, the boss's dish, on every new menu.
  - Steak with onion rings and steak with sautéed onions: paid.
  - Sautéed onions on their own: a cheap filler, paid.
- **Price moves both how many come and how they rate** (r = price ÷ suggested price):
  - Arrivals scale by r^−1.5, kept between 0.15× and 1.5×. 20% over the suggestion brings about 24% fewer guests, and 20% under about 40% more.
  - Each guest's stars shift by −2.5 × (r − 1), clamped to ±1 star. A perfect steak at 20% over the odds earns 4.5 stars.
  - Within the menu, a dish is ordered in proportion to r^−1, so a dear dish is picked less.
  - Every number is a tuning value in `CookbookConfig`.
- **The cookbook is a tab of the forkcast dashboard at Review, used by Player 1** (moved there the same day; see the entry above). Up/down browses, A unlocks, and left/right sets an unlocked dish's price in steps of 1 (it repeats while held).

**How it works (proposed while building; flagged for review):**
- **Unlocks and prices are saved in the campaign** (`CampaignSave.Recipes`, `Prices`, both with defaults, no version bump). An older file loads with just the starter dish.
- **The shared cookbook is `cookbook.json`** beside `profile.json` and the settings, outside the slots. It's written as soon as a campaign unlocks something new to it.
- **The menu is set as the doors open** (`ServiceSimulation.SetMenu`), so whatever was unlocked or priced at the last Review counts from the next service.
- **A recipe's side keeps its share of the price** (for example €5 of the steak with onions' €27). A plate missing the side isn't paid that share, as before.
- **X resets an unlocked dish to its suggested price.**
- **Prices run from 1 to 5× the suggestion.** An unlock is paid like a shop purchase and can't go into debt.
- **Onion rings take fewer cuts than diced onion** (2 against 5), so steak with rings is the cheaper unlock (€40, suggested €25) and steak with sautéed onions the dearer one (€60, suggested €27). This swaps what was first proposed: harder recipes pay more.
- **The forkcast Menu tab** shows "On the menu" with each dish at tonight's price, and the suggestion beside any price that's moved. Today's orders follow. The listing's price band follows the dearest dish.
- **Orders are still tallied as "steak, medium"**, so steak with a side counts with plain steak in the Menu tab's table.

**Reason:** The menu becomes the players' own choice. Pricing is learned from how many guests come and what they say, not from a refusal.
## 2026-11-10 — Street vendors: stalls outside the front door, stock straight into storage, starter stock instead of the delivery

**User's calls (Phase 5, milestone 3):**
- **The vendor street is outside the restaurant, in the kitchen scene,** following the user's mockup: a brick pavement and a road with passing cars along the front, a plaza with stalls to the left, a parking lot and one trash container to the right, houses behind as backdrop. The restaurant keeps running while chefs shop, so a trip during service costs real time. (Not the office scene's city: that would mean loading scenes mid-service.)
- **Two stalls to start: a butcher (steak) and a greengrocer (onion, tomato),** each item in a cheap and a premium grade. The mockup's third canopy stands closed, for a future cuisine.
- **Buying works like storage:** hold RT at a stall to open its menu, and a face button buys up to 6 portions (a full storage slot). It's paid at once to the day's ingredients line, even into debt.
- **Bought stock goes straight into storage, not into a chef's hands** (the user's call, replacing "crates carried back by hand"). Food that keeps cold (`IngredientDefinition.KeepCold`; steak) goes to a fridge first and onto a shelf when the fridges are full. Everything else goes to a shelf first and into a fridge when the shelves are full. Only what fits is bought and paid for. The menu shows how many fit and their price, greyed out at none; with no room at all the stall shakes. The walk to the stall is still the time cost.
- **Every purchase is felt** (the user's call):
  - the storage it went to gives a gentle double hop, distinct from the sideways "can't" shake, with a set-down clack;
  - a fridge also pops its lid a moment with a breath of cold mist;
  - the till rings at the stall;
  - the money at the top turns red and shakes, with the amount in red beside it.
  The money label now shows in Prep too, as the balance; in service it still shows what the service has made.
- **The free morning delivery goes.** A new campaign's first morning finds the storeroom stocked once, with what the delivery used to bring; everything after that is bought.

**Details (proposed while building; flagged for review):**
- **Premium is better through freshness alone.** Cheap stock starts at 70% freshness, premium at 100%, and costs 1.6× the ingredient's price. Both age alike, so cheap stock spoils sooner. Note that freshness weighs only 0.1 in the quality score today, so in play the score difference is small. If premium should matter more, raise the weight or lower cheap freshness (`MarketConfig`, `DishScorer` settings).
- **The starter stock is free** (it was charged at the ingredients' prices as the morning delivery).
- **The outer wall opens where the street door stands** (`StreetDoorway`), following the door when it's moved. The door panel is shown propped open. Pictures hung where the doorway is come down until the door moves.
- **Nobody reaches through an outer wall:** a chef outside can't work a table inside against the wall, and the other way round.
- **While anyone's outside, the camera may show the street** instead of stopping at the building's walls.
- **Customers still appear at the door inside;** walking in from the pavement is left for later.

- **The money total also shows red whenever the balance is below zero** (in Prep), a standing cue for debt.

**Reason:** Shopping becomes a planning and budget choice (`plans/phase-5.md`). Going out costs time, and grades trade money against freshness. Stock going straight into storage keeps the trip about time and choice rather than carrying chores, and the feedback shows each purchase where it went and what it cost.
## 2026-11-11 — A fire sign floats over a burning hob

**User's call:** the pan fire wasn't noticeable or urgent enough; add a floating signifier like the bin's full sign, but for fire.

- **The sign:** a red disc with a dark rim and an orange flame with a yellow heart, drawn in code like the bin's 🚫 (`HobView`, `TEX_HobFireSign`; swap for drawn art in Phase 9). 0.4 m across, 1.95 m over the hob, clear of the flames, flat to the camera, so it reads at any of the 8 rotations. (0.62 m at first: the user found it too big.)
- **The fire spreads over the appliance** (the user's follow-up: it was too small, vertical and focused): 18 tongues scattered over the whole hob top (0.36 m from its middle), tallest over the pan, 55% shorter at the edge and leaning out 28° there, so it spills over the hob's sides. Fields renamed `_blazeTongues` and `_blazeSize` so the old values baked into the hob prefab don't override them.
- **Urgent, not calm:** it pops in with an overshoot as the pan catches, then swells on a quick beat (2.5 a second, 14% bigger) and rocks a little until the fire is out, then pops away. The bin's sign only bobs.
- Only while burning: before that, the smoke and the alarm are the warning.

**Reason:** the user's note after seeing the fire captures.

---

## 2026-11-10 — Phase 5 cleaning and fire: trace a shape, a medium mess, dirt in reviews, no ruined hobs

**User's calls (milestone 5):**
- **The stick game is tracing a shape.** A path lies on the dirt and the chef follows it with the stick, pulling a marker round it; each loop wipes a layer. The cloth draws small circles, the mop a wide figure-eight, the extinguisher side-to-side sweeps across the flames. It's about moving, so it plays differently from the sink, which is about holding still.
- **A medium mess.** Each diner leaves a small smudge on their table, a plate dropped by a waiter or broken at the sink leaves a big floor spill, a pan burnt on the heat leaves a scorch on the counter next to its hob, every few guests scuff the dining floor, and a fire put out leaves foam. A tidy service leaves about a minute of cleaning for one chef; a chaotic one, two or three. All of it is tunable in `TuningConfig`.
- **Dirt counts in reviews only, for now.** A guest seated at a dirty table or next to floor dirt scores a little lower and may mention it. Health inspections come in Phase 7.
- **Appliances have no lifespan for now.** A fire never ruins its hob: it burns until it's put out. This replaces "left too long, the hob is ruined" (2026-11-07).

**Reason:** the user's answers to the milestone's open questions.

**How it's built (proposed while building, tunable in `TuningConfig` under Dirt, Cleaning game and Gas fires):**
- **Hold X with empty hands.** X does the station's own thing first (flip, ring the bell); with nothing to do there it cleans: a burning hob first, then dirt on what the chef faces, then the floor a step ahead. Letting go of X, or B, stops, and what was wiped stays wiped. While X is held, a finished patch moves straight on to the next one in reach. No card while cleaning, as at the sink; otherwise the prompt shows "X wipe" or "X mop".
- **The trace:** the stick's position is the point, so circling the stick draws the cloth's circle. The marker only goes forward, and only new path counts, so wiggling in one place does nothing; a stick slammed about (faster than 15 units a second) doesn't count; under 0.15 the stick reads as centred, which only reaches the path where it runs through the middle (the mop's crossing). A first rule that ignored the stick under 0.35 froze the marker just before the eight's crossing, found by the play test. Tolerance 0.4 of the stick's reach. Fastest loops a second: cloth 1.1, mop 0.7, extinguisher 0.9.
- **Amounts in wipes (one loop each):** a diner 1, a broken plate 3, a burnt pan 2, a scuff every 6 guests 1, foam 2; up to 4 in one spot. So a table smudge is about a second, a spill about four seconds with the mop.
- **In reviews:** a full wipe on the guest's own table takes 0.1 off the dish score (half a star); each wipe of floor dirt within 1.5 m takes 0.04; at most 0.2. From 0.05 the review may be about it ("Dirty table" on the dashboard), after anything wrong with the food and before a slow wait.
- **Fire:** 4 real seconds on a lit flame after the food has burnt on the side it's lying on (flipping the pale side down, lifting the food out or turning the gas off in time stops it). The food is lost. While it burns the hob takes no pan, no heat change and no flip, the gas roars at full and tall flames stand out of the pan, and the alarm keeps going. Turning the knob off doesn't put it out. Three sweeps of the extinguisher do, which also turns the gas off. A fire still burning at closing goes out on its own and leaves its foam for the morning.
- **Shown on the dirt:** flat cartoony patches that shrink as they're wiped, and a bead track in the shape lit in the chef's colour up to the marker, with chevrons for the way round. The mop head, cloth or nozzle rides the marker. The track turns with the camera, never the furniture, and is drawn over the chef (`Game/ToonOnTop`). Placeholder look; `CleaningView`, added by the kitchen builder or **Tools/Game/Apply Cleaning View**.

---

## 2026-11-10 — Hair and snug hats follow the head and end in a crisp lip

**Decision:** The short crop, the hair at the back, the cap's crown and the bandana are now built by `add_head_shell` in `blender/build_pilot_models.py` instead of as loose ellipsoids.
- Each shell follows the head sphere at a set offset, fuller on the crown for the crop.
- Each covers the crown down to an edge set over the brow, the ears and the nape.
- Each ends in a lip tucked into the head.

**Reason:** The user saw ragged hat and hair edges on the faces (most visibly the cap on a dark-skinned chef). Two near-equal curves crossing at a shallow angle meet along a line that wanders over the facets. A shell that sits on the head and ends in its own lip gives one clean edge.

---

## 2026-11-10 — Characters take no cast shadow on themselves; the headdress sweeps back

**Decision:**
- People (chefs, customers, staff, the office's people) still cast shadows on the floor, but receive none from the sun on their own bodies.
- The toon shader has a new per-material `_ReceiveShadows` (1 for the set, 0 on every character material, set by `CharacterArt.Part`).
- The feather headdress's feathers now stand on a cone tipped 55° back: the front ones rise over the brow, the side ones fan out and back, and the back ones sweep down behind the head, as in the user's war bonnet reference.

**Reason:**
- The user found the chef models "bad/buggy across the board". At the sun's shadow-map resolution, the small round bodies shadowed themselves into jagged acne teeth on jackets, aprons, trousers and faces. Captures with shadows switched off showed the teeth were all shadow (not SSAO, which was ruled out the same way).
- Cartoon characters usually don't take cast shadows. The lit/mid cel split across heads and torsos is the toon ramp itself and stays.

---

## 2026-11-10 — The opening runs up an avenue to the tower

**User's notes (2026-11-09):**
- From the user's diagram: the camera comes up an avenue that ends at the tower, stops in the road for the menu, then climbs the tower's face (user's choice of "up the face" over "over and down").
- The movement was rushed, so the flight is now 15 s and the climb 13 s, at an even pace.
- The floors weren't lit the same way. Every floor is now built and lit alike, and the tower is more detailed, in the city's style.
- The whole street should hustle: signs, neon, a food market, portraits and selfies with flashes, crowds, and more refined cars.
- A glass lift on the tower's front, with a chef riding up as the camera climbs.
- The sky matters: stars, the moon, clouds and planes.

**Reason:** the opening's first seconds sell the game's tone; the user wants the city to feel alive and the tower to be the destination all along.

**Technical:** the street level switches off once the camera lands, so it costs nothing during play (the 60 fps budget with four players). How it's built: `docs/opening-flow.md`.

---

## 2026-11-09 — The opening runs in the office scene

**User's call:** the street, the menu and the floor are one scene, the office's, rather than a menu scene handing over to the office (recommended for one unbroken camera move with nothing to load). Leaving for the kitchen stays simple: every joined player at the miniature table, then A.

**Built:**
- The save notice shows on first launch only.
- The fly-through is about 9 s and skippable with A every time; the climb takes 5 s.
- The menu's controller joins as player 1 when the camera lands.
- How it's built: `docs/opening-flow.md`.

**Also built:**
- The office is the boot scene, followed by the kitchen; `MainMenu.unity` is out of builds.
- The miniature table loads the kitchen once everyone is at it and someone presses A.
- **The office's players carry into the kitchen** (user's call): same controllers, same order, no joining again.

---

## 2026-11-09 — A lost controller pauses the service

**User's call** (of three options: pause in service only, always pause, never pause):
- **During service,** a lost controller (battery, Bluetooth) stops the game. A card names who's missing: "Paused: P3, reconnect your controller". It adds "Press Menu to carry on without them" while someone is still playing.
  - The game picks up as soon as the controller is back, on the same chef.
  - If the others carry on without them, the game isn't paused again for that player until they come back and drop out again.
- **In Plan, Prep and Review nothing pauses.** The ready-up leaves the missing player out, and the strip shows "P3 controller lost".

**How:**
- `ControllerPause` (Simulation, tested) holds the rule.
- `DayLoop` feeds it each frame. While it waits, `DayLoop` sets the time scale to 0 and `KitchenServices.Paused`. The time scale stops the simulation, customers, staff and chefs. The flag stops the interactor's instant actions (pick up, use, heat). The Input System pairs the controller again by itself when it comes back.
- `ReadyCheck.IsEveryoneReady` now takes the players who are present, rather than a count.

**Also: slots across scenes.** The office already hands its players to the kitchen (`PlayerRoster`, earlier today). They now rejoin at their own index, not the next free one. So if P2's controller is off, P3 stays P3 and keeps P3's colour and chef, and P2 takes the free slot back by pressing A.

**Not covered:**
- Sound keeps playing while paused, so a pan's sizzle loops on.
- There's no pause in the office.
- A keyboard player can't be "lost".

---

## 2026-11-09 — Up to four players

**User's call:** the game supports up to four local players, in the office and the kitchen. It replaces "1–2 players, 3–4 a stretch goal" (2026-11-02). The office shows four join slots (opening flow).

**Updated:**
- `CLAUDE.md` and `game-design.md`: 1–4 players.
- **The performance target** is now 60 fps on a MacBook with four players.

**Raised to four (2026-11-09).** `CoopSettings._maxPlayers` is 4 in code and in the asset. The test now checks for four, as the requirement changed. What a check of every two-player assumption found:

- **Player colours.** The old set failed colour blindness: under deuteranopia, orange and lime were nearly the same (ΔE about 9). P1 orange and P2 teal stay. **P3 is now yellow and P4 violet** (raspberry and lime before). This was checked by simulating protanopia, deuteranopia and tritanopia (Machado 2009). The closest pair is now orange and yellow, about ΔE 22 for a deuteranope, who tells them apart by lightness. Labels still say P1–P4 wherever colour alone would have to carry it. *Proposed, the user can overrule:* this changes the look of P3's and P4's band, neckerchief and apron.
- **Kitchen spawn points.** There were only two, so P3 and P4 would have started inside P1 and P2. `GreyboxKitchenBuilder` now makes four: P3 and P4 go in the aisle in front of the island. **The saved kitchen scene still has two** until *Tools/Game/Build Greybox Kitchen* is run again. `PlayerJoinController` warns in the Console when a scene has fewer spawn points than players. A joining chef also steps off any furniture that a saved layout put over their spawn point.
- **Prompt cards.** With three or four players at one station, the extra cards marched off to the right of it. They now fan out to either side: right, left, then further right.
- **Tailor cards.** The ladders are about 1.8 m apart, so four 440 px dressing cards over neighbouring chefs overlapped. The cards now share one row and are spread so none overlap (`HudRow`), and the row stays on screen. Four dressing cards fit at 1920 px wide.
- **Fine as they were:**
  - The kitchen camera: four players can't spread wider than two at opposite corners, so neither the framing nor the 18 m "keep the kitchen" height changes.
  - The office camera, for the same reason.
  - The push between bodies: everyone against everyone, which is cheap at this size.
  - The layout editor: one cursor per `PlayerInput`.
  - Ready-up: everyone has to be ready, now tested with four.
  - The ready strips on the Plan card, the Prep strip and the Review dashboard: four entries fit beside the hints.
  - The tailor's saves: `ChefProfile` already kept four slots.
  - The office elevator: its four spawn points fit the car, 0.6 m or more from every wall. The 3 m doorway lets the back row walk out between the front row.
- **Performance.** A code check found nothing that grows badly with players. The layout cursors searched their frame's renderers every frame; they're now tinted once. **The real frame rate hasn't been measured:** that needs four chefs in the Editor with the Profiler.

**To check by playing, with four real controllers:**
- Four Xbox controllers over Bluetooth on macOS: that they all pair and stay connected.
- The kitchen is 6.8 × 3.8 m, and the aisle behind the island is 0.9 m wide, room for one chef. It may jam with four. Is it fun-crowded or frustrating?
- With four players, someone is out of the kitchen more often, so the camera spends more time on the wider shot and the chefs look smaller.
- Four layout cursors close together: their labels can overlap, and only the frame shows whose is whose.

**Fixed afterwards:** players keeping their slot across scenes, and lost controllers (see "A lost controller pauses the service").

**Out of date:** `game-design.md` line 60 still speaks of "close-up panels" with two players, which the 2026-11-02 decision (no close-up panel) had already replaced.

---

## 2026-11-09 — The opening: four join slots, a street to start in, always skippable

**User's calls** (`docs/opening-flow.md` updated):
- **Four join slots** in the office.
- **The menu's player joins automatically as player 1** (left to us): nobody presses A twice.
- **The fly-through starts in a street,** not a park, full of clutter, movement, details and graffiti.
- **It's skippable every time.**

**Where this leaves the docs:**
- **Four players is now a goal, at least for joining the office.** `CLAUDE.md` and `game-design.md` still call 3–4 players a stretch goal.
- **`CoopSettings._maxPlayers` is 2.** Raising it reaches the kitchen too: camera framing, controller support and space at the stations. That needs its own decision.

---

## 2026-11-09 — The opening: from the city's streets up to the boss's floor

**User's outline and calls** (full spec: `docs/opening-flow.md`):
- The first half-minute is one unbroken camera move:
  - logos;
  - a save notice;
  - a fade;
  - a fly-through of the city at night, in a park with people and birds;
  - the camera stops on the boss's tower and the menu appears;
  - after New campaign or Continue, the menu fades and the camera climbs to the office's wide shot;
  - players join by pressing A and move freely.
- **Logos** are an assumption to work out later.
- **The bad-language disclaimer** gives way to a **save notice:** what the save icon means, and not to quit while it shows. A loading moment may follow it.
- **Agreed:** the short fades, the 8–10 s skippable fly-through, the stop framing the tower, the menu's choices leading into the same climb, and the climb handing over to the office.
- **The menu's city needs reworking with this in mind:** the boss's tower and the penthouse as built, a park at street level, one city seen from two heights.

**Proposed, not yet answered:**
- the menu's player joins as player 1 automatically;
- two join slots rather than four;
- the guided first day later starts at step 9.

---

## 2026-11-09 — A control-glyph kit, apart from the game's look

**User's call:** control indications get their own visual kit, easy to see apart from the game scene, drawn by Claude in code for now. The joystick is more stylised (after the user's reference), with an L on top and no ridged edges.

- **The kit** (`ControlGlyphArt`, sprites in `Art/UI/Controls`): flat HUD art like a pad's own glyphs. Dark glossy bodies, a thick near-black outline, a soft shadow; symbols in the HUD display face on top, so one body serves every button. Face buttons keep the official Xbox colour on the letter (art bible). Bodies: round button (A B X Y), bumper pill (LB RB), trigger (LT RT), and the stick from three-quarters in three parts (well, post, cap). Drawn from signed distance shapes at 4×; the Art Director can replace the PNGs without code changes.
- **Never part of the scene:** glyphs are screen-space UI, never lit, shaded or hidden by the world; one that points at something follows it on screen and stays inside the screen.
- **The stick hint** is now `StickHintHud` on the HUD: the cap slides towards the way to push, the post leans after it, push-hold-release, over and over. The 3D thumbstick and its `Game/UI/TutorialIcon` shader are gone.
- **Then flat, in the centre** (the user's call: the three-quarter stick on the rim looked too strong): the cue is a flat, top-down stick from the kit, a light cream ring and a cream cap marked L, 46 px, 90% opaque, sitting in the centre of the wash ring where the soap has to go. The cap slides the way to push and springs back. The three-quarter stick and its plate stay in the kit for button prompts. The hint is placed from the camera's viewport, so it's right at any window size.
- **Sheet:** `ControlGlyphArt.CaptureSheet` renders the kit on a dark and a light half (`Screenshots/control-glyphs.png`).
- **Next:** move the prompt badges (`ButtonBadge`) and the bottom hint bar onto the kit, and add the D-pad, Menu/View, right stick and the press, hold and rotate animations.

---

## 2026-11-09 — The wash game in 3D, a tutorial stick, no card, and 3 centrings a plate

**User's calls, after playing the flat ring:** it looked too flat for the scene; the card ("LS center the soap", "X stop washing") should go, with an animated joystick showing the movement instead, and X or B to leave; the stick should look like a real thumbstick with an L on top, styled as a scene-agnostic tutorial glyph; a plate takes 3 touches of the centre, not 4.

- **3D pieces** (`SinkWashRingView`, meshes from `StationKitMeshes`): 15 chunky toon-shaded segments (5 per centring) that light up in the washer's colour, a raised centre target whose 12 segments turn green while the soap is held, and a soap bar that bobs, leans as it slides and shakes when far off, with bubbles circling it in the centre.
- **Drawn over the chef** with `Game/ToonOnTop` (house toon shading, ignores depth, drawn late in a fixed order per piece). An overlay camera on its own layer was tried first and dropped: the tap still drew over the ring.
- **No card while washing.** X or B stops (B did nothing while busy before; `PlayerInteractor.BackOut`).
- **The stick hint** appears beside the soap, on its far side from the centre, when it's been off target for 0.6 s with the stick not pushing towards the centre (not moving it counts), and tips towards the centre until the player does. A domed base with a collar, a short shaft, a rounded cap with a grippy rim and a dished top marked L (Fredoka). `Game/UI/TutorialIcon` gives it a fixed light and its own outline, so it looks the same in every scene and at every hour.
- **3 centrings a plate** (`Corrections` 2): the plate starts tipped to one side and the two pushes go to the other two, about 120° apart (±20°), so it's still tipped every way. A steady simulated hand takes about 3.7 s a plate.
- **The tap may cover the ring** (the user's call, after it showed in captures with the sink turned 45°): it's the one thing allowed in front, so the ring still reads as sitting in the sink.

---

## 2026-11-09 — The wash ring is drawn over the chef

**User's call (from a mockup):** with the chef's back to the camera, their toque hid most of the ring, centre included (captured with the sink turned −90°). The ring is now drawn over whatever stands in front of it, the washing chef above all, instead of being moved off the water or the chef faded.

- **Scope:** station rings only (`Game/FX/WashRing` ignores depth). This is a deliberate exception to 2026-11-07, which stopped the hob's heat pips drawing over things in front of them; the pips are unchanged. The sink is always in the play area, so in practice only people and the tap come between it and the camera.
- The dark backing under the ring is lighter (20%, was 40%), as over the toque it read as a grey patch.
- **Bigger, after review:** the ring is 0.8 m across its quad (was 0.6 m), about 0.67 m at the rim, so it overhangs the 0.62 m basin a little (the user's call: fine, for visibility). The soap is about twice the size on screen (point radius 0.16 of the quad, was 0.12).
- **Level with the rim:** the ring sits just over the basin's rim (0.915 m) instead of on the water (0.79 m). Seen by the 55° camera from 12 cm lower, it looked shifted towards the camera and off centre in the opening. Drawn over everything, it can sit where it lines up.
- The station-is-the-interface rule (above) changes accordingly: a station's ring stays readable over the chef using it.
- Considered and dropped: centring the ring on the tap's base (the tap covers the centre and half the ring leaves the basin), shifting it back in the basin, standing it up at the tap, fading the chef, and the chef stepping aside.

**Reason:** the game must read from any side of the sink, at any of the layout editor's rotations.

---

## 2026-11-09 — Washing: hold the centre, then correct three pushes

**User's call:** a plate is washed by bringing the soap to the centre and steadying it there; the game then pushes it off in a random direction to be corrected, three times, so every plate gets tilted every way. The ring keeps its size with a soft outline and thicker segments, and the suds cloud in the middle goes.

- **The rule (`WashGame`):** hold the point in the centre (`GoodRadius`, 0.25 of the circle) for **0.3 s**; flicking through doesn't count, and leaving starts the hold again. Each hold scrubs an equal share of the plate. After the first and each following hold, the point is kicked off at **3.5** circle radii a second, **3 times** per plate, so a plate takes 4 holds. The kicks are spaced evenly round the circle from a random start, each within **20°** of its spot, so they always come from three different sides.
- **The drift is gentler** (proposed, since the pushes now bring the challenge): instability 3.2 (was 4.5), nudge 1.0 (was 1.6). Doing nothing still never cleans a plate.
- **Measured with simulated hands, not real ones:** a steady hand with a 0.15 s reaction takes about 5 s a plate (it was under 4 s); slamming the stick about never holds the centre long enough.
- **The look:** the centre is a pale disc with a white ring, and an arc in the good colour fills round it while the soap is held, so "stay here" reads. Segments are thicker (rim width 0.19, was 0.07). The outline is a soft deep blue at 40% strength instead of Ink. The rim's segments light up a quarter at a time.
- **Not yet:** stopping mid-plate and starting again keeps the sink's progress but restarts the pushes, so that plate may finish with fewer than three. Tunable in `TuningConfig` (Washing by hand).

**Reason:** the user wanted washing to feel like steadying a plate that keeps getting knocked, and to know each plate was turned every way.

---

## 2026-11-09 — The station is the interface; washing moves onto the sink's water

**User's call:** minigames are drawn on their appliance, not over the chef, and every mechanic and its interface must read at any of the 8 rotations the layout editor allows, with the chef on any side. The sink goes first. After the first captures: bolder and more arcade-like, the plate tilting with the point, and a cue that the stick moves it.

- **The rule for every station:** what you time or judge lies flat on the appliance, tinted in the player's colour. Floating cards are only for button prompts and short text. What must be read (needle, target, letters, sweep direction) is laid out relative to the camera and the chef, never the station's own axes. Text stays upright on screen. Nothing is drawn over objects in front of it (2026-11-07).
- **Sink:** the balance circle lies on the water (`SinkWashRingView`, shader `Game/FX/WashRing`).
  - The target is a cloud of suds with an Ink outline (it has to read on the white plate under it); a dashed Ink circle marks where washing stops.
  - The point is a glossy soap bar with a thick outline: a pulsing suds halo when it's washing well, a broken, turning red ring when it isn't, so it reads without colour.
  - The rim is chunky and arcade-like: an Ink-outlined track in 16 segments that light up in the washer's colour with the plate's progress (it replaces the bar). The ring pulses gently while washing well, and pops and flashes white when a plate comes clean.
  - **The plate in the basin tips towards the point** (up to 22°, sliding up to 5 cm), levelling out at the centre, so the balancing reads as the plate itself (the user's idea).
  - **How to play it:** while the soap is off target, chevrons flow from it towards the suds, and the washer's prompt card shows a new LS badge, "center the soap", above "stop washing". The stick isn't part of the A / X / triggers grammar, so it keeps a prompt.
  - The mechanic and its tuning are unchanged.
- **Any rotation:** the ring and the tilt follow the camera, so pushing the stick up always moves the point, and dips the plate, up on screen. A circle reads the same at any angle.
- Added by **Tools/Game/Apply Wash Ring** (and by the kitchen builder with the sink's water), which also removes the old circle above the chef. **Tools/Game/Art/Capture Wash Ring** renders it at the sink's angle and turned 45°, with a chef in place.

**Reason:** the old circle floated above the chef's head with nothing tying it to the sink. On the water it reads as part of the kitchen, and the user found it the most natural home for the ring.

---

## 2026-11-09 — HUD type: Nunito and Fredoka, 28 px minimum, an Ink outline

**User's call:** Fredoka and Nunito, as the art bible suggests, over Inter. First step of the cooking and service UI pass (type and readability).

- **Nunito ExtraBold for words** (prompt verbs, tags, tickets, reviews), **Fredoka SemiBold for numbers and button letters** (clock, money, badge glyphs). `HudStyle` has a new `DisplayFont` slot; the body font stays `Font`. The fixed weights are cut from Google Fonts' variable files (`Art/Fonts/FONTS-LICENSE.md`). The Review dashboard keeps Inter and Lora, drawn to its mockups.
- **28 px minimum for text read in play** (art bible): HUD and label text 22/20 → 28, clock, money and phase card 26 → 32, the Prep strip 18 → 28, the chop grade 26 → 30 (a miss is 38) and its speed hint 15 → 28. Badges grow from 34 to 40 px so the letters match the text.
- **A thin Ink outline on the HUD fonts' materials** (`HudStyle` text outline, 0.15), so text that floats over the scene without a card, like the chop grade, reads on any background. Cards are a little more opaque (86% → 92%) so food colours on them don't wash out over the white chef.
- **The flip gauge moves 10 px lower** (−66 → −76) to clear the taller doneness tag above it.
- **"1 star", not "1 stars"** on review cards.
- Applied by **Tools/Game/Apply HUD Type**, which makes the font assets, sets the style, rebuilds the prompt card and chop dial prefabs and raises the sizes stored in the open scene.

**Reason:** the HUD still used Unity's placeholder font below the art bible's minimum size, which made every screen read as greybox and was hard to read from the couch.

---

## 2026-11-09 — Every on-screen text goes through our own translation tables; a language setting

**User's call:** our own CSV tables rather than Unity's Localization package (step 2 of `ideas.md`, "Localising reviews and replies").

- **Keys are the English text** (gettext style): `Tr.Get("Settings")`, `Tr.Format("Start day {day}", ("day", 2))` with named slots, `Tr.Plural(n, "1 day", "{n} days")`, and `Tr.In("heat", "medium")` for an English word with two meanings ("medium" heat is "medio", a "medium" steak is "al punto"). Untranslated text reads in English, so rewording English never breaks anything.
- **One CSV per language** (`Data/Localization/es.csv`: english, translation, note), listed in the `LanguageTables` asset that `HudStyle` loads. Spreadsheets open them as they are.
- **Tools/Game/Update Language Tables** collects every key (what the code passes to `Tr`, serialized HUD texts shown through `Tr.Get`, ingredient, doneness, staff and furniture names, the review voices) and merges them into each table, keeping translations; keys nobody uses any more stay, marked `unused`.
- **Settings has a Language row** (English, Español, and in the Editor and development builds a pseudo-language that accents, brackets and lengthens every translated text, to find hard-coded strings and text that can't stretch). It's saved in the player settings and switches everything at once, old reviews included.
- **Money, dates and names follow the language** (`Locale`, `GuestNames`). Data words are translated where they're shown (`ItemCatalog.DisplayName`, tickets, tags, prompts).
- Not translated: the developer cooking overlay, the sign artwork's baked-in text, and the boss's office (another session's work in progress). The review voices' Spanish lines are step 3.
- Tests check that every text in the code has a Spanish translation and that every translation keeps its `{slots}`.

**Reason:** A small, readable system we control: no package setup, tables a translator can edit in a spreadsheet, and keys that are the English itself, matching how reviews are already stored (templates keyed by their English).

**Spanish needs a native review:** the first table (Spain Spanish, informal "tú") was written for testing the system. Some lines read stiffly in Spanish because English word order is assumed, e.g. "{dishes} are costing you." becomes "Filete, al punto te está costando caro."

---

## 2026-11-09 — The bodyguards stare at chefs near their doors

**The user's notes:**
- The bodyguards should clearly stare at players walking round their room, and lock on while they're in the kitchen or the awards room.
- In the boss's rooms, the waiting room, the entrance or the accountant, they stop looking.
- With two players they split their focus 1:1 from the centre of the screen; with more, by who's nearest.

**Done (`BodyguardWatch`):**
- **Watched rooms:** chefs in the floor's middle row are watched: the test kitchen, the guards' own room and the hall of fame (the awards room). Anyone elsewhere is ignored.
- **Who watches whom:**

  | Chefs watched | Guards' focus |
  |---|---|
  | One | both guards stare at them |
  | Two | split down the middle of the screen: the left guard takes the chef further left |
  | Three or four | each guard watches the chef nearest to him |

- **The stare:** a guard turns quickly (320°/s) and holds still, which is what makes it read as a stare.
- **When nobody's left to watch:** they turn back to the front more slowly and only then resume their idle glances.
- **Build:** each guard has his own turning frame (`OfficeBuilder.Guards.cs`), so the glance (an `Oscillate` on the person) and the stare don't fight over one transform.

**Not checked in play:** the split and the turning speeds; tune `_turnSpeed` and `_relaxSpeed` on the watcher.

---

## 2026-11-09 — A permanent player bar in the office; the dressing card grows out of it

**Decision:** The office has a player bar, always on screen: one card per player slot, docked in slot order from the top-left corner. It replaces the tailor's separate dressing cards, which the user found too big and wanted on the left.
- **Empty slot:** the slot's number with "A Join", on a translucent card (40% opacity). Translucent was the user's call.
- **Joined:** a compact 210 × 108 card in the player's colour, with a live portrait of their chef's head and "P1"–"P4". It shows from the moment they appear in the elevator. Compact portraits redraw six times a second to keep four of them cheap.
- **Dressing at the tailor:** that player's card grows into the 340-wide dressing card (portrait framed by section, LB/RB, item, B Done), and shrinks back when they step off. The cards to its right slide along.
- **Size:** compact, the bar fits inside the office camera's reserved top band (15% of the screen), so the camera is unchanged. Four players dressing at once still takes about 75% of the width.
- **"A Climb up"** stays over the chef at a ladder's foot.

**Reason:** the user wanted the cards to be there permanently, smaller and less disturbing, and suggested they also show who has joined.

**Not yet:** the bar is in the office only. Whether the kitchen gets one too is still to be decided.

---

## 2026-11-09 — Tailor cards docked top right, with a live portrait of the chef

**The user's notes:**
- Show a thumbnail of what's selected, so it can be seen even when the players are far apart.
- Dock the menus top right; four players dressing at once should use about 80% of the screen.

**Done:**
- **Each dressing card has a live portrait:** a small camera in front of the chef renders into the top of the card (512 × 360, in the game's look with its ink and lighting). It eases between framings by section: the head for hats, faces and accessories, the upper body for skin, the whole chef for outfits. The camera runs only while that chef is dressing.
- **Dressing cards dock in a row from the top-right corner,** in player-slot order, Player 1 nearest the corner. Each is 400 × 504 with a 12 px gap, so four use about 88% of the width and under half the height. The "A Climb up" prompts still sit over their chefs, spread so they never overlap (`HudRow`).

**Watch:** four portrait cameras running at once is the costliest case; check the frame rate with four players dressing.

---

## 2026-11-09 — The tailor's menu: LB/RB for sections, and no more sluggishness

**The user's notes:** moving through sections should use RB/LB, and the menu felt sluggish.

**Changed:**
- **LB and RB change section** (hat, face, skin, accessory, outfit), using the `RotateLeft`/`RotateRight` actions the Review dashboard's tabs use (keyboard Z and C). The stick's up and down no longer do anything here.
- **Items:** the stick or the D-pad, left or right. Move has no D-pad binding, so the ladder reads the player's gamepad D-pad directly.
- **Faster:**
  - a press steps at once with only a 40% lean (was 55%);
  - holding repeats after 0.24 s (was 0.38) every 0.1 s, speeding up to every 0.05 s after a second (was 0.16).
- **Every press answers:**
  - the item swells and settles in 0.16 s, and the arrow on its side kicks;
  - a new section's name swells, and its dot lights in the player's colour;
  - the chef does a quick squash and stretch as they try something on (`ChefWardrobe.TryOn`).

  None of it holds the next press back.
- **The card:** LB and RB pills either side of the section name, a dot per section under it, the item between arrows with its place in the list (or "Locked: earn it first"), and B Done.

---

## 2026-11-09 — A feathered headdress on the rail (the user's call)

**User's call:** build the feathered war-bonnet-style headdress from the user's references, after the concern was raised twice: it's sacred regalia in Plains nations, and as a costume it's widely seen as disrespectful. This replaces the earlier proposal to offer only the plumed hat, which stays on the rail too.

**Done:**
- **"Feather headdress"** is an earned item, stylised in the game's look, with no other costume pieces:
  - a beaded browband in the player's colour, with white beads all round and red rosettes over the temples;
  - eleven long white feathers with dark tips, fanned round the back of the band and tipped slightly outwards, each swaying on its own spring (`HatSway`, stiffness varying a little).
- It doesn't hide the hair, and it stays within the reach limit.

**To revisit if needed:** if the game is shown publicly or reviewed for cultural content, this is the first item to look at.

---

## 2026-11-09 — The tailor's room: ladders any player climbs, a tailor who measures, a busier room

**The user's notes:**
- Four small ladders to climb; any player can use any of them.
- Once a player climbs, the tailor approaches and takes random measures of their body while they browse. If another player climbs up, the tailor moves to them; otherwise they stay idle or roam gently towards the middle of the room.
- The room could be busier, with more of the trade's objects.

**Done:**
- **Four step-ladders** replace the colour plinths, along the back wall, each before a three-way mirror. Each is a walnut platform with two steps, a velvet top and a brass rail.
  - Any player at a ladder's foot sees "A Climb up" and hops up onto the top step (an arc, about 0.3 s), then dresses as their own slot's chef. B hops them back down.
  - The card now takes the colour of whoever is on the ladder. The look still saves per player slot.
- **The tailor (`TailorAttendant`)** potters about the rug in the middle of the room, pausing now and then.
  - When a chef climbs up, they walk over to the ladder's front corner on the room's side (clear of where chefs hop up and down) and face the chef.
  - They take one measure after another, chest, waist, an arm, a leg (stooping) or the head, with a yellow tape stretched between their hands. The walk's arm swing hands over to them while they measure (`WalkCycleView.SwingArms`).
  - If another chef climbs up, they go to the newest. The tailor has no collider, so nobody snags on them; dressing doesn't wait for them.
  - They walk on their own frame, so the office's breathing idle still works.
- **A busier room** (`OfficeBuilder.Tailor.cs`):
  - jacket rails;
  - a wall of fabric bolts in every colour and a rack of thread spools;
  - a cutting table with pattern paper, a half-unrolled bolt, shears, a pin cushion, chalk and a tape;
  - a sewing machine on its table with a stool;
  - an ironing board and iron;
  - two dress forms;
  - a hat stand by the door wearing hats from the rail;
  - a round rug in the middle.

  The middle and the way from the door stay clear.

**Not checked in play:** the hop, the tailor's walk and reach, and the tape; only the room itself has been rendered.

---

## 2026-11-09 — Hats that read from above, and headphones that work

**The user's notes:** the headphones felt broken, and some hats were very hard to tell apart.

**Changed:**
- **Headphones:** the band hugged the skull, so most hair swallowed it and only the ear cups showed. Now they're big DJ headphones: a fat padded band arching clear of every hair style (afro included), sliders into big cups in the player's colour, and dark cushions. The notes are bigger and more frequent.
- **A signature shape for each lookalike:**
  - the bandana has white polka dots on the player's colour (a print on a tinted part, `PlayerPrint`), so it isn't a peakless cap;
  - the cap has a bigger peak and a top button;
  - the captain's hat has a bigger glossy peak, a thick gold braid and a large anchor badge that leans back with the crown;
  - the cowboy hat has a creased, pinched crown and a brim curling up hard at the sides.
- **Polka dots** no longer bunch into a star at the top of a dome: the way round scales with the distance from the axis (in `Game/Toon`).
- `CharacterCapture` renders every hat on the same chef from the game camera, to judge them side by side.

---

## 2026-11-09 — Hats that move, a cowboy hat, and a plumed hat instead of a headdress

**The user's notes:**
- Ideas for a Native American feathered headdress and a cowboy hat.
- Hat parts should be animated as much as possible and answer the chef's movement: propellers turn, feathers move gently in the wind, and headphones send little music notes floating up from the ears.

**Decision:**
- **No Native American headdress (proposed, the user can overrule).** The war bonnet is sacred regalia earned within those nations, and wearing one as a costume is widely seen as disrespectful. Instead, a **plumed hat**: a soft wide brim pinned up on one side, a band in the player's colour, and a big white feather that sways.
- **A cowboy hat:** a dented crown and a brim curling up at the sides, brown, with the band in the player's colour. It's distinct from the boss's white one.
- **Movement:** `HatSway` hangs a loose part on a damped spring. It trails behind as the chef walks, swings on past a stop or a turn, settles, and drifts in a light breeze. The plume, the party hat (cone and pompom on its elastic) and the bandana's two new tails all sway, each with its own stiffness. The propeller already spins faster with the walk (`PropellerSpin`).
- **Headphones** send music notes (`FxMusicNote` particles, in the player's colour) up from each ear. They drift outwards, wobble, and shrink away at the top rather than fading, which suits the opaque toon look. They're left behind as the chef walks.
- Both hats are earned items within the reach limit, with Spanish names.

---

## 2026-11-09 — Four more hats for the tailor

**The user's references:** a captain's cap, a propeller cap, headphones and a conical straw hat.

**Decision:** all four are crazy items (earned before they're worn, like the horns, crown and party hat). Each has its part in the player's colour:

| Hat | What it is | Player colour |
|---|---|---|
| Captain's hat | white crown flaring flat, glossy black peak, gold braid round the band and across the peak, gold anchor badge | the band |
| Propeller cap | four panels (red, blue, yellow, green), a stem with beads; the propeller spins slowly standing still and whirs as the chef walks (`PropellerSpin`, from the walk's speed) | the peak |
| Headphones | dark padded band over the top; doesn't hide the hair (an afro hides the band from above) | the ear cups |
| Straw hat | shallow cone out to the full reach, with ribs and a bound rim so it reads as a cone from above | the chin strap |

All within the 0.38 m reach (`CharacterProportionTests`). Spanish names are in `es.csv`.

---

## 2026-11-09 — The tailor: dress your chef on your plinth, saved per player slot

**The user's calls:**
- Looks are kept per player slot, not in named profiles.
- Crazy items are visible but locked until there's a way to earn them.
- Dressing happens in the room, with a card above the chef, so both players can dress at once.

The plan is in `plans/tailor.md`.

**Done:**
- **The look (Simulation, `Game.Simulation.Characters`):**
  - A `ChefLook` is an item id per module: hat, face (with its hair), skin, accessory and outfit.
  - `Wardrobe` is the rail, in browsing order. The first item of each module is the default chef. Ids are what saves and never change.
  - `TailorSession` is the dressing: up/down picks the module and left/right the item, wrapping round. The chef shows whatever is picked, locked or not. Only open items are worn and saved. Stepping off drops a locked preview.
- **The profile:** `ChefProfile` has a look per slot (four) and the crazy items earned (none yet), with a version number and an upgrade step.
  - Anything unknown or locked in a saved look goes back to the default chef. A file from a newer game is refused and never overwritten.
  - `ChefProfileFile` keeps it in `profile.json` beside the settings, outside any campaign slot, and saves at once on every change, as anything done in the office does.
- **On the chef:** the player prefab carries the whole rail (`CharacterArt.BuildChef`), and `ChefWardrobe` shows a look.
  - Each joining player wears their slot's look in the kitchen and the office, and changes as soon as the tailor saves.
  - A hat that covers the head (toque, cap, bandana, crown, party hat) hides the top of the face's hair; the headband and horns don't.
  - The player colour stays on the neckerchief, the apron and each hat's band, peak, gems or cone.
- **The rail:**

  | Module | Items |
  |---|---|
  | Hats | toque, cap, bandana, headband; locked: devil horns, a crown and a party hat (both new models) |
  | Faces | eight, each a hair style and colour, a nose and maybe a moustache |
  | Skin | eight tones |
  | Accessories | none, glasses, sunglasses, aviators |
  | Outfits | chef whites, line cook, wok cook (tank top), pastry chef; locked: a suit and a tourist (stripes and a camera) |

- **The plinths (`TailorPlinth`):**
  - The colour plinths are now in the players' own colours, slot by slot.
  - The slot's player steps on and presses A. The chef stops (`IsBusy`) and turns to face the camera (`PlayerMovement.FacingOverride`).
  - The stick browses: one step per push, repeating when held. B steps off.
- **The card (`TailorCardHud`):** in the house HUD style, with the player's colour on its edge.
  - On the plinth it shows "A Dress up".
  - While dressing it shows the module with a dot per module, the item between arrows and its place in the list, "Locked: earn it first" for a locked item, and LS Browse and B Done.
  - It stays whole on screen and names only Xbox controls. Spanish translations are in `es.csv`.

**Checked:** `WardrobeTests` covers the defaults, the round trip, repairs, refusing a newer file, browsing, locks and that the player carries every item. Captures show the rail on the real player prefab. The plinths and the card haven't been played.

**Not yet:** how crazy items are earned (Phase 7), named profiles, and a mirror close-up.

---

## 2026-11-09 — Garment styles, and bodyguards with their own shades and big earpieces

**The user's notes:** customers should wear different styles (a jacket, a vest, a tank top). The bodyguards each wearing a different style of sunglasses (in concept v16) was a detail to keep, and their earpieces could be more exaggerated.

**Decision:**
- **Garments** (`CharacterArt.Garment`):

  | Garment | What shows |
  |---|---|
  | Shirt | as before |
  | Open jacket | the shirt in a V at the front, with lapels |
  | Vest | a deeper V, three buttons, and the shirt's sleeves |
  | Tank top | bare shoulders and bare arms |
  | Hoodie | a hood behind the neck, a front pocket, drawstrings |

  The shirt underneath is its own colour (`Look.Under`; white, pale blue, cream or pale pink when rolled). Ties now sit proud of a shirt front.
- **Rolled per archetype** (a garment's torso pieces and both sleeves show together):

  | Archetype | Garments |
  |---|---|
  | Regular | shirt or vest |
  | Office worker | mostly a jacket, sometimes a vest or a shirt |
  | Tourist | shirt or tank top |
  | Retiree | shirt, vest or jacket |
  | Gym-goer | mostly a tank top, sometimes a hoodie or a tee |
  | Date night | stays in a dress |

  The pattern goes on the main garment (a pinstriped jacket, a plaid vest).
- **The office:**
  - the bodyguards wear suit jackets with a red tie and a big coiled earpiece wire from the right ear to the collar; the left one wears wayfarers, the right one gold aviators;
  - the boss and the receptionist wear jackets;
  - the tailor and the accountant wear vests.

---

## 2026-11-09 — Patterned clothes, and the office's people on the players' body

**The user's call:** customers always wore plain colours and should get patterns too. The office's people should follow the new characters.

**Decision:**
- **Cloth patterns in `Game/Toon`:** `_Pattern` picks one of stripes, plaid, polka dots, pinstripes or gingham, in `_PatternColor` at `_PatternScale` repeats per metre.
  - They're drawn in object space, wrapped round the body's vertical axis with a whole number of repeats, so there's no seam and no UVs are needed.
  - The edges are crisp and eased over a pixel, flat like the toon bands.
  - Like the wood grain, they change nothing for materials that leave them off.
- **Patterns are part of the roll** (`Variation.Patterns`, `PatternColours`, `PatternScale`):

  | Archetype | Patterns |
  |---|---|
  | Regular | mostly a flannel plaid |
  | Office worker | plain or pinstripes |
  | Date night | plain or polka dots |
  | Tourist | breton stripes, gingham or plain |
  | Retiree | sometimes gingham |
  | Gym-goer | sometimes sporty stripes |

  The pattern covers the top, the sleeves and a dress. A different pattern also counts as a different person in the no-repeat check.
- **The office's people use `CharacterArt`:**
  - the receptionist, with a bun and glasses;
  - the tailor, grey, with moustache, glasses and a gold tie;
  - the accountant, ginger, with frames and a green visor;
  - the bodyguards, same suit, sunglasses and red tie, one bald and one cropped;
  - the boss, grey, with a moustache, a big nose and the camel suit. His cowboy hat is scaled to the new head (about its centre) and the bolo moved to the new collar.

  The tailor's mannequin wears the kit's barrel jacket. Without the kit, the old chef model stands in as before.

---

## 2026-11-09 — Customers are rolled people, with their archetype's props

**The user's playtest:** two customers came in looking exactly the same. Each archetype should have its own props (a cane for the retiree, a backpack or leather bag for the office worker), with as much variety as fits.

**Decision:**
- **The archetype is the clothes; the person is rolled** (concept v9.2). When a customer appears, `CharacterRoll` picks the following within their archetype (`CharacterArt.Variation`):
  - one of eight skin tones;
  - a hair style the archetype allows, and a natural or grey colour;
  - a shade of their clothes, never a player colour;
  - a nose;
  - glasses or sunglasses, and a moustache, each by chance;
  - one of the archetype's props, or none.
- **No two people in the room are identical:** a new customer re-rolls (up to 24 times) if they'd exactly match someone already there.
- **Every option is built on the body and shown or hidden.** Colours are set by property block. There are no extra prefabs.
- **Props** (new meshes in `SM_Characters`):

  | Archetype | Prop | Chance |
  |---|---|---|
  | Retiree | walking cane, held in the hand | 80% |
  | Office worker | leather satchel across the body, or a backpack | 75% |
  | Date night | handbag | 70% |
  | Gym-goer | water bottle | 80% |
  | Regular | rolled newspaper | 50% |
  | Tourist | backpack (plus the camera) | 50% |

  Held props swing with the arm. Worn props stay within the 0.38 m reach.
- **Gym-goers don't get an afro,** because it would hide the headband, their trademark.

**Cost:** recoloured parts leave the SRP batcher (they use per-renderer properties). With up to about 20 customers that's fine; check it in the Profiler if crowds grow.

---

## 2026-11-09 — A calmer walk, a walk per customer, a smaller toque, and a flip you can see

**The user's second playtest:** the walk was great but swung too much, and the knife showed out of its sheath while walking. Customers' walk and speed could be part of their character. What's carried should show more when a chef is turned away, so the hat could be smaller or the arms longer. The flip was very hard to notice and should be exaggerated substantially.

**Changed:**
- **Calmer walk:** the waddle is 1.5° (was 4°), the bob 2 cm (was 3.5 cm), the leg swing 24° (was 30°) and the arm swing 16–20° (was 22–32°).
- **The knife stays in its sheath:** the knife's anchor now moves with the body, so the waddle no longer tips the sheath away from the knife.
- **A walk per customer** (`Look.Pace`, `Stride`, `LegSwing`, `Waddle`, `Bob`):
  - the office worker strides briskly (115% pace);
  - the gym-goer bounds (125%);
  - the date sways with short steps (95%);
  - the tourist ambles (85%);
  - the retiree takes small, slow steps (70%).
- **Pace is gameplay too:** `WalkingAgent.SpeedFactor` scales the configured walking speed, so a retiree takes longer to reach a seat or the door. Nothing else depends on it.
- **What's carried shows more:** the toque is smaller (the top is 1.74 m; head and toque are 37% of the height). The arms reach 0.48 m to the hand, and things are carried higher and further out (1.1 m up, 0.56 m forward). From 45° on, the plate shows clearly. Facing straight away it's still hidden: a 55° camera can't see past a head held level with the hands.
- **The flip:** the pan lifts 25 cm (was 12) and tips 35° (was 18). The food jumps 40 cm out of the pan or off the grill, turning a full somersault, and lands as the pan settles. A flip lasts 0.6 s (was 0.4). The hand at a grill flicks twice as high. This is presentation only: when the food turns over in the simulation is unchanged.

---

## 2026-11-09 — Characters toward v16 after the first playtest: smaller heads, longer legs that walk, hands on what's carried

**The user's playtest of v9.2:** it was easy to lose track of what you're holding: things floated in front of the chef, and the hat seemed too big. Customers felt too big. Walking didn't show: the legs were too short and didn't move. The 20° storage bend was fine. The references were concept v16 (`Art/Direction/concepts/2026-11-09_character-system_v16_full-cast.png`): head and hat about 38%, slimmer, with visible legs.

**Changed:**
- **Proportions:** head and toque are now 38% of the height, down from 47%. The head is 0.54 m across instead of 0.62 m (head parts are authored once and scaled together, `CHAR_HEAD_SCALE`). The toque is a short, puffy mushroom. The body is slimmer (0.55 m across), and the waist sits at 0.65 m with legs below it. The top of the head is still 1.63 m; the toque reaches 1.78 m.
- **Arms reach what's carried:** the arms are 0.42 m to the hand, and the carry point is at 1.0 m high and 0.5 m out (`CharacterArt.CarryAnchor`), so both hands are on the plate. `CharacterProportionTests.HandsReachWhatsCarried` checks it.
- **Walking:** each leg hangs from its own hip. `WalkCycleView` swings the legs, bobs the upper body once a step and waddles a few degrees. The cycle advances with distance walked (1 m per two steps), so feet don't skate at any speed. A chef's arms (`ChefArmsView`) follow the same gait, swinging against the legs. Customers and staff swing their arms from the gait itself.
- **Customers** are 90% of a chef's size (`CharacterArt.CustomerScale`).
- **The storage bend** stays at 20°, which now has more room (up to about 32° would clear the shelf).

**Still found:** facing straight away from the camera, the toque and head still hide what's carried. They hide less than before, and from 45° on it shows. The user will judge in play whether this still needs a fix (for example, an icon above the head).

---

## 2026-11-09 — Characters v9.2 in the kitchen: big heads, barrel bodies, stubby legs (proportion greybox)

**User's call:** test the concept sheet's proportions in the real kitchen first, in the scene's current look (toon ramp, soft ink, chunky bevelled shapes, decision 2026-11-08).

**Done:**
- **One body kit,** `SM_Characters.fbx`, made by `build_characters()` in `blender/build_pilot_models.py` with the same helpers, bevels and toon materials as the props. Each hair, hat, face feature and accessory is its own mesh. `CharacterArt` (Editor) builds a person from a `Look` on the same frames as the old chef (feet, waist, a shoulder per arm), so `ChefArmsView` and the storage bend work unchanged.
- **Proportions:** waist 0.45 m, head 0.62 m across (top at 1.64 m), toque to 1.94 m. Head and toque are 47% of the height. Short sleeves with mitten hands.
- **Players** are the chef in whites, with the player colour on the toque's band, the neckerchief and the apron (2026-11-08). The game's colours stay (orange, teal, raspberry, lime), not the concept's.
- **Customers** are one prefab with six looks built in: regular, office, date night, tourist, retiree, gym. `CharacterLookPicker` shows one at random as each customer appears. **The dishwasher** wears grey with a slate apron, in no player's colour.
- **Fitting the look:** the darkest hair is a warm brown (`CharacterArt.DarkHair`), not black, so it bands like everything else. The apron curves round the belly instead of being a flat panel.
- **Fitting the world:** nothing worn reaches more than 0.38 m from the centre (the capsule is 0.35 m), so hair and hats don't show through walls and counters. The cap's bill and the bandana's knot were cut back to fit. The bend into storage is 20°, not 40°: the bigger, higher head would go through the shelf's top board (worked out in `CharacterArt.StorageBend`).
- **Checks:** `CharacterProportionTests` (proportions, reach, hats above the eyes). `StorageArtTests.ChefBendsAtTheWaist` now checks the new body: its waist is 0.45 m, and its height is the head's 1.64 m, toque to 1.85–2.0 m. Before, it checked the old chef's 0.85 m waist and 1.7 m height. `Tools/Game/Art/Capture Characters` renders the people in the kitchen from the game camera.

**Found:** facing straight away from the camera (about ±30°), a chef's head and toque hide a carried plate completely. At 45° and beyond it shows. The old chef hid less, because its head was smaller and its toque narrower. This needs a design decision.

**Not yet:** the office's people (boss, bodyguards, receptionist, accountant, tailor) are still built from the old chef. Hair, hats and outfits can't be chosen in game yet (the tailor).

---

## 2026-11-09 — Chef customisation: modules, slot colours, a profile that outlives campaigns, the tailor

**User's call**, after reviewing the character concept sheet v9.2 (`Art/Direction/concepts/2026-11-09_character-system_v9-2.png`):
- **Modules:** skin tone, hat, face, accessories and body. **Hair is part of the face:** picking a face changes the hair with it, not a separate choice. The body is an outfit, from realistic kitchen looks (chef whites, line cook, pastry, sushi, pizza, wok) to fun and crazy ones.
- **Customers wear hats too,** to give certain characters movement and make them memorable (the tourist's sun hat, a regular you recognise by their cap).
- **Hat on or hat off.** With the hat on, players pick its shape, which isn't limited to kitchen hats: devil horns and other crazy headwear too.
- **Player colour is fixed by slot** (Player 1 is always the same colour), not chosen. Two players can never pick similar colours, and the four colours stay readable. It sits on the spots every outfit and hat keeps for it (see the 2026-11-08 entry: apron and neckerchief).
- **The look is saved in a player profile that lasts across campaigns,** like the cookbook, not in the campaign save.
- **Crazy outfits and hats are earned,** not all available from the start. What earns each one is still open (cuisines, reputation milestones and so on).
- **Players customise in the boss's office, at the tailor** (`docs/office-scene.md`): stand on a plinth, A = change look, saved at once.

**Reason:** Your look is part of "your restaurant, your identity". Slot colours keep co-op readable. A profile that outlives campaigns matches the cookbook, so losing a campaign doesn't take away the chef you've built. Unlocks give a reason to keep playing.

**Proposed constraints, to test in play:** every hat and outfit has a marked player-colour area (with the hat off, a headband or clip carries it); hats have height and width caps (hoods, doorways, never hiding the carried item or what's behind); when a player and a customer wear the same kind of hat, the player-colour area and the ring tell them apart (check in a busy service); each hair style has a flattened under-hat version; accessories go on the head, neck and face only, so the hands stay free to carry.

**Docs now out of date:** `game-design.md` §15.4 says changing a player's appearance saves with the campaign; it now saves to the player profile.

---

## 2026-11-09 — Reviews are written when shown, so a language switch rewrites them all

**User's call:** changing language mid-campaign must localise everything, old reviews included, automatically. This is step 1 of the plan (`ideas.md`, "Localising reviews and replies").
- **Saves keep what was said, not the sentence.** Each review saves the templates its line was written from (`LineParts`: the line, and an influencer's hashtag) and the templates of the owner's reply (`ReplyParts`: greeting, heart, sign-off), plus the facts that filled them (`Facts`: dish, side, doneness ordered and served, wait, time left, restaurant). The guest's name comes from their name slot.
- **The key is the English template itself,** the way gettext works. Translators map English templates to their language (`Translations`, empty until step 3). Rewording an English line never breaks an old save, which keeps the old template and still reads in English.
- **`ReviewText` writes each line and reply when it's shown,** in `Locale.Language`. Templates, dish and doneness words come from that language's table, and the guest's name from their slot. Anything without a translation shows in English. The dashboard (quotes, replies, orders, the Menu tab's dishes), the main menu's best review and the in-service review popup all use it.
- **Existing saves upgrade themselves.** `ReviewTextRecovery` turns each template into a pattern, finds the one a saved English sentence came from, and recovers its facts from the words: "31 minutes, no steak. Left at 20:05. #medrare" gives back the template, 31, 20:05, steak and the hashtag. Replies built from parts are traced part by part.
  - It runs when the kitchen loads a campaign and when the menu reads the slots. The next save keeps what it found, and reviews already traced are left alone.
  - Lines from before the voices (the first fixed replies) can't be traced and show as saved.
  - `ReviewTextTests` checks every template in the grill and generic voices traces back to the same sentence.
- **The English and the old sentence are still saved** (`Line`, `ReplyText`) as a fallback, and for anything reading saves directly.

**Not yet:** UI strings (labels, hints, the Menu warning, "Guests mention" topics) are step 2, with Unity's Localization package and a language setting. The voices' translations are step 3.

---

## 2026-11-09 — Currency follows the language

**User's call:** dollars in English, and currencies follow the language.
- **`Locale` holds the language and how money is written in it.**
  - English: "$2,340.50", with the symbol first and a comma for thousands.
  - Spanish: "2.340,50 €", with the symbol after a space and a dot for thousands.
  - Negative amounts get a minus sign ("−$62.40"). Price bands follow the symbol too ("$$" or "€€").
- **Amounts are the same numbers in every language,** with no exchange rate: a $24 steak is a 24 € steak.
- **Everything that writes money goes through `Locale`:** the money HUD, review popups, the plan screen, the layout editor's prices, the Review dashboard, the main menu cards and the debug overlay. `ServiceConfig`'s fixed currency symbol is gone.
- **Names and money share one language:** `GuestNames.Language` is `Locale.Language`. It's English until the game has a language setting (`ideas.md`, "Localising reviews and replies", and the plan for the whole game).

---

## 2026-11-09 — Shorter replies; guest names that translate

**User's call: replies shouldn't be unnecessarily long.**
- **Most replies are now short:** 40% are a one-liner (50% when the owner is fed up). Most of the rest are two parts, usually the heart of the reply and a sign-off. All three parts happen 5% of the time.
- **The cap is 110 characters** (was 150). When a reply runs long, the greeting goes first, then the sign-off.

  "Thanks for being straight with us, Tony. We let the tickets pile up and you paid for it. Come back on a Tuesday. It's calmer." now tends to read "We let the tickets pile up and you paid for it. Come back on a Tuesday. It's calmer."
- **The month metrics still pass:** replies 90–93% unique, none more than twice or within 10 days.

**User's call: guests' names should be local in a translated game** ("Juan S.", not "John S.", in Spanish).
- **Saves keep a guest's name slot and initial** (`ReviewRecord.GuestSlot`, `GuestInitial`) as well as the English name. The guest book still knows guests by the English name.
- **`GuestNames` holds a first-name table per language, slot for slot.** Each slot is a role (a guy called Mike is Miguel in Spanish), not a literal translation.
  - There's English and a Spanish example; `GuestNameTests` checks every language fills every slot.
  - The dashboard and the main menu show names in `GuestNames.Language`, a stand-in until the game has a language setting and Unity's Localization package.
  - A language with no table shows English, and older saves show the name they kept.
- **Replies address the guest by their name in the player's language.**

**Flagged for later (`ideas.md`):** review and reply text is still saved as finished English sentences. Translating those needs saving which line was used plus its facts, and writing the sentence when it's shown.

---

## 2026-11-09 — Measured variety; fuller replies; an owner who tires

**User's call: every restaurant's interactions should feel unique, measured, not hoped for.**
- **`VoiceMetrics` plays a busy month in a restaurant's voice:**
  - four reviews a day, a realistic mix of complaints and praise, and every kind of guest;
  - returning guests;
  - replies to 80% of reviews (fitting 70% of the time), with the owner tiring;
  - the campaign's no-repeat memory.

  It reports how unique the reviews and replies are, the most uses of any one text, and the shortest gap between repeats.
- **`VoiceMetricsTests` holds the grill to it on three seeds.** No reply may appear more than twice in 30 days or within 7 days of itself. No review line may appear more than 3 times or within 5 days.

  When a test fails, the voice needs more lines, not looser targets. It already caught two: the "apologised, worse again" return lines were too few, and so were a tired owner's apologies.
- **Today's results:** replies 100% unique; reviews 73–78% unique, at most twice or three times a month, never within a week. **Tools/Game/Write Review Samples** now opens with this report.

**User's call: replies of varying length, more natural, using names, and rude when the day's been long.**
- **Replies come in three lengths.** About a third are the short one-liners. The rest are built from an opener (usually with the guest's name), the heart of the reply and a sign-off, up to two lines on the card. That makes hundreds of combinations per kind of reply.
- **The owner's mood:** each reply posted today and each walk-out tonight wears on them. After 3 they're tired, after 6 fed up (`ServiceConfig` → Tired After, Fed Up After).
  - Tired replies admit the long night.
  - Fed-up apologies turn passive-aggressive ("Yes, it was slow. Everyone has mentioned it. Repeatedly.").
  - Fed-up pushback turns openly rude ("Maybe try the chain place by the highway. They have a laminated menu.").
- **The no-repeat memory grew from 80 to 200 lines**, about a month of reviews and replies built from parts.
- **The dashboard measures each reply's wrapped height,** and its card grows by a line when it needs two.

**User's call: rudeness costs return visits.** Each reply's mood is saved with its review (`ReviewRecord.ReplyMood`). A guest's willingness to come back is multiplied by it: ×0.8 after a tired reply and ×0.4 after a fed-up one (`ServiceConfig` → Return After Mood). A fed-up apology still beats silence, just. A reply's rating nudge is unchanged. The metric targets and the mood thresholds are first guesses.

---

## 2026-11-09 — One reply per review, and returning guests who remember

**User's call: one reply per review, not a conversation.** The owner's reply sits under the review. Its words show what kind of reply it is: an apology sounds sorry, thanks sounds grateful (or tone-deaf, to a complaint), and digging in sounds proud. The guest's answers and the likes are gone, from the data, the writer, the dashboard and the save. This supersedes those parts of the two entries below.

**Returning guests (user's choice of next step), built:**
- **Who returns:** only guests who have posted before, since reviews are how the game knows them. The guest book (`GuestBook`) is rebuilt each day from the review history, so it needs no save of its own.
- **When:** a reviewing guest is someone who's been before 30% of the time, when anyone might come back. Each returns at most once a day.
- **Who comes back depends on how they were treated last time.** A fitting reply to a complaint (an apology) weighs 3. No reply weighs 1, and a dismissive reply (thanks or standing your ground) weighs 0.3. Thanks for praise weighs 2, and no thanks 1.2. This is the reply's real effect: players see who comes back and what they say (learn by playing).
- **What they write remembers it:** lines per memory, for a better or worse night ("Came back after the apology. Glad I did." against "Still no reply. Still not right."). There's a generic set for every memory and the grill's own. They keep their name and kind of guest, and they're 1.5× likelier to post.
- **On the dashboard,** their review's details end "back again", or "visit N" from the third.

**Proposed while building (flagged for review):** every number above (30%, the weights, 1.5×) is on `ServiceConfig` → Reviews, a first guess to tune in play. A new guest's generated name can match a known guest's by chance, and they'd then be counted as the same person. That's rare, and left for now.

---

## 2026-11-09 — Not every guest posts a review; the grill's full voice

**User's call:** like in real life, a guest can leave happy or disappointed without posting about it.
- **Each guest posts by chance.** It's likeliest after a walk-out (65%), then a complaint (50%) or praise (45%). A forgettable "decent" night is rarely written about (15%).
- **Some kinds of guest post more than others.** The chance is multiplied by the guest: regular ×0.7, foodie ×1.3, influencer ×2, critic ×3, tourist ×1.1, local ×0.8, capped at certain.

  All of these are on `ServiceConfig` → Reviews, and they're first guesses to tune in play. The rating, the review count and "Guests mention" come from the reviews posted.
- **The dashboard counts guests, not reviews,** wherever it means guests: "Tonight's guests" and the Menu tab's orders and money read every guest served or who walked out. The rating column reads the reviews. "All time" reads a per-dish tally saved with the campaign (`CampaignSave.Dishes`), so it no longer depends on reviews either.

**The grill voice, written in full** (`ReviewVoiceDefaults.Grill`):
- About 130 lines, with every complaint type covered by at least two.
- Each kind of guest has its own lines.
- Every reply combination is written to read as what it is.
- Hashtags for influencers.

**Writing rules the code now enforces** (`VoiceFitTests`):
- A review line fits one line of the card: 44 characters with the longest facts filled in.
- An influencer's hashtag is only added when it still fits.
- Replies fit 72 characters.
- Every fact a line names is one the writer knows.
- A line starting with a fact gets a capital.

**Doneness complaints have their own reply group,** so replies naming the order ("you asked for medium rare…") answer only under- and overcooked reviews. Other food complaints get general food replies.

**Tools/Game/Write Review Samples** writes a sample week (each guest, their review, a reply or none, and returning guests) to `Screenshots/ReviewSamples.md`, for reading the writing as players will meet it.

---

## 2026-11-09 — Review voices: the system for varied reviews and replies

**User's call:** build the system before writing much content. Built, with starter writing to deal from:
- **A voice per restaurant style plus a generic one** (`ServiceConfig` → Voices, editable in the inspector). Each voice has:
  - guest lines per topic, optionally for one kind of guest;
  - owner replies per reply and per what the review was about (praise, the food, the wait);
  - warm and dry guest answers;
  - how often each kind of guest comes;
  - hashtags.

  The grill/steakhouse voice and the generic one exist. The old review lines joined the generic voice.
- **Guests have a persona:** regular, foodie, influencer, critic, tourist or local, weighted by the style. Influencers add a hashtag and draw four times the likes.
- **Lines use the visit's facts:** {main}, {side}, {ordered}, {got}, {name}, {restaurant}, {left} and {wait}. A line naming a fact that isn't known is skipped, and lines with facts are favoured two to one.
- **No repeats:** the last 80 lines used are remembered and saved with the campaign, so words don't come back across days. When every fitting line was used lately, the one used longest ago returns.
- **Replies write a thread** (`ReviewReplies.WriteThread`): the owner's reply in the style's voice, about what the review said, then the guest's answer. It's warm if the reply fits, with 3–14 likes, and dry if it doesn't, with 0–2. The same reply to the same review always reads the same. Old saves' replies fall back to the fixed lines.

**Proposed while building (flagged for review):** how likes are counted, the personas and their weights, and the size of the memory (80) are first guesses to tune in play. The starter writing is a first pass.

**Not built yet (ideas.md):** returning guests who remember a reply, running jokes, and more voices (one per cuisine in Phase 7).

---

## 2026-11-09 — Reply effects show in the thread, not as chips

**User's call:** no effect chips on the reply wheel. Instead, the owner's reply as it appears in the thread, and how the guest answers it, hint at the intended effect. Players read the effect rather than seeing numbers, which keeps learn-by-playing. The design (and the wider push for varied, funny reviews tailored by restaurant style) is in `ideas.md`, "Reviews and replies with variety, wit and a voice per restaurant". Not built yet.

---

## 2026-11-09 — Softer office contrast; gentle lamps in the kitchen

**User's notes:** the office's dramatic lighting is liked but too much at times, and there's too much contrast next to the kitchen, which is mostly flat. The kitchen should get some light sources too, but without strong light and shadow contrast. Strong contrast belongs only to specific areas of the office.

**Done:**
- **The office is softer:**
  - a brighter moon and night ambient;
  - every room's fill up by a third;
  - the spotlights at 60% of their strength;
  - the desk lamp and fire a little lower.

  Rooms keep their personalities, but the pools of light no longer go to near-black around them.
- **The kitchen gets lamps** (`KitchenLighting`, called by the kitchen builder):
  - three broad, overlapping ceiling lights over the kitchen and two pendants over the dining room;
  - four brass sconces on the dining room's side walls with small, soft washes;
  - the grill's glow now counts too.

  Lamp strength is 1.4, against the office's 3, so they warm the scene evenly rather than lighting spots.
- **`OfficeLighting` is now `LampLighting`** (in `Game.Presentation`), used by both scenes.

**Not committed:** the regenerated kitchen scene, main menu and furniture prefabs. Another session's uncommitted builds of those were already in the working tree. **Build Greybox Kitchen** brings the lamps in, and the next commit of the kitchen scene will carry them.

---

## 2026-11-09 — Windows with rooms behind them, people passing, brighter rooms

**User's notes:**
- The city's windows lacked a natural light texture.
- Some office rooms were too dark.
- People passing behind windows would bring the life discussed earlier.

**Done:**
- **Painted window glass** (`WindowArt`), generated textures, six lit kinds of room behind the glass:
  - a lamp's warm glow between curtains;
  - blinds two-thirds down;
  - an office with ceiling panels and monitors;
  - a hanging lamp;
  - a plant on the sill;
  - one lamp lit in a corner.

  Dark windows hold the night sky's blue with a streak of reflection. A window that lights up takes a kind at random. The boss's tower shows offices.
- **People passing** (`WindowSilhouette`): about one lit window in five on the near buildings has someone who now and then walks across it. The wall hides them as they come in and go out, cropped to the glass, then nobody for a while. Only visible in Play mode.
- **Brighter rooms,** keeping the personalities:
  - a soft fill light in every room in its own tone (welcoming reception, kept-dim guarded doors, firelit boss, showroom tailor, fluorescent test kitchen, dark-blue miniature room, museum hall, cosy lounge, greenish accountant);
  - a stronger moon and ambient;
  - lamp strength raised from 2.5 to 3.

---

## 2026-11-09 — The office at night, each room lit its own way

**User's notes:** the scene looked flat. It happens at night, so play with light sources outside and inside the office, giving each room its own personality. It's fine for lamps to light shapes naturally, disregarding the toon finish.

**Done:**
- **The toon shading takes lamps.** It ignored point and spot lights until now: only the sun lit anything. `ToonCommon.hlsl` gains `LocalLights`: a smooth, natural falloff that wraps a little round the sides facing away. It's added to the toon and room surfaces through URP's Forward+ light loop.
  - It's gated by a global, `_GameLocalLights`, which the office's `OfficeLighting` sets. Unset, it's 0, so the kitchen looks exactly as before (checked in a capture).
- **Night:**
  - a low, cool moon instead of the sun;
  - a dim blue ambient;
  - a deep blue haze and sky, so the far city fades into the dark while its windows glow.
- **Each room's light:**
  - **reception:** warm pendants;
  - **guarded doors:** amber sconces and a cold spot on the bodyguards;
  - **the boss's office:** a golden desk lamp, amber floor lamps, and a new fireplace that flickers;
  - **the tailor:** showroom spots on the plinths;
  - **the test kitchen:** fluorescent tubes, one faulty;
  - **the miniature room:** a spot on the table and a blue glow off the model city;
  - **the hall of fame:** museum spots;
  - **the lounge:** floor lamps and a new television flickering blue;
  - **the accountant:** a green banker's lamp under a stuttering tube;
  - **the terrace:** string lights along the rail.
- **Floor lamps and lamp posts** light themselves.
- **In the city:** the pool glows, the billboard is floodlit, the steak sign, pergolas and decks glow warm, the heliport has a pad light, and the crane has a floodlight.
- **`FlickerLight`** drives the fire, the television and the faulty tubes, with the maths in `AmbientMotion` (tested).

**Moved:** the boss's sofa moved back to make room for the fireplace.

**Flagged for review:**
- **Strength:** lamp strength is set to 2.5 (`OfficeLighting`).
- **Readability:** the darkest corners need checking with chefs in them, in Play mode.
- **Phase:** this replaces "always dusk" from earlier today.

---

## 2026-11-09 — The camera fits spread-out players; roofs with real detail

**User's notes:** with two players far apart, the camera didn't fit them, and it should zoom out to. The rooftops were too simple and unrealistic, lacked detail, and didn't feel alive.

**Done:**
- **The office camera zooms out until both players fit,** up to 55 m (it stopped at 22 m before). The kitchen's 100 px chef rule gives way when players split up, since the office has no leash. A capture of two players at the elevator and on the terrace checks it.
- **Every roof** is gravel inside a parapet with a coping stone, at the cornice's real top. Props used to sit 0.35 m into it.
- **Near roofs carry machinery round their edges, much of it moving:**
  - a plant room with a door and louvres, sometimes with a mast and a blinking light;
  - air conditioners with spinning fans, grilles and a duct;
  - an exhaust stack breathing steam;
  - turbine vents turning;
  - a satellite dish, a hatch, and a pipe run on supports;
  - sometimes an engineer in a hard hat working at a unit.
- **The middle of each near roof** is a deck with a smoker, solar panels, a water tower or a lit skylight, or one of the stories. New stories:
  - a billboard with a giant bobbing burger, chasing bulbs and floodlights;
  - a basketball court with one player dribbling and one guarding;
  - a pigeon keeper waving a flag at their flock;
  - window cleaners riding a gondola up and down a facade.
- **The middle ring** gets a plant room and a unit or a tank.
- **The crane's mast** is capped so it always swings below the penthouse floor.

**Not checked:** how it all moves in Play mode.

**Size:** the scene file is now about 23 MB. Making the roof kit into prefabs would shrink it (`ideas.md`).

---

## 2026-11-09 — The city comes alive

**User's note:** the city's details could be more exaggerated and diverse: another tower with a heliport, another with a garden, fun details that are easy to recognise and move. Animate the scene as much as possible, so that looking at the surroundings (entering the terrace, say) brings joy: birds, flickering windows, people smoking on their terraces.

**Done** (`OfficeBuilder.Life.cs`, and the `Ambience` components in Presentation):
- **Near roofs with one thing each going on:**
  - a heliport with a red helicopter warming up (rotor turning), blinking pad lights and a swinging windsock;
  - two roof gardens: lawn, flower beds, two small trees in big planters, a pergola of twinkling string lights, and a gardener bending to water;
  - a yellow tower crane slowly swinging its jib, its load swaying, red lights at top and tip;
  - a steakhouse's giant spinning steak sign ringed with chasing bulbs;
  - a pool with someone drifting round on a rubber ring;
  - two washing lines with sheets swaying and a smoker beside them.

  The trees are only in the gardens' planters.
- **Smokers on roof decks:** they raise the cigarette, hold it, and breathe out a puff as they lower it. The cigarette's ember glows and a thin wisp rises. They use the players' character kit, casually dressed.
- **In the air:**
  - pigeon flocks wheeling round the near roofs on either side;
  - gulls circling high over the city;
  - a blue helicopter lapping the skyline.
- **On the boss's terrace:** pigeons along the glass rail and on his helipad, pecking now and then.
- **Windows:** lights go on and off around the city, and about one window in 25 flickers blue with a television. Air-conditioner fans spin.
- **The office's people:**
  - everyone breathes, out of step;
  - the bodyguards each glance along the room now and then, never together.
- **Built on:** pure functions of time (`AmbientMotion`, tested), so everything is cheap, deterministic and never drifts.
  - Moving things are kept out of static batching.
  - Television windows use runtime copies of their material, so Play mode never edits assets.
  - New Blender meshes: bird, beak, wing, fan blades, windsock.

**Flagged for review:**
- **The gardens have trees in planters,** after the earlier note against trees on flat roofs, because a garden was asked for.
- **Gulls fly over the floor at 11 m.** They can cross the view of the back rooms.

**Not checked:** how the motion feels in Play mode. Screenshots are stills.

---

## 2026-11-09 — A wider centre; the tailor and the miniature through the boss

**User's calls:**
- **The centre column is wider:** 18 m instead of 13 m. The side rooms (lounge, test kitchen, tailor, accountant, hall of fame, miniature) don't need much space and are 8.5 m. The floor stays 35 m wide.
- **The tailor and the miniature open only from the boss's office.** The other rooms all connect to each other.

**Proposed with it (flagged for review):** the terrace is also reached only from the boss's office, by a door either side of his desk; the tailor's and the miniature room's terrace doors are gone. So every visit to the miniature, which is how a day starts, passes the bodyguards and the boss.

---

## 2026-11-09 — A glass tower, and roofs as real roofs

**User's notes:** if the penthouse is glass on all four sides, so should the floors below it be; and trees on flat roofs looked wrong.

**Done:**
- **The boss's tower is a glass curtain wall all the way down.** On every floor, tinted glass sits between bronze mullions over a sandstone spandrel band, with some offices lit behind it. It replaces the sandstone block with punched windows.
- **No trees on the neighbouring roofs.** A roof now carries one of these:
  - a railed roof deck with tables under umbrellas and low planter boxes of shrubs;
  - rows of solar panels;
  - a water tower;
  - the usual air conditioners, skylights, stair huts and aerials.

  The boss's own terrace keeps its trees in big planters, as a luxury penthouse would.

---

## 2026-11-09 — The office in glass, over a city in the game's look

**User's notes:** the outer city had little to do with the game's look and level of detail, especially next to the kitchen; the kitchen's wall and floor materials didn't suit the office; and the outer walls could be taller, with glass windows.

**Done:**
- **Outer walls:** 2.8 m glass on a panelled sill with walnut mullions, on the sides and back. The front is a 1.2 m glass parapet instead. A tall frame there ran across the front rooms in play view, since the camera looks over it (the art bible's dollhouse cut).
- **Inner walls:** walnut panelling with a brass rail and cap, in the room shader, instead of the kitchen's cream plaster. Floors have less grain and softer seams.
- **The city.** The city is rebuilt as the kitchen's world at dusk:
  - warm brick, sandstone, cream, sage, slate and terracotta facades in the same toon light, with framed windows on the faces the camera sees;
  - the nearest roofs, 9–22 m below the floor, carry the detail: water towers, air conditioners, lit skylights, stair huts, aerials, and roof gardens with trees, loungers and umbrellas;
  - a plainer middle ring, and a skyline far behind;
  - dark gravel roofs, so the office stays the brightest thing in frame;
  - linear violet dusk fog, also the camera's clear colour, for depth.
  The boss's tower is sandstone with framed windows.
- **The office is lit for dusk rather than night,** like the kitchen's world. This replaces "always night" in `office-scene.md`.

**Flagged for review:** the front parapet height, how close the near roofs are, and the fog distances.

---

## 2026-11-09 — The boss's floor rebuilt from the kitchen's pieces, at office scale

**User's notes on the first greybox:** some textures didn't work; they expected the 3D models we already have (chairs, kitchen appliances) to be reused; the quality should match the kitchen; and the rooms felt too small to feel like walking through an office. The draft was only a reference.

**Done:**
- **Bigger rooms.** The columns are 11, 13 and 11 m wide and the rows 9, 8 and 10 m deep, so the floor is 35 × 27 m with a 6 m terrace (it was 24 × 18 m). Doors are 2 m wide.
- **The kitchen's materials.** Floors and walls use the kitchen's room shader (planks, tiles, panelled walls) at true size. The first greybox stretched the toon shader's wood grain over whole rooms, which caused the smeared stripes, and capped its walls in a dark trim the kitchen doesn't have.
- **The kitchen's furniture and models.** Counters, hob, fridge, cutting board, knife rack, shelf, tables and bar tables with stools are copied from the kitchen's prefabs, visual only. The dining chair and pictures are reused too. People are built from the chef's model, recoloured and without the toque. Trees, street lamps, water towers and the towers around come from the night city's models.
- **New models only where nothing fits:** `SM_Office` (Blender, same script, `-- --only SM_Office.fbx` so the other models aren't rewritten). It holds the sofa, armchair, executive desk, office chair, filing cabinet, plant, floor lamp, trophy, cowboy hat, tailor's plinth, clothes rail, jacket, umbrella, lounger and helicopter, plus a bevelled block for simple shapes.
- `HouseStyle.RoomMaterial` is now public, so other builders can make room-shader floors.

**Not done:**
- The people don't sit; the boss and accountant stand behind their chairs.
- The rooms' interactions are still Phase 6. There's no join hint, the exterior isn't yet cooler, and the scene can't be reached from the main menu.

---

## 2026-11-09 — The boss's floor as a greybox

**Done:** `Tools/Game/Build Office` (`OfficeBuilder`) builds `Office_Greybox.unity` to `docs/office-scene.md`: the nine rooms, terrace and helipad, the boss's tower and the night city below, with every room dressed in primitives in the toon materials (boss, bodyguards, receptionist, tailor, accountant, the miniature table, the cookbook lectern, and so on). Only walking and joining work.
- **The elevator works.** Each join rings a new arrival bell (`SFX_Lift_Ding_01`, synthesized, lower and purer than the order bell) at once. The doors open 0.35 s later. They stay open while anyone is in the car or doorway, then close after 1.5 s. A join while they're open rings again and restarts the hold, and one while they're closing reopens them. Logic in `ElevatorDoors` (tested), timings in `ElevatorConfig`. A dial over the doors sweeps up to the floor, and a lamp lights while the car is here.
- **The camera** (`OfficeCamera`): a wide shot of the whole floor before anyone joins, then it follows the players at the kitchen's 55° tilt, between 10 and 22 m up. It's a simpler cousin of the kitchen's `FramingCamera`, which depends on the kitchen's systems.

**Proposed while building (flagged for review):**
- Doors from lounge to test kitchen and from accountant to hall of fame, so no room is a dead end.
- The elevator's door wall is 2.5 m, which briefly hides a chef stepping out.
- The boss wears a camel suit and a white cowboy hat; the bodyguards wear sunglasses and red ties.
- The test kitchen's floor is the kitchen's own tile rather than the draft's cream, which would swallow a white chef.

**Not done:**
- Room interactions (Phase 6), a "press A to join" hint, Blender models (art phase), and a cooler, desaturated exterior (art bible: a local Volume).
- The office isn't reachable from the main menu yet: open the scene and press Play.

---

## 2026-11-09 — The boss's floor

**User's calls**, from their lo-fi floor plan; the full spec is `docs/office-scene.md`:
- **The office is the whole penthouse floor of the boss's skyscraper:** nine rooms in a 3 × 3 grid (tailor, the boss's office, back room with the city miniature; test kitchen, guarded doors, hall of fame; waiting lounge, reception, accountant) with a terrace and helipad behind. The full floor stays, rather than a tighter set of core rooms.
- **There's a test kitchen in the office.** It's an office kitchen with bare-minimum appliances where players try things out (searing, cooking, cutting). Its fridge holds the ingredients of every recipe in the cookbook, so it grows as players buy and learn recipes across restaurants. This replaces "No test kitchen in the office" (2026-11-06). The rest of that rule still holds: an ingredient and its mechanics are first met when it's bought, since only learned recipes reach the test kitchen.
- **One big elevator, not four.** Every player joins through it. Each time someone joins, it rings as if it's just reached the floor, the doors open and their chef steps out.
- **Spec first, Art Director later.** The scene is specced from the user's draft now; the AD reviews it before the art phase.
- **A day starts at the city miniature,** once every joined player is at it, looking at the map. The elevator is for joining and coming back, not for leaving.
- **The waiting lounge and the accountant are for fun:** characters and objects to interact with, and easter eggs. The books stay on the Review dashboard.
- **The bodyguards taste the test kitchen's food.** Players serve it to them, and they review it like customers, but pay nothing.
- **The free kitchen sandbox stays as its own mode,** reshaped: players play a restaurant they've already bought, with the recipes they've bought, real customers and phases, and unlimited money for appliances and ingredients (`ideas.md`).

**Proposed in the spec (flagged for review):** rooms 8 × 6 m like the kitchen; every wall cut to 1.0 m except a glass back wall, so the overhead camera sees into every room; a wide shot of the floor before anyone joins, then the kitchen's follow camera; the test kitchen free, endless, unsaved and without a clock; a serving hatch from the test kitchen to the bodyguards, whose reviews are practice and never reach a listing; the reception checker in two close tones and the carpet in burgundy, for readability against the chef and the "Bad" red.

**Where `game-design.md` is now out of date:** §15.2 describes the office as a single lobby and doesn't mention the floor's rooms, the elevator, the test kitchen or starting a day at the miniature.

---

## 2026-11-09 — The Review dashboard drawn to the Art Director's dashboard mockups

**User's call:** the reply wheel keeps the game's three replies in the mockup's look: X Apologise, Y Thank, A Stand your ground, B Leave it (closes without replying). No effect chips: players still learn what replies do by playing. The mockup's Invite back and Defend, with their effects, aren't built.

**Done:**
- **Measured from the six dashboard SVGs** (overview, reviews, reply wheel, reply thread, menu, books), in Inter, Inter ExtraBold and Lora. The mockups' outlined half-stars, pink forkcast logo and icons are exported as sprites. The fonts and art hang off `HudStyle` (Tools/Game/Apply UI Art), so the kitchen scene needs no rebuild.
- **Behaviour that changed with the look:**
  - Reviews sort "needs attention first": walk-outs, then the lowest stars, by the guest's own stars, so a reply doesn't move a review.
  - The Overview shows two reviews and a "N more reviews" bar.
  - Tabs or the stick close the reply wheel and carry on.
  - Replies name the guest ("Sorry, Inés. …").
  - The Menu tab warns about a dish when two or more of its orders walked out today.
  - Books shows the boss's fee as day segments, "Short by" or what's owed, and where the money went as two stacked bars. From day 3 a "How it's going" strip shows each day's net and the team's day.
- **The backdrop is the photo taken at close, blurred and dimmed.** Before, it was a flat dim. The canvas scales with Expand, so 16:10 screens fit.

**Flagged for review:**
- **Reviews filters (user's call):** the stick or arrow keys go up from the top review to the chips; left and right filter at once (All, Walk-outs, 1–2 stars, 3+ stars, Unreplied); down returns to the list. The active chip is dark, with a gold ring while the chips have the focus, and the hint reads "LS Filter". The filter resets each new day.
- The Menu warning adds "Guests wait longest for these." The mockup's tip about grill time only fits medium steaks. The user will review it while testing.
- With two or more cooks, who's ready shows at the left of the hint bar.

---

## 2026-11-09 — The main menu drawn to the Art Director's v11 mockups

**Done (the user asked for the menu to mirror the intended design):**
- **Measured, not eyeballed.** Positions, sizes, radii and colours come from the mockup SVGs (`Art/Direction/concepts/svg/ui_svgs.zip`); text is placed by its baseline, as the mockups set it. `Tools/Game/Capture Main Menu UI` renders the menu at 1920×1080 with sample campaigns that mirror the mockup, for side-by-side checks.
- **Type:** Inter (Medium, SemiBold, Bold) and Lora Italic for quotes, both SIL Open Font License, as static TextMeshPro atlases (Latin-1). They replace LiberationSans on the menu only. The art bible's "e.g. Fredoka / Nunito" doesn't match the mockups; that line is the Art Director's to update.
- **Static art is exported from the SVGs as sprites** (`Assets/_Project/Art/UI/Menu`): the sign with its stickers, the forkcast logo, icons, polaroids, the dashed slot frame. The sign follows v11 (gold chains, a hard drop shadow, an inner frame line) rather than the SVG export, which still had orange posts.
- **The canvas scales with Expand**, so the whole 1920×1080 layout fits on a 16:10 MacBook screen (before, the sides were cut off). The hint bar spans the screen's bottom edge.
- **Dark text on light panels gets a small SDF dilate**, because linear colour space draws it 20–40% thinner than designed.

**Kept different from the mockup (flagged for review):**
- **No followers chip:** followers wait for influencers (2026-11-08). The rating line already shows the review count.
- **The hints show only what works:** on the list they're Browse and Select. The mockup also shows Back and Delete slot there, but those do nothing on the list.
- **Cook dots use the game's player colours**, not the mockup's illustrative ones.
- **The sign's "@ines.eats" sticker, 12.4k bubble and polaroid are now static art**, as in both mockups (the new-player mockup shows them with no campaigns). Before, the sticker showed the best real review.
- **Free kitchen stays dimmed** until it's built (2026-11-08).
- **Selected rows show a short line of what they do** (e.g. Settings: "Cooking precision, spoilage, patience"), extending the mockup's Continue and New campaign sub-lines to every row.

---

## 2026-11-08 — A main menu with three campaign slots; the review app is forkcast

**User's calls (from the user's main menu mockup):**
- **There are three save slots, each a separate campaign** with its own restaurant, money, boss debt and days. This replaces "one campaign at a time, no slots" from 2026-11-06.

  `CLAUDE.md` (hard rules and Saves) and `game-design.md` §15.4 still describe one campaign. They're out of date, and they're the user's to update.
- **The main menu has:**
  - a sign: "Mise en FLEX";
  - Continue (the last played slot);
  - New campaign;
  - Free kitchen;
  - Settings, Credits and Quit;
  - slot cards on the right. Each card shows a photo, name, rating, style and area, the day, balance, the cooks who played it, the best review, when it was last played and the time played. Y deletes a slot.
- **Free kitchen is a sandbox for later:** no money, fee or boss, and nothing saved. It's greyed out in the menu until it's built (`ideas.md`).
- **The review app is called forkcast** everywhere, including the Review dashboard (was "Plates").
- **The menu's backdrop is a 3D night city in the kitchen's style:** toon shading and ink outlines.

**Details (proposed while building; flagged for review):**
- **New campaign takes the first free slot.** With all three full, it asks players to delete one first.
- **Opening the kitchen scene directly in the Editor** continues the last played slot, or starts a new campaign in a free slot. This is for testing, with no menu needed.
- **The city is built from simple shapes for now.** Real models are a Blender task to raise with the user.
- **Cards show the review count, not followers.** Followers wait for influencers (Phase 7).
- **Settings holds the three realism dials that exist** (cooking precision, spoilage, customer patience), saved for all slots. Presets and rebinding controls come with milestone 6.
- **The cookbook outlives campaigns.** Whether the three slots share one cookbook is open, to decide with milestone 4.

---

## 2026-11-08 — Money out, the fee every 5 days, and the Plates dashboard

**User's calls (Phase 5, milestone 2):**
- **The fee to the boss falls due every 5 finished days in a restaurant, not 7.** It's a tuning value. This replaces "every 7 days" in `game-design.md` §15.4, and the collection day also sets the next period's goal.
- **The fee is sized at about three good days' profit,** so a good period leaves money over and a bad one goes into debt. It's a tuning value, to adjust in play.
- **The morning delivery is charged at the ingredients' prices** until street vendors replace it in milestone 3.
- **Replying to a review (Y) gives a small reputation nudge.** There are three canned replies: apologise, thank or stand your ground. A fitting reply softens a bad review a little, and a poor one makes it slightly worse. One reply per review, shown under it.
- **Books shows today and the period so far, plus the boss and progress:**
  - money in and out, line by line;
  - what's owed to the boss and when the next fee falls due;
  - how the restaurant is doing day by day (rating, guests, money).
- **The dashboard follows the user's mockup** (named forkcast since the main menu decision) (`plans/vision for management UI.png`).

**Details (proposed while building; flagged for review):**
- **The dashboard is a full-screen view over the kitchen during Review,** not a separate Unity scene. It's still read-only, and it avoids reloading the restaurant every day.
- **Replies:** apologising fits complaints and thanking fits praise. Standing your ground never fits, which players learn from the listing. The nudge is about ±0.25 stars on that review's weight in the rating, as a tuning value.
- **The rating in the listing is the average of every review so far,** after reply nudges.
- **Broken plates are no longer replaced free;** plates are bought in the shop, a few at a time.
- **The restaurant's name is a placeholder until it's chosen when buying in Phase 6.** Day 1 is a Monday.

---

## 2026-11-08 — Saves: a title card, the morning after, everything as it lies

**User's calls (Phase 5, milestone 1):**
- **At launch with a saved campaign, a small title card asks first:** "Continue · Day N" or "New campaign". New campaign asks again, since there's only one campaign. Nobody can join until one is chosen. The boss's office replaces the card in Phase 6.
- **Continue starts the morning after the saved day,** in Plan, with the Review already seen.
- **Everything is kept as it lies:** food on counters, pans on hobs with what's in them, knives wherever they were put, dirty plates in the sink and on tables, stock in storage with its freshness. What was in a chef's hands goes on a free counter.

**How it works (proposed while building; flagged for review):**
- **The file is `campaign.json` in the persistent data folder.** It's written to a temporary file and then swapped in, so a crash mid-write never breaks the save. It holds:
  - the day and the money (which can be negative);
  - every station's position and angle, a table's chairs, and counted things such as clean plates and trays in their racks;
  - every item with its full cooking state;
  - the hired staff with their morale (fatigue rests overnight);
  - all reviews so far;
  - broken plates.
- **Saved once the day's results are in,** after wages, energy and staff morale.
- **A newer game's file is refused; an older one loads.** Missing fields take their defaults, and a numbered migration step covers anything a default can't. A file that can't be read starts a new campaign and logs a warning.
- **New campaign moves the old file aside** (`campaign.previous.json`) instead of deleting it. Nothing reads it; it's a safety net.
- **Quitting the built game in Plan, Prep or Service asks first:** today isn't saved. Stopping play mode in the Editor can't be caught, so it just loses the day.
- **Continue reloads the scene with the save,** carried by a small static (`CampaignLaunch`), like the earlier rejoin exception. Play tests save into a throwaway folder, never the real campaign.

**Not kept yet (TODOs):**
- what a member of staff is carrying when the service ends;
- cuts already made on a half-cut piece on the board;
- hob dial settings (hobs start the morning off).

The cookbook file comes with the cookbook in milestone 4.

**Reason:** One clear save point once the day is decided, with nothing lost or duplicated, so the restaurant really grows from day to day.

---

## 2026-11-08 — Phase 5: walk-to vendors, free prices, debt below zero, dirt from what happened

**User's calls (plan in `plans/phase-5.md`):**
- **Ingredients are bought at street stalls players walk to,** through the front door, carrying crates back. Going out during service costs real time.
- **Players set each dish's price freely,** in steps of 1, with a suggested price shown. Higher prices bring fewer customers, who expect more.
- **Money can go below zero before the boss exists.** It shows as owed to him on the dashboard, with no consequences until Phase 6.
- **Service dirt comes from what happened** (spills, burnt pans, customers eating, busy floors), so a chaotic service leaves a messier morning.

**Reason:** Each keeps decisions in the players' hands and lets them learn by playing: shopping is a trip that costs time, the right price is found from reviews, and the morning's mess reflects the service.

---

## 2026-11-08 — The current look is the reference for all future models and visual style

**Decision:** (the user's words) The direction the art has taken as of today is good, and it rules future models and visual style. That means the stylized models, the three-band toon ramp, the soft screen-space ink (a darker shade of what's underneath, mostly silhouettes, no hull outlines), and the banded contact shadows as they stand on main. New assets match this look. Changes to it are the user's call, made by playing here. They aren't delegated to the Art Director or another project.

**Reason:** The user played the kitchen after the softer ink and likes the result. A fixed reference keeps new assets consistent and stops the style drifting through outside hand-offs.

---

## 2026-11-08 — Plated food leaves the plate room to breathe

**Decision:** (the user's note) Food on a plate shows at 0.6× its size, relative to the plate (`ItemSizes.OnPlate`). The steak can be big on its own on a counter (about 1.42×), but plated it is smaller (about 1.26×, some 60% of the plate across), leaving room round it for a side, a garnish or a sauce.

---

## 2026-11-08 — Bug fixed: the fridge lid stood open at the start of the day

**Decision:** (the user's bug report) The chest fridge's lid stood open from the start of the day until a player opened and closed it. Its "last held open" frame started at `int.MinValue`, and "frame now minus that" overflowed to a negative number, which read as "held open just now". It now starts at a plain negative frame. A play test checks every fridge lid is shut and flat after the kitchen opens.

---

## 2026-11-08 — The dirty plate in the sink looks like the dirty plate

**Decision:** (the user's notes) The dirty plate in the sink wears the dirty plate's own grime, the same plate shader and dirt as a dirty plate carried in. Its material (`MAT_Plate_Dirty`) has the grime on, so it shows in the Editor too, and washing fades it as before. It lies nearly flat (tipped 12° towards the tap), 1.2× its size, just under the water with its far edge breaking the surface. A first try propped it up at 40° with separate food scraps on it; that read as a speaker cone and didn't match the dirty plate, so it was dropped. Each time a dirty plate goes in it lands differently: turned any way, tipped 6–16° in a random direction and shifted a few centimetres (`RestPoseShuffle`, the user's note).

---

## 2026-11-08 — An onion that doesn't read as a pumpkin

**Decision:** (the user's note) The onion was a bright orange ball with even dark ribs from pole to pole, a pumpkin's cues. Now it is:
- a muted copper-amber;
- a slimmer teardrop drawn up into a long pointed neck;
- five faint, uneven papery lines of different lengths;
- a longer curling dry tuft and wispy roots.
The halves share its new profile, and its menu icon is re-rendered.

---

## 2026-11-08 — A four-knife block with brass-collared slots

**Decision:** (the user's notes) The knife block holds four knives, up from three, and is wider (0.46 m) to fit them. Each slot is a wider dark opening framed by a brass collar standing proud of the face. Knives in the block show 1.5× their size, with more of the bolster and blade out of the slot. A full slot shows a big handle, and an empty slot a dark opening in brass, so it's clear at a glance how many knives are there.

---

## 2026-11-08 — Softer shadows under counters; the plate rack in light steel

**Decision:** (the user's notes) The floor under the counters went too dark, so the contact shadow is lighter (the ramp's AO step goes to 0.87, from 0.78) and so are cast shadows (sun shadow strength 0.45, from 0.6). The plate rack is the same light steel as the tray rack's frame rather than near-black.

---

## 2026-11-08 — Plating at the plate stack; plate piles with their rims showing

**Decision:** (the user's notes)
- **Food in hand at the clean plate stack goes onto a fresh plate** in one press, and the chef then holds the plated food. The prompt reads "plate it". It needs a clean plate on the stack, and works for food only, not a pan, plate or container, which hold their own (`ItemTransfer`, rule 8).
- **Plates up a dirty pile sit well off the one below:** half to all of 11 cm, before the counter's 1.4×, and tipped up to 8°. The rims of the plates underneath show, rather than only the top plate's coloured edge.

---

## 2026-11-08 — A dropped-looking plate pile; the knife block faces front; no cutting dial for the layout cursor

**Decision:** (the user's notes after playing)
- **A pile of dirty plates on a counter looks dropped, not stacked.** Each plate is nudged up to 5 cm and turned, and the ones up the pile tip up to 5°. The bottom one lies flat. Before, the plates above the first sat exactly on it and missed the plate size on counters, so they showed smaller than the bottom one (`CounterRestView` now counts a pile's levels as on the counter).
- **The knife block stands in the middle of its counter with its sloping face to the front.** It had been turned a quarter to show the handles full length; the user prefers it facing out.
- **Bug fixed:** with the knife in the chef's tool slot, the cutting dial showed under the layout cursor in management mode, for instance while placing a cutting board from the shop. There the chef's target is whatever the cursor is over, so the dial thought the chef was at the board. The dial now ignores chefs who are managing. A play test checks it shows at the board and not for the cursor; the test failed before the fix.

---

## 2026-11-08 — Steel sheet trays and gastronorm containers

**Decision:** (the user's references) The tray and container racks were piles of grey discs, and the items were flat boxes.
- **A tray is a steel sheet tray** (0.72 × 0.56 m, shallow, with a flat rim). The tray rack is an open steel frame with runners, holding its four trays one above another.
- **A container is a deep, tapered gastronorm pan** with a flat rim. The container rack is a stack of six, nested loosely: each one is nudged and turned a little, as if dropped back on the pile, not shaped exactly into the one below (the user's note).
- **Follow-up (the user's notes):** containers are rectangular and lower: 0.36 × 0.22 × 0.14 m deep. Deeper versions (0.32 and 0.20 m) and a near-square footprint were tried and found too tall. Portions start 6 cm up, as if the pan were part filled, so they show from above. The idle stack shows at 1.6×, about 0.58 m across and 0.42 m tall, using more of its counter so it's easy to spot.
- Food sits on the pans' floors. The racks lose their top tray or container first. On a counter, a tray keeps about its own size (0.75× on top of the counter's 1.4×), since it already spans most of the counter.
- `PanArt` builds them; without the models, the greybox shapes stay.

---

## 2026-11-08 — Volume from the sun; items sized by where they are; a bolder board and plate rack

**Decision:** (the user's notes after playing: things looked flat, the fridge lost its volume, and the steak, cutting board and plate rack were hard to notice at a glance)
- **The sun is high and from the left,** almost square to the camera (`HouseStyle.SunAngles`, 58° up, 96° round). Tops now land in the ramp's lit band, fronts in its mid band and right-hand sides in its shadow band, so every box reads as a box. With the sun behind the camera, tops and fronts were both fully lit, and only the removed hull outlines had told them apart.
- **Shadows fade on faces turned from the sun** (`ToonCommon.hlsl`). A face lit edge-on or from behind is already in its darker band, and at grazing angles the shadow map aliased into teeth, first seen on the fridge front.
- **Items show at a size that depends on where they are** (`ItemSizes`, the anchors' scale; an item eases between sizes as it moves):
  - on a counter or the cutting board: 1.4×;
  - carried: 1.3×;
  - on the grill: 1.15×;
  - fridge, shelves, plates and the pan: 1×.
  **Change from the choice:** food in a pan was to be 1.15×, but the steak is made to fill the pan, so the pan stays at 1×. The knife on a counter now lies at 1.15× on top of the counter's 1.4×.
- **Models evened out:** the onion is 0.8× and the steak 1.15× (`IngredientType.ModelScale`). **Change from the choice:** the steak was to be 1.3×, but then it overflows the pan. Later trimmed on counters only (`CounterScale` 0.88, the user's note): about 1.4× its old size there instead of 1.6×.
- **The cutting board is deep walnut** with a thicker rim (6.5 cm wide) standing 1.5 cm proud of the cutting surface. Food still sits on the cutting surface.
- **The plate rack and its plates show 1.25× bigger,** the frame is dark steel and plate rims are a stronger blue (on every plate).

**To check by playing:** whether 1.4× on counters crowds the counters; the size easing when items fly between places; the new light direction across the whole kitchen and dining room.

**Follow-up the same day** (the user's notes after seeing it):
- **The cutting board** fills most of its counter (0.88 × 0.75 m) and is a thick (7 cm), plain slab with rounded edges and a low lip (3 cm wide, 8 mm proud). The walnut has no grain. The deep rim and inset panel read as a picture frame, and the grain was distracting. Food sits on the 7 cm cutting surface.
- **Plates and prepared dishes on a counter** show 1.3× on top of the counter's 1.4×, about 0.58 m across, barely off centre (`CounterRestView._scale`). The plate rack stays 1.25×.
- **Food on a counter sits a little off centre (up to 9 cm) and turned** (`CounterRestView`), differently on every counter. The spot and turn come from the counter's position and the food's kind, so they're steady, and a round onion faces every which way. This applies only directly on a counter: not in containers or on the board.

---

## 2026-11-08 — Softer ink: thinner, a shade of what's underneath, mostly silhouettes

**Decision:** The user played the screen-space ink and found it far too strong ("it completely removes the fun"). They approved these changes to the brief below (relayed by the Main build session):
- **Thinner:** 1 px at 1080p and never more than 2 px (`_Thickness` 1, new `_MaxThickness` 2). The brief's 1.5 px scaled with screen height, so it came out at about 3 px on the MacBook's Retina display.
- **Coloured by what's underneath:** the line is the scene colour × 0.45 (`_InkShade`), with a quarter of the house brown mixed in (`_InkColorMix` 0.25), at half strength (`_InkOpacity` 0.5). It was the fixed #332117 at 0.92. Wood edges read as darker wood, and the white chef gets a soft brown-grey line.
- **Silhouettes, not creases:** `_NormalThreshold` 1.1 (was 0.6), so cabinet doors, counter lips and plank grooves mostly stop inking.
All of these are material properties on `MAT_Post_InkOutline`, set by `RenderLook`.

**Hull outlines removed everywhere** (the user's call, after being shown that the chef is barely outlined by the softer ink alone; keeping them on the chef only was the other option). The Outline passes are gone from `SH_Toon`, `SH_Plate` and `SH_FoodLayered`, along with their helpers in `ToonCommon.hlsl`, the per-material widths (thinner on plates and wires) and the debug toggle. Materials still carry `_OutlineColor` and `_OutlineWidth`, which nothing reads now; they can go in a later material clean-up.

**To check by playing:** whether the chef stands out enough without its own outline.

**Smoother ink lines** (the user's note after playing: "some outlines still look pixelated"; fix suggested by the Main build session). The depth and normals buffers aren't anti-aliased, and SMAA mostly misses a soft line, so each pixel was either inked or not and diagonals stair-stepped. Two changes:
- The edge test is averaged over four points a quarter pixel apart (`_SubSamples`, on). This gives partial coverage along diagonals. It costs four times the taps: 32 instead of 8 per pixel.
- The fade-in ramps are wider: threshold to 2.5× it (`_EdgeRamp`), where it was 1.5× for depth and 1.3× for normals. Weak edges now fade in instead of switching on.
In 1080p renders the improvement is modest, mostly on thin diagonals like the plate rack's wires. If the GPU cost shows, `_SubSamples` 0 keeps only the wider ramps.

---

## 2026-11-08 — Screen-space ink, three-band ramp, banded SSAO

**Decision:** (the art director's brief, approved by the user)
- **Ink outlines drawn over the screen** (`SH_InkOutline`, `MAT_Post_InkOutline`, a Full Screen Pass before post-processing). They come from depth and normals at a constant on-screen width (1.5 px at 1080p), on every opaque surface, room surfaces included. The ink colour comes from `HouseStyle.OutlineColor`, so it has one source.
- **The old inverted-hull outlines stay for now.** At the kitchen shot they're about half a pixel wide. *Tools > Game > Debug > Toggle Hull Outlines* turns them off and back on, each material keeping its own width, so the two can be compared. They will be removed once the user approves.
- **A three-band toon ramp** (lit, mid, shadow) in `ToonCommon.hlsl` replaces the two-band step and each material's `_ShadowTint`, which is retired. The sky light is lower (0.12).
- **SSAO, banded by the ramp into one flat contact shadow:**
  - Blue Noise, intensity 1, radius 0.25, falloff 30, depth normals;
  - normal quality, samples and blur as below;
  - no After Opaque, direct lighting strength 0.
  **Deviation from the brief:** the brief asked for a Medium blur at half resolution. That left grainy, speckled fringes once the ramp banded the noisy AO, so the AO runs at full resolution with the High (bilateral) blur. This costs more GPU; if frame time is tight, going back to half resolution is the first thing to try.
- **Shadows:** 25 m in 2 cascades (split 0.4), low soft-shadow quality, depth and normal bias 0.6. No acne on counter tops in the renders.
- **The camera clears to a near-black warm brown,** #120D0A.
- The pipeline settings live in `RenderLook`, which the kitchen build applies (*Tools > Game > Apply Render Look*).

**Reason:** At the overhead kitchen shot the hull outlines barely showed. Screen-space ink holds a steady width and reaches the room's edges. Three bands and contact shadows separate tops from fronts.

**Still to check:** 60 fps with two players on the MacBook. SSAO and the full-screen pass are the main cost; check with the Stats panel or the Profiler in the Editor.

---

## 2026-11-08 — An onion cuts into rings, then on into dice; a clearer knife

**Decision:**
- **An onion cuts in two steps** (the user's note): two chops make **onion rings**, a new ingredient. Three more chops on the rings make dice: five in all, as before. To keep rings, stop and take them off the board. The board keeps cutting what it just cut whenever that can be cut further, with a fresh cutting dial for each step.
- **Rings cook like dice but a little slower**, being thicker. This is starting tuning, copied from diced onion and not yet tested in play. No dish asks for rings yet (`ideas.md`).
- **The prep assistant follows the chain:** asked for dice, it fetches an onion and cuts it through rings into dice.
- **The cut shows its stage on the board:** the onion's first chop leaves two halves lying cut face up; then the fan of rings; each chop into the rings pushes fewer rings aside as the dice pile up; then the dice heap.
- **A knife put down on a counter lies across it, diagonal and 1.6× its size**, so it reads from above. At its real size, pointing straight away, it was a sliver on a whole counter.
- **The sheath only shows while the chef has a knife**, so you can tell at a glance whether a chef is carrying one (the user's note). This replaces "the sheath is there even without a knife" from the entry below.

**Reason:** The user's notes from playing: an onion should go from halves to rings to dice, with stopping for rings; a knife on a counter was hard to see; and it should be clearer whether a chef has a knife.

---

## 2026-11-08 — The knife rides in a sheath; an onion comes apart as it's cut

**Decision:**
- **A leather sheath on the right hip**, a little behind, holds the knife blade-down with only its handle and bolster showing (the user's choice). It replaces pushing the knife into the belt, which read as stabbed into the thigh. The knife still comes out only facing the board with empty hands. The sheath is part of the chef, so it is there even without a knife.
- **Cutting shows its progress** on the board (the user's choice):
  - the first chop splits the onion into two halves lying cut face up, their rings showing;
  - each later chop piles up more dice beside them, and one half goes before the last chop;
  - the last chop leaves the cut onion's heap;
  - bits of onion fly with every chop.
  It's only looks (`CutProgressView`); the cut itself is unchanged.
- **A cut onion is a heap of chunky cream dice** everywhere it shows: on the board, in the pan (browning as one), on a plate and in its menu icon. It used to be a pale flattened ball.
- **Rings on a first pass and dice on a second** stay an idea for later (`ideas.md`).

**Reason:** The user's notes: the knife looked stabbed into the thigh, and the onion cut should be more visual.

---

## 2026-11-08 — Chef arms that act out the work; a real chef's knife; the knife rides in the belt

**Decision:**
- **The chef has arms**: chunky sleeves with mitten hands, hanging from the shoulders and turning about them (`ChefArmsView`). They swing as the chef walks and blend between poses. Hands only reach to points just in front of the chef at counter height, so they don't reach into walls or furniture.
- **A pan is carried by its handle**: both hands side by side on it and the pan out in front, after the user's reference. The pan's handle is longer so both hands fit. Other carried things are gripped with a hand on each side.
- **The knife rides in the belt** at the right hip, handle out so it shows from above (the user's note). It only comes out facing the cutting board with empty hands. There it is held pointing away from the chef with its edge down, while the other hand steadies the food, after the user's reference. Each cut is a fast chop.
- **A flip at a hob tosses the pan**: it lifts and tips, with one hand on its handle. The other hand keeps hold of anything carried, such as a plate (the user's references and note). At a grill there's no pan, so a flip is a flick of the hand.
- **Washing** holds the plate in the left hand and scrubs it in circles with the right, after the user's reference.
- **The knife is a chef's knife** at a cartoon but believable size: a 0.26 m blade whose spine runs straight to the tip, a steel bolster, and a black handle with silver rivets. The big 0.36 m blade read as a sword.
- **Stations report what they did:** the chef's interactor raises `Acted` when a station accepts a press (a chop or a flip), which is what the arms act out.

**Reason:** The user's notes: a carried pan was hard to understand, the knife looked like a sword, the knife should sit in the belt until the board, and cutting, washing and flipping should show on the chef.

**Still to check by playing:** how the chop, toss and scrub feel in motion; that hands never pass through a counter in any layout or facing; the belt knife from every camera angle.

---

## 2026-11-08 — A big cartoony knife, an angled knife block, an onion, and bigger held items

**Decision:**
- **The knife** is big and cartoony (after the user's chef reference): a 0.36 m blade, wide and curved to its point, and a chunky dark wooden handle with brass rivets.
- **The knife rack** is an angled wooden block, after the user's reference. The knives stand in three slots in its sloping face, a little blade and the whole handle showing. The block stands turned a quarter, its slope to the side, so the knives lean across the overhead camera's view; facing the camera, their handles were foreshortened to stubs.
- **The onion** is a coppery bulb with a pointed neck, a few fine stripes, a dry tuft and roots, after the user's references. It lies on its side with its neck tilted up; from above, a round striped top read as a pumpkin. It shows wherever an onion does: in hand, on a board, in the shelf baskets and in its menu icon.
- **What a chef carries shows 1.3 times bigger** (the carry and tool anchors), so a carried pan or knife reads at a glance. It's back to its own size once put down.

**Reason:** The user found the props so realistic in proportion that it was hard to tell a chef was carrying a knife, or even a pan.

---

## 2026-11-08 — A full bin refuses more, with a 🚫 over it

**Decision:**
- **The bin's bag fills after 8 things** (`BinStation._capacity`, was 24 and only for the look). A plate scraped into a nearly full bin keeps what didn't fit.
- **A full bin takes nothing more.** A red 🚫 sign pops up over it, bobbing, and rocks when a player with something in hand faces it. Players have to leave food somewhere else (`BinFillView`).
- **A prep assistant** with cut food and a full bin keeps hold of it and waits by the bin; it's dealt with when they're let go at the end of the day.
- **For now a fresh bag goes in each morning** (start of Plan). Later, once the restaurant's outside exists, players take the bag out to street containers round the building (docs/ideas.md).

**Reason:** The user's notes: the bin should fill with less, and once full, a small sign should show and players must put things elsewhere. Supersedes "for now it only fills, and being full changes nothing in play" in the 2026-11-08 sink and bin entry.

---

## 2026-11-08 — 4x MSAA on the PC pipeline

**Decision:** The PC pipeline asset (`Assets/Settings/PC_RPAsset.asset`) renders with 4x MSAA, and the game camera adds SMAA (High) on top. SMAA smooths the steps inside surfaces too, such as toon light bands and thin lines. SMAA needs the camera's post-processing on, so the PC pipeline no longer carries the template's sample effects profile (bloom, vignette, tonemapping); post-processing adds nothing else. The mobile asset is unchanged.

**Reason:** The user found the shapes' edges pixelated and noisy to look at. MSAA smooths geometry edges and the toon outlines, which are geometry too (an inverted hull). It's cheap for a scene this size, within the 60 fps target on a MacBook with two players. Still to check in play.

---

## 2026-11-08 — Effects carry the fun: bubbles, fog, sparks and flies

**Decision:** Effects are a deliberate layer of fun and liveliness in the scene, not understated touches (the user's note).
- **Washing:** a few big soap bubbles (a shiny ring texture, pastel tints, about five a second) drift off the sink in every direction, towards the chef too, then wobble, swell and pop. Splashes are chunky droplets thrown well up.
- **The fridge:** opening it releases a short breath of cool fog, nothing while it stays open. The fog is dense and pushed out over the floor to where the chef stands, so it wraps round them. The fog uses a texture that's white all over, fading only in its alpha (TEX_Puff). The built-in particle texture's darkening edges showed as a dark shade.
- **Cooking:** sparks are big, bright and plentiful from middling heat up (`HobView._emberRate`).
- **The bin:** once it's past half full, flies circle it, more the fuller it gets (`BinFillView`).
- **The sink basin** is deeper (0.23 m), with walls darker than its floor and the water at 0.79 m, so its depth reads from the camera. Under it the cabinet's body stops at 0.66 m.

**Reason:** The user's notes that these effects should be noticeable and funny, and that the sink still lacked visible depth.

---

## 2026-11-08 — A deep sink with water, a bin that fills, plain dining furniture, a less yellow scene

**Decision:**
- **Dining furniture** (tables, the bar, chairs and stools) is plain wood colour, without grain.
- **The sink** has parts in their own tones so its shape reads: a light steel top, a raised rolled rim round the opening, a darker basin, a dark drain and a chrome tap. It's always half-full of see-through blue water, so its depth shows, with the plate being washed visible under it.
- **While anyone washes** (chef or dishwasher), water runs from the tap onto the water with splashes, and soap bubbles float on it (`SinkWashView`). It's seen from the wash progress moving and lingers a moment after.
- **The bin** is an open tub lined with its black bag, so it looks deep and empty. A rubbish mound rises up the bag with each thing thrown away (`BinStation.ThrownAway`, `BinFillView`), full at 24.
  - For now it only fills, and being full changes nothing in play.
  - Taking the bag out comes with the restaurant's outside (docs/ideas.md).
- **The scene is less yellow:** a near-white sun, neutral sky light, slightly cool shadows, and cream walls and countertops instead of yellow ones. The wood keeps its warmth.

**Reason:** The user's notes on the dining furniture, the sink, the bin and the overall colour.

---

## 2026-11-08 — Gas hob: fire is the only heat cue; soft gas flames; a grey knob with a red pointer

**Decision:**
- **Heat lights:** the gas hob has none; its heat shows only in its fire. The induction hob keeps its three heat lights, lit green, orange and red.
- **The gas hob's top** has three greys: the rim lightest, the cooking surface a step darker, the grate almost black.
- **The fire** is soft and see-through (`Game/FX/Flame`: additive, fading at its tip and silhouette), after the user's photo and reference image.
  - 16 thin tongues in a ring at the pan's edge, so even a low flame peeks out from under the pan, with an inner ring of short blue cores.
  - Blue at low and middling heat. Past about a third of the dial the tongues turn yellow at the base and orange at the tip, the orange glow and embers appear, and the cooking surface glows round the burner.
- **The knob** is dark grey with a red pointer stripe, the only red part. Off, it points to the top of the hob; it turns clockwise through 270° to full heat, like a real gas knob.

**Reason:** The user's notes on the hob, the fire references, and the knob being hard to pick out.

---

## 2026-11-08 — A grate with presence, layered fire, and counters that join

**Decision:**
- **The range's top** is a steel rim round a recessed black spill tray, so the grate has room to show. The rim is wide at the front as a control strip, where the knob now sits, and at the back, where the heat pips sit.
- **The grate** is heavy cast iron: a thick frame with corner knuckles and feet, four thick arms on the diagonals, and pan supports whose tops are the pan's resting height, 0.91 m.
- **The fire** mixes cartoon and realistic. Orange tongues lean out past the pan, with shorter yellow-hot cores between them. Soft additive flame puffs rise from a ring just outside the pan, and embers drift off. All of it grows with the heat (`HobView`), and embers only really start at higher heat.
- **Counters right beside each other join into one continuous top.** Each top is rounded along sides open to the room and square and flush where it meets another counter, chosen from 16 versions of the top (`CounterJoins`, tested). Cabinet bodies and bases run the full width, so they touch. It's worked out in each counter's own axes and follows the layout editor. Only counters join: hobs, sinks and the rest keep their own edges.

**Reason:** The user found the thin grate sluggish and without personality, wanted fire that mixes cartoon and realistic effects, and wanted side-by-side counters to read as one surface.

---

## 2026-11-08 — Notes on the overhaul: plain tops, a snug round bin, flames and a grate

**Decision:**
- **Countertops** are plain: one pale colour, no grain.
- **The bin** is a round grey tub with a black bag over the rim, about 0.54 m across. It keeps its 1 m slot, so layout and gameplay are unchanged. Within the slot it slides to sit snug against whatever is beside it: a wall or another station, into the corner if there are two, and centred if it's boxed in on opposite sides (`SnugFit`). It re-checks every frame, so it follows when players rearrange the kitchen. Its collider still fills the old 0.7 m block.
- **The gas hob** has a cast-iron grate whose tops are at the pan's resting height (0.91 m), over a small burner head. A ring of twelve flame tongues leans out from under the pan. They're off at zero heat, then grow, widen and flicker with the dial, going from blue at the very bottom to orange. At high heat they lick well past the pan's edge, so heat reads at a glance, like the user's reference.
- **The chefs' player colour** is on the apron and neckerchief. The rest of the chef stays white jacket, dark trousers and toque, so co-op players stay easy to tell apart.

**Reason:** The user's notes and reference images on the overhaul. Earlier the grate was left out because the old wide burner ring sat at the pan's height; a small burner head under the grate removes that clash.

---

## 2026-11-08 — Art overhaul toward the vision image

**Decision:** The scene is restyled toward the user's vision image, one step at a time:
1. Warm light, warm shadow sides and thin dark-brown outlines.
2. The room: tiled kitchen floor, planked dining floor, panelled cream walls.
3. One wooden counter family.
4. Restyled range, sink, fridge, bin and racks.
5. Dining furniture and wall details.
6. Person-shaped chefs.

Each step's palette and light settings live in `HouseStyle` (Editor), which the kitchen builder applies to the scene and to every toon material.

- The fridge stays a chest with a glass lid, so stock shows from the camera, but gets a white enamel look. The image's tall upright fridge would hide its stock again.
- The kitchen floor is a mid-tone warm grey tile, a step lighter than the image's dark slate, so dark pans and food stay visible on it.
- Countertops stay light wood, so plates, food and the cutting board stand out.

**Not in scope:** the HUD and build menu shown in the image, which belong to the UI work, and the camera angle, which belongs to the Camera session. The image's camera is higher and closer to isometric.

---

## 2026-11-08 — Kitchen-real colours and counter-sized proportions

**Decision:**
- **Sink:** the tap stands at the model's back. The greybox layout turns the sink so its back is against the wall, and players turn it themselves when they move it. Nothing works out which wall is nearest. The basin is wider and fills most of the counter.
- **Hob:** stainless steel (light steel body, charcoal top) instead of red. One big light-steel heat knob sits on the top's front-right corner and turns clockwise with the heat, up to 270°, next to the heat pips along the back. The front is plain steel: the dial and oven window that were there read as unexplained circles.
- **Fridge:** the four baskets are steak-sized (0.32 × 0.26 m) and sit towards the middle (±0.18, ±0.15).
- **Plate rack:** plates stand upright again, as in the first design. The rack has thin rounded light-steel wires and short tines between the plates. Plates get a soft blue rim, and plates and wires get thinner outlines.

**Reason:** The user's references show appliances that are real-coloured and fill their counter the way real ones do, with detail in lines rather than in thick shapes. The exaggerated rack and the red hob read as toys.

---

## 2026-11-08 — Style test on the hob; notes on the sink, fridge, board, plates and rack

**Decision (user's calls):**
- **A style test before restyling everything.** The gas hob, its pan, the steak, plates and the board get toon shading: flat bands of light, a cool shadow tint, a soft rim light and a dark outline. They also get a bold palette, with a red enamel hob, a dark top, a cream dial and a teal oven window. The hob is reshaped: one huge dial and a big round oven window instead of three knobs and a door. The pan is mostly rim and a stubby ball-ended handle. Everything else keeps its current look until the direction is agreed.
- **The sink's tap rises from the middle of its back edge** and arches forward over the basin, so water always comes from the back of the sink, whichever way it's turned. The sideways lever that read as a spout is now small, at the back.
- **The fridge's four baskets are small and gathered in the middle,** so its stock shows from every side.
- **The cutting board is big,** 0.7 × 0.55 m, using most of its counter, with wood grain drawn by its material.
- **Dirty plates show a texture, not shapes:** short broken wipes, splatter, a faint tide mark, grease and crumbs, in dull browns and oranges. Solid stains could be mistaken for meat.
- **Plates and the plate rack are toy-like and exaggerated:** a thick plate with a fat rolled rim, and a rack of fat round bars.

**Reason:** the earlier pass was chunky but still realistic. Shading, outlines and colour do most of the cartoony work from the overhead camera, so they're tried on one station first.

**Not verified:** how it reads in play. The grime is subtler than before, so check it from the game camera.

---

## 2026-11-08 — A cartoony style for every model, and dirty plates that look dirty

**Decision (user's calls):**
- **Models are exaggerated and cartoony, not realistic,** so they read in a rush: chunky shapes, fat rounded edges, oversized details such as knobs, handles and the tap. The reference photos guide what something is, not its proportions. Sizes the game relies on stay exact: footprints, the counter height, where pans and plates rest, hinges, slots and how far the fridge lid swings.
- **This covers every model now:** hob, pan, steak, fridge, shelf, chef, plates, plate rack, sink and board. The chef is a bean shape with a white toque. Plates, the board and the steak are thicker.
- **Dirty plates show their mess:** a sauce puddle with a smear dragged from it, tomato red or gravy brown, greasy patches and clusters of crumbs. Each plate looks different, and gets a new mess every time it's used. A plate in the sink visibly comes clean as it's washed.

**Reason:** in a frantic kitchen, things have to be recognised at a glance from the overhead camera, which exaggerated shapes and a messy-looking dirty plate do better than realistic ones.

**Not verified:** how the chunkier shapes and the grime read in play, and whether the toque crowds the camera when chefs stand close together.

---

## 2026-11-08 — The sink only washes; dirty plates pile on any counter

**Decision (user's calls):**
- **The sink washes one plate at a time and keeps none.** Drop a dirty plate in and wash it, and it comes out clean in your hands, ready to carry to the plate stack. The cleaner works the same way. Replaces the sink holding 2 plates, dirty and clean (2026-11-04).
- **The dirty pile station is gone.** Dirty plates pile up to 3 on any counter. A clean plate, food or anything else can't go on a pile, and a pile only takes dirty plates. Where the pile stood, by the sink, is now an ordinary counter.
- **The waiter** clears stools onto counters, onto an existing pile before an empty counter, so plates gather in a few piles. With nowhere to put a plate, they leave the stool for now.
- **The cleaner** washes whatever's in the sink first, then clears stools, then collects plates from counter piles. They take each clean plate to the stack. If the sink's busy, they pile the plate they're carrying on a counter.

**Reason:** the sink becomes a quick station that players pass through, and dirty plates become visible clutter wherever they're left. That makes falling behind on washing up something you see across the kitchen.

**Details (proposed while building):** a plate dropped with nowhere to go breaks, and is replaced in the morning like other broken plates. The pile limit and spacing are settings on each counter.

---

## 2026-11-08 — Models for plates, the plate stack, the sink and the cutting board

**Decision (user's call):** these get Blender models, following the user's reference photos. Only meshes change: footprints, anchors, slots, colliders and rules stay as they were.
- **Plates** are white with a well and a gold rim, everywhere a plate appears.
- **The plate stack** is plates standing upright in a black wire rack, facing the camera. The front plate is the first to go.
- **The sink** is a stainless unit with one big basin, a swan-neck tap, open legs and a slatted bottom shelf. Its drainer went when the sink stopped keeping plates (see the entry above).
- **The cutting board** is bamboo with rounded corners and a juice groove, without the photo's logo. It's lighter than its counter so it stands out.

**Reason:** it brings the kitchen's most-used stations up to the look of the hob and storage, without touching how they play.

---

## 2026-11-08 — The fridge is a chest at counter height with a glass lid

**Decision (user's call, after play; details proposed):** a tall fridge with two ingredients per shelf hid stock and blocked the view, so it becomes a chest fridge, like a counter.
- **Counter height (0.9 m), with all four slots on one level,** the chest's floor at 0.7 m. They copy the face buttons' diamond seen from above: Y and B at the back, X and A at the front.
- **A glass lid in a white frame covers it,** so the stock shows from above whichever way the chest is turned. The sides are solid white, with a pale lining inside.
- **The lid lifts while RT is held:** hinged along the back, it opens to 75° in 0.25 s with a small overshoot, never past upright, and closes in 0.3 s. It stays over the chest's own footprint, so no wall, furniture or chef can be in its way. Being glass, it hides nothing even when it stands on the camera's side. Tunable on `FridgeDoorView` (`_openAngle`, `_openSeconds`, `_closeSeconds`, `_overshoot`). The cold air spills over the front edge as it opens.
- **The chef bends at the waist to reach in,** as at the shelf.
- **Replaces** the glass fridge from earlier today: its sliding doors front and back, its glass sides, and the rule making sides glass or solid. The shelf stays as it is (below). The 2026-11-07 entry's "tall storage in the back-left corner" now holds only for the shelf.

**Reason:** one level under a glass top shows all four ingredients at once from the overhead camera, in any layout, and a lid that only rises can't clash with anything around it.

---

## 2026-11-08 — Stock shows from any side: a glass fridge and an open shelf

**Decision (user's call, details proposed):** players can turn storage any way, so what's in it should show from every angle.
- **The fridge has a glass top and glass shelves,** in a white frame of corner posts, a solid base and a rim round the top.
- **Each of its four sides is see-through, unless it's right against a wall and doesn't face the camera** (user's call, revised after play). A wall within 0.2 m of a side makes it solid white (`_reach` and `_gap` on `FridgeShellView`).
  - **Furniture never counts:** the fridge shows above a counter beside it. Neither do people or loose items.
  - **The side facing the camera** (south, within 45°) **stays see-through even against a wall,** because the camera looks over the low walls into the fridge.
  - It checks every frame, so it follows the layout as pieces move, even while the fridge is carried.
  - The first version also made sides solid against furniture and against walls on the camera side. In play, that hid stock that should have shown.
  - **Open, the left and right sides are glass, and the front and back are sliding doors.** The back door is the front one turned round.
  - **A chef opens the door on their side,** and the half in front of them slides over the other, as before. If the door on their side is solid, the other one opens. The cold air puffs out of whichever door opened.
- **The shelf is an open rack:** shallow boards 0.4 m deep (were 0.7 m), centred. The upper tier's board is its top, with no empty posts above it (user's call, after play), so it's 1.05 m tall (was 1.5 m). From the camera's 55° view, the upper board no longer hides what's on the lower tier.
- **Stock sits in the middle of both,** where it used to sit towards the front. It's then the same from the front, the back and either side.
- **Replaces** the fridge's solid sides and back, its pale lining, the shelf's deep boards, its top board and its 1.5 m height, and slots sitting towards the front.

**Reason:** with every piece placeable and turnable, any side might face the camera. Stock that only shows from the front would hide what's in storage for half of the possible layouts.

**To check in play:** whether the glass and the shallow boards still read clearly as a fridge and a shelf from the overhead camera.

---

## 2026-11-08 — The cutting board is a counter of its own

**Decision (user's call):** the cutting board works like the knife rack. It comes built into its own counter, and the two can't be separated.
- **One piece:** it's bought, carried, placed, rotated, moved and sold like any counter. The last board still can't be sold.
- **Price 50:** a counter (30) plus the board (20).
- **The starting kitchen's board** is now this piece, where its counter stood.
- **Controls unchanged:** A puts food on the board or takes it off, and X cuts with the knife.
- **Replaces** the 2026-11-07 rule that boards sit on counters and move from one free counter to another. Nothing sits on a counter any more, so the "it goes on a free counter" placement check is gone.

**Reason:** the same as the knife rack. Every piece is placed the same way, so the board is a clear layout choice with nothing extra to learn.

---

## 2026-11-08 — The fridge has sliding glass panes

**Decision (user's call, after play; details proposed):** the fridge's hinged door becomes two half-width glass panes on two tracks across its front, like a drinks fridge.
- **The left pane runs on the back track, the right one in front.** Each covers half the opening plus a 4 cm overlap.
- **Opening slides the half in front of the chef:** the left pane slides right behind the other, or the right pane slides left in front of it. The side is chosen when it opens from shut, by whoever opened it.
- **It never leaves the fridge's width,** so walls, furniture and chefs can never be in its way.
- **Motion:** 0.2 s to open and 0.25 s to close, eased in and out with no overshoot. A pane on a track glides and stops. The cold mist and the open and close sounds are unchanged. Tunable on `FridgeDoorView` (`_slide`, `_openSeconds`, `_closeSeconds`).
- **Replaces** the 2026-11-07 hinged door: checking for room to swing, choosing a hinge side, and chefs stepping aside, along with the menu keeping its station while a chef shuffled. Also replaces "the door hinges on the left" in the tall-storage entry.

**Reason:** a swinging door has to share floor with chefs, and moving them out of the way took control from the player. A sliding door needs no floor, so it works the same in every layout.

---

## 2026-11-08 — The knife rack is a counter of its own

**Decision (user's call, from playtest feedback):**
- **A knife rack comes built into its own counter, and the two can't be separated.** It's bought, carried, placed, moved and sold as one piece, like a counter. It sells only when empty, like any piece.
- **Price 55:** a counter (30) plus the rack (25).
- **A takes and hangs knives at it**, as before. Food can't go on it.
- **A bought knife hangs on a rack with room** before going to a free counter. This replaces "a bought knife appears on the first free counter" (2026-11-07) for kitchens that have a rack.
- **Replaces** the 2026-11-07 rule that a knife rack sits on a counter like a cutting board. Boards still do.

**Tried and dropped the same day:**
- **Sharing the board's counter, with the board answering A for both.** The controls got complicated, and real 3D knives laid flat would have crowded the counter.
- **Putting a bought rack on the free counter nearest a board.** Building the rack into its own counter is simpler, and placing it is the players' choice, like any counter.

**Reason:** a rack fused to its counter is placed like everything else, so it's a clear layout decision, with no fixtures to manage.

**For the art phase:** model it as an upright knife block on its counter, which reads better from the overhead camera than knives lying flat.

---

## 2026-11-07 — Playtest fixes: cooked food in storage, furniture landing on chefs, HUD on screen, fridge door

**Decision (from playtest feedback; details proposed):**
- **Cooked food gets its own storage slot.** A slot holds one ingredient in one state: cooked (any doneness level or any browning) never shares a slot with raw stock. Before, a cooked steak joined the raw steaks' slot and, taken oldest first, could never be reached, while the bin only showed raw steak, so it seemed to vanish. Its own view stays in its bin, and the RT menu says "cooked steak". With no free slot, the fridge refuses it, as when full. Staff never fetch cooked food as stock.
- **Furniture never lands on anyone.** A piece being carried in management mode is a preview without collision, so it can't shove anyone. It can't be put down within a body's own radius plus 0.1 m (`_personalSpace` on `LayoutEditor`); before, the check used 0.3 m against a 0.35 m chef. A piece sent back home by Cancel may land on someone who walked into its spot: they step out to the nearest free floor. As a safety net, a chef who ends up 1 m below the floor is put back on the nearest free spot.
- **HUD cards always stay on screen.** Every world-anchored card (shop list, tickets, food tags, review popups, the chop dial and flip gauge) slides back inside the screen, 12 px from the edges (`ScreenMargin` on the HUD style). Prompt cards already did.
- **The fridge door knows its surroundings.** Each time it opens from shut, it checks how far it can swing before touching walls or furniture, hinges on whichever side has more room (left unless the right has 10° more), and eases to a stop 4° short of anything in the way, without overshooting into it. This replaces "the door hinges on the left" in the fridge entry below.
- **The chef who opens the fridge steps aside (user's call, after the door went through the chef).** If they're standing in the door's swing, they shuffle at 3 m/s to just past the door's free edge, no closer to the fridge (backing off just enough to clear the door if they were nearer) and turning to face it. The door holds until they're clear, then follows. The stick cancels the shuffle at once. If there's no free floor there, the chef stays and the door only opens as far as they allow. Anyone else in the swing holds the door back too, and it gives way at once to anyone who walks into it. While the storage menu is held, the chef stays on that station as long as it's in reach, so the shuffle can't switch them to the shelf next door. Tunable on `FridgeDoorView` (`_stepAsideSpeed`, `_stepClearance`, `_followSpeed`).

**Reason:** these all broke the game's readability or let a player lose a chef for good. Animations have to look right wherever players put furniture, since layouts are theirs.

**Not verified:** all of it needs checking in play; a fridge hinged on the right is the door model mirrored.

---

## 2026-11-07 — Tall glass fridge and shelf, ingredient pictures, chefs bend at shelves

**Decision (user's calls):**
- **Storage shows its stock.** The fridge is an upright unit, 1.8 m tall, with a white-framed glass door, and the shelf is a tall open unit, 1.5 m. Each holds two ingredients on each of two tiers, seen from the front, so players can read what's stored from the furniture itself.
- **Slots follow the tiers:** Y and B on the top tier, X and A below, left to right. This replaces the diamond on the counter top.
- **The RT menu shows pictures.** Each slot's line shows the ingredient's picture and count beside its button, rendered from the game's own food models, and falls back to its name if there's no picture.
- **While a chef holds RT:** the fridge door swings open with a puff of cold air, and at a shelf the chef bends forward at the waist. The door raises events for a door sound, which the sound session adds.
- **The fridge and shelf stand in the back-left corner** of the starting kitchen. The knife counter moves to the west wall by prep, and the tray rack to the back wall beside the hob. The rest stays put.

**Reason:** tall furniture hides what stands behind it from the overhead camera. On the west wall, the shelf would have hidden the fridge's door. The back wall faces the camera, with open floor in front.

**Details (proposed and accepted):**
- The door hinges on the left, so the open door swings out beside a chef standing in front, not into them. Two players at one fridge keep it open until both let go.
- The chef is now two placeholder parts, legs and a torso hinged at the waist. It isn't character art: it keeps the same height, colours and nose.
- A prompt card sits above a tall station rather than over it, so it doesn't cover what's inside.
- Stored steak shows as the steak model, and pictures are rendered by Tools/Game/Art/Render Ingredient Icons. A kitchen build renders only missing ones.

**Trade-off:** a chef standing at a unit covers part of it from the camera; the pictures in the RT menu make up for that. Tests that used the back-right gap as open floor now use the floor between the island and the pass.

**Not verified:** the mist doesn't show in still captures, so it needs checking in play.
---

## 2026-11-07 — Kinds of heat and kitchenware replace equipment tiers

**Decision (user's calls):**
- **Appliances differ in kind, not tier.** Instead of basic and better versions of the same hob, there are different ways to cook: a gas hob, an induction hob and a charcoal grill now, ovens later. Any of them takes a pan. Fridges and shelves stay as they are for now.
- **Gas hob:** works as today. A dial, and the pan takes time to heat and cool, so players have to be precise.
- **Induction hob:** three heat steps. The pan gets hot or cold as soon as the step changes.
- **Charcoal grill:** customers ask for one when the restaurant specialises in meat (for example, a review asking how a steak place has no flames). No appliance changes the quality score: what differs is how they cook, not how good the result is.
- **The charcoal grill takes two floor slots:** a hot zone and a cool zone, each holding up to two pieces directly on the grate. A pan or pot takes a whole zone, and X flips what's in it, as on a hob. In the open, two players can cook from opposite sides at once; against a wall, one chef works it alone. Either way, a chef acts on the zone they face.
- **Cooking on the grill:**
  - **Placing:** food goes on whichever zone the chef faces. Starting on the cool zone gives a weaker crust, which lowers quality through the crust score.
  - **X flips the food and moves it to the other zone,** so the chef steps across to follow it. Each press sends it back again, changing how fast it cooks and how it turns out.
  - **No room:** if the other zone already holds two pieces, the food can't be flipped until there's space.
  - **A** takes the food off.
  - **Two pieces in a zone:** A and X act on the hotter one.
- **The grill is always lit,** with nothing to light or keep going. Its glow is part of the room's look, so it's decoration as well as an appliance.
- **Energy is an expense in the end-of-day summary,** next to wages. Electricity is cheapest, then gas, then charcoal and wood.
  - *Proposed, to confirm:* hobs cost by how much heat they used; the always-lit grill costs a flat amount a day; fridges aren't charged for now.
- **Cutting boards and knife racks sit on counters,** and each uses up the counter it's on. They're moved only in management mode, and bought in the shop as many as wanted.
- **Knives are items.** The kitchen starts with one knife lying on a counter and no knife rack; picking it up frees the counter. Chopping needs a knife, so nobody can chop until it's put down somewhere. A bought knife appears on the first free counter.

- **Staff chop without the knife (user's call):** a hired prep assistant cuts by skill and doesn't need the kitchen's knife, the one exception to "nobody chops without it".
- **The grill cooks like a hob at fixed heats (user's call):** the hot zone sears and cooks fast; on the cool zone food cooks more slowly and is much harder to burn, so finishing there is comfortable.
- **Only the gas hob catches fire;** induction and the grill don't. Putting a fire out works like cleaning (§15.8): hold X and the chef pulls out an extinguisher, as they would a mop or a cloth, then a short stick game.
  - *Confirmed (user's call):* a pan left on a lit flame after its food has burnt catches fire a few seconds later, so the smoke alarm is the warning. While it burns, the hob and pan can't be used and the food is lost. Left too long, the hob is ruined and has to be replaced. It doesn't spread for now. The extinguisher is always to hand, with nothing to buy. Built with cleaning in Phase 5, as it shares the stick game.

**Details proposed while building (flagged for review; tunable in `EquipmentTier` and `FurnitureCatalog`):**
- **Induction steps:** low 130 °C, medium 190 °C, high 250 °C; the pan gets there in about half a real second. Off lets the pan cool on its own, like a pan lifted off. Each pull of a trigger is one step.
- **Grill zones:** hot 260 °C, cool 150 °C (below where browning starts, so food finishes there without more crust).
- **Prices:** induction hob 150, charcoal grill 250, cutting board 20, knife rack 25 (holds three knives), knife 15, pan 25. The gas hob stays 80.
- **Energy:** electricity 0.60 a kWh, gas 1.00 a kWh, charcoal 15 per grill per day.
- **What can't be sold:** the last hob of any kind (so the gas hob can go once there's an induction hob), and the last cutting board (a hired prep assistant needs one).
- **A knife or pan bought with no free counter** isn't bought: the shop says so instead of taking the money.

**Reason:** different kinds of heat change how you cook and what the restaurant is, which serves "cooking is a craft" and "your restaurant, your identity" better than numbered upgrades. Boards and knife racks on counters extend the rule that counters are precious (2026-11-04).

**Given up:** equipment tiers (better hobs, bigger storage) and the board rail they were meant to grow. The board already only cuts.

**Later (see `ideas.md`):** ovens, pots, skewers and oven trays arrive with the cuisines that need them. Customers asking for a grill arrives with cuisine and service expectations.

---

## 2026-11-07 — Steak fat and per-steak variety

**Decision (user's calls):**
- **Steaks have fat and no two are alike.** Each has fat round its edge, a seam and marbling, with its own pattern, size and angle. Fat stays a small share of the top: big white patches looked like candy.
- **Fat covers the edge and holds its colour.** It runs down the side of the steak round most of the outline, with gaps where the lean reaches the edge. Fat goes from white to a creamy yellow and stays that way while the lean browns, colouring only when the steak is nearly burnt. Only thin marbling melts away.

**Reason:** real cuts look like this, and the variety stops every steak looking the same.

**Trade-off:** fat hides the doneness bands wherever it covers the side, so the core mostly shows only in the gaps. The doneness chip stays.

---

## 2026-11-07 — Heat pips stay ordinary objects

**Decision (user's call):** remove the pilot's change that drew a hob's heat pips over anything in front of them. Fix what hides them in the HUD pass instead (see `ideas.md`).

**Reason:** play showed the prompt card hides the pips, not the chef, so the change didn't fix the problem. It also added a shader and let pips show through walls.

---

## 2026-11-07 — An art pilot ahead of Phase 9, with a hand-written food shader

**Decision (user's call):** before Phase 9, a pilot replaces the hob, pan and steak with real Blender models, on its own `art-pilot` branch, without changing how the game plays. The look to try is chunky, soft-edged shapes with a few flat colours and little texture, with food slightly oversized so it reads from above. Food shaders are hand-written URP shaders rather than Shader Graph, which `CLAUDE.md` specified.

**Reason:** it tests the Blender pipeline, the food-state shader, overhead readability and frame rate while they are still cheap to change, and it can run alongside gameplay work. Shader code can be written and reviewed outside the Editor, which Shader Graph files can't easily.

**Given up:** tweaking food shaders visually in a graph. Colours and strengths remain material settings. The rest of the art stays in Phase 9, so these three models will look out of place next to the greybox until then.

---

## 2026-11-07 — More kitchen sounds: fire detector, footsteps and small cues

**Decision (user's call):** add a fire detector, footsteps, a clack for setting things down, a till on payment and a chime on the street door. All synthesized placeholders, like the first set.

**Reason:** the detector and chime tell players something without them having to look; the rest give actions feedback and make the kitchen feel alive.

**How it behaves (proposed and accepted):**
- **Fire detector:** one double beep a second, starting with the thick smoke at 75% of the way to burnt. One alarm however many pans are burning, panned towards the worst. It stops when the food leaves the pan, the pan cools below 140 °C, or a flip puts a paler side down. It's a warning only: no cost, and customers don't react.
- **Footsteps:** players only, very quiet, timed by distance actually walked, so walking into a wall is silent. Each chef has a slightly different pitch.
- **Clack:** whenever a chef puts something down or combines items, the same sound whatever it is, for now.
- **Till and chime:** one sound per table paying or party arriving, not one per person.

---

## 2026-11-07 — First kitchen sounds, ahead of Phase 9

**Decision (user's call):** bring a small set of sound effects forward from Phase 9: pan sizzle, bell, chopping and the kitchen door. The first clips are synthesized placeholders from `tools/synth_sfx.py`; a real recording replaces one by overwriting its file, with no code changes.

**Reason:** sound carries information (design doc §13), so it's worth having while the mechanics are being tuned, not only at the polish stage.

**How it behaves (proposed and accepted):**
- The sizzle needs food in the pan and follows the pan's temperature: louder and higher as it heats, silent below 110 °C and between shifts. Food landing or being flipped adds a rush of hiss.
- Chopping sounds by grade: a clean knock on the beat, duller when early or late, a skid on a miss.
- The door's creak is short and quiet, because it gets used constantly during service.
- Sounds are 2D and pan left and right with their place on screen. There's no distance fade, so a burning pan stays audible wherever the camera's attention is.

Tuning lives in `Data/Settings/SoundBank.asset`. This replaces the "Not built: the bell's sound" note under 2026-11-06.

**Not built:** see "Sound still to do" in `ideas.md`.

---

## 2026-11-07 — Size tiers dropped from Phase 4

**Decision (user's call):** a restaurant's size comes from which one you buy on the city miniature (Phase 6), not from upgrading one. Phase 4 ends with the waiter and equipment tiers.

**Reason:** Upgrade tiers would be replaced by the campaign as soon as it's built.

**Given up:** expanding a restaurant you already own. It could return in Phase 6 as knocking through a wall (see `ideas.md`).

---

## 2026-11-06 — Roadmap: a Campaign phase and a guided first day

**Decision (user's call):** two new phases. **Phase 6, Campaign,** covers the office, buying restaurants, the boss, the tablet station, service styles and decoration. **Phase 8, Guided first day,** comes after Content, once mechanics have settled. Content moves to Phase 7, and Art and polish to Phase 9. Phase 5 gains the read-only Review dashboard, vendors, the cookbook and cleaning.

**Reason:** The campaign design adds a lot that no phase covered, and the guided first day depends on nearly everything else.

**Trade-off:** no new player can try the full first-day flow until Phase 8, though its parts can be tested as they're built. Phase 6 is large and may need splitting when it's planned.

**Older entries** use the previous phase numbers.

---

## 2026-11-07 — Food stops cooking on a plate

**User's call:** a pan taken off the hob keeps cooking its food with its leftover heat, but food on a plate stops cooking. Plating is how a chef decides it's done. Replaces carryover cooking on the plate. The stronger carryover I proposed was not built.

**User's call:** the bar shows on pans off the hob too, and the needle slows as the pan cools.

**Details:** food on a plate still cools, so serving temperature still matters. Resting on a plate still counts towards the rest score.

---

## 2026-11-07 — The camera follows the people working; markers for customers out of view

**User's calls:**
- **The door and customers no longer count for the framing.** In a big restaurant the door may be far from the kitchen by design, and players see all of it by splitting up. Staff count like players. Replaces framing the door during service.
- **In the Review, and before anyone joins, the whole restaurant shows.** Once a player joins, the camera zooms in.
- **A customer out of view gets a marker on the edge of the screen:** a round timer while waiting for food, no timer while eating, and a face showing how it went for a couple of seconds as they leave.

**After play (user's calls):** the camera comes in closer and follows chefs more tightly, because it cut them off. It comes no closer than 6.5 m up, so two players together get a comfortable view, and from the kitchen a customer's walk from the door shows a marker for a while.

**After more play (user's calls):** chefs walk a little slower, 4.2 m/s down from 5, and the camera follows closer behind. As a player walks away from the kitchen, the camera moves with them and gently zooms out.

**Camera motion, after play (from user feedback; values proposed, tunable on `FramingCamera`):** it felt artificial, and then dizzying. Its moves are eased twice, so they build up and settle softly, and the threshold that made it move in steps is gone. A look-ahead was tried and removed: it nearly tripled the camera's travel in the kitchen. Height is now eased on its own and the floor position framed for the height it has actually reached, so zooming doesn't slide the view. It backs away at once but waits 0.75 s before closing in, which then takes about 5 s in all from the dining room. Top speeds are 6 m/s across the floor (down from 14) and 2 m/s up and down. With people far apart it leaves 20% of the screen clear at the top instead of 27%, as that room was going unused.

**The camera holds the kitchen (user's call, after a recording showed it moving on nearly every step):** following the chefs made it pan and zoom constantly, which felt chaotic and unpredictable. Now it's anchored on the kitchen, where the hobs and boards are:
- **One still shot of the kitchen:** exactly its working area, 0.5 m past its sides and back for the walls and the units on them, and 2 m in front for the customers at the wall and a little behind them. No margins are added on top (user's call: close it in a little), so it sits about 7.9 m up, down from 9.4 m. Anyone inside it doesn't count, so walking around in there never moves the camera.
- **As soon as anyone leaves the kitchen, it shows the whole restaurant,** centred and still, if that's no more than 1.4 times the kitchen's height (1.3 in the first restaurant). The move out starts at once and goes to one place, instead of creeping after them, which was part of the hook when opening. It counts as leaving 0.5 m past the kitchen, and as back 0.25 m past it, so standing in the doorway doesn't flick it. In a bigger restaurant, the kitchen's shot grows to take them in instead, always keeping the kitchen.
- **Past 18 m up, in a big restaurant, it frames the people alone,** as before.
- **A change of shot is one smooth arc (user's call, from a sketch):** height and floor position used to ease separately, the floor quickly and the height slowly, so the camera slid across first and then dropped or rose at the end, a hook. They now ease together as one point in space, starting and arriving together. It no longer waits 0.75 s before closing in (user's call, after play: it went in with too much delay): coming back into the kitchen, or a player joining, starts the move at once. `_holdBeforeClosingIn` is still there, at 0, and while it waits the camera doesn't move at all. If someone walks out faster than it moves, it hurries along that same arc rather than being pushed sideways off it, which was the rest of the hook when opening.
- **Every move starts at once, and joining is quickest (user's calls, over several plays: first too delayed everywhere, then a little too quick, backing away most of all; it also went too far out before coming back).** The two-stage easing, which started gently, is now one easing that moves off the moment the shot changes and is quickest at the start. It's most of the way there in about 0.9 s backing away (`_zoomOutSeconds` 0.45, down from 1.2), 1.4 s closing in (`_zoomInSeconds` 0.7, down from 1.5), and 0.65 s when the first player joins or a new day starts (`_joinSeconds` 0.32, down from 0.9). In between, closing in was 0.35 and joining 0.12, which the user found prompt but too quick and a bit dizzy; then 0.3, 0.6 and 0.25, still 0.1–0.2 s too quick, most of all backing away. Top speed is 40 m/s (`_maxSpeed`, up from 6), so the cap doesn't slow these moves. Going too far out was a bug: being hurried along its arc pushed the easing's target too, past the shot it was heading for, so it overshot and then crept back. Now it eases on from wherever it was hurried to, towards the same shot, and stops there.
- **The whole restaurant stays still wherever anyone is in the dining room (bug fix, after play: it moved erratically there).** Whether to show the whole restaurant compares its height with the kitchen shot's. That height was measured with the room at the top of the screen for someone far off, which shrinks as they walk away, so in the front metre of the dining room the comparison went over 1.4× and the camera followed the chef instead. It's now measured for the kitchen shot alone, so the ratio no longer changes with where anyone is.
- **The whole restaurant is framed a little closer (user's call, from a screenshot):** it keeps 0.9 m of floor beyond the back of the room instead of 1.5 m (`_wholeFarMargin`), which brings it down from 10.4 m to 9.75 m, 1.24× the kitchen shot's 7.9 m.
- **Nobody walks off screen:** under the easing, a hard limit pushes the camera at once if anyone comes within 0.6 m of the edge or into the top 10% of the screen, so players splitting up fast can't outrun it.
- **While a player manages, the camera follows their cursor, not their chef (user's call):** the chef left standing in the dining room doesn't pull the camera out while the player rearranges the kitchen; the cursor counts as if the chef were there. The chef may be off screen until the player stops managing, which is fine because it stands still.
- **Replaces** easing back as a player walks away from the kitchen, and the 6.5 m closest height as what decides the view in the kitchen. The kitchen's front is assumed to face the dining room at the south; a restaurant laid out another way will need that in its floor plan data.

**Details (proposed; flagged for review, tunable on `CustomerIndicatorHud`):**
- **A plain dot for anyone else out of view,** walking in or queueing.
- **Faces:** 4 stars and up is happy, 2.5 and up neutral, anything else or walking out unhappy.
- **Icons are drawn in code** until the art phase, rather than real emoji.
- **A group's markers spread out along the edge,** one each.

---

## 2026-11-06 — The waiter, the bell, and who's for hire where

**User's calls:**
- **Plates wait on any counter, and players ring a bell (X) to send one out.** It works before a waiter is hired too. Replaces my proposed dedicated pass counter.
- **A waiter serves only the diner whose order matches,** one plate at a time, serving before clearing. Wages are €45-70 a day for now.
- **The pile of dirty plates unlocks the waiter.** The cleaner looks after surfaces and plates; with a pile and a waiter, the cleaner focuses on washing.
- **Waiters and cleaners are only for hire from restaurant two.** Restaurant one only has helpers.
- **Waiters and cleaners wait and roam in the dining room,** not the kitchen.

**Details (proposed; flagged for review, tunable in `StaffConfig`):**
- **`_restaurantNumber` says which restaurant this is,** set to 1, until the campaign provides it in Phase 5. Set it to 2 to try cleaners and waiters.
- **The dishwasher is now called the cleaner** on screen. They don't clean surfaces yet: that comes with cleaning (design doc §15.8).
- **A waiter who slips drops the plate at the counter,** breaking it and losing the dish. The plate is replaced in the morning, like others.
- **If a diner leaves while the plate's on its way,** it goes back on a free counter and rings again.
- **Waiters walk at 3 m/s,** other staff at 2.
- **Staff blocked by someone standing still go another way round, or wait until there's space (user's call).** They never push through a player.
- **With a waiter, the cleaner no longer clears tables,** even against the wall.

**Not built:** the bell's sound, shown only as an "Order up!" label for now. The pile as an unlock is recorded but has nothing to check yet, because the pile can't be bought or sold; it matters once it can (Phase 5). A rung plate that can't go to anyone just stays rung.

---

## 2026-11-06 — Slower cooking, one bar under the pan, and how crust is judged

**User's calls:** a bar guides players through the cooking stages, teaching that more heat means more crust and faster cooking, and less heat a blander, slower steak. The ideal is high heat for crust, flip once, then low until done. A warning comes before burning. Cooking runs a little slower than the clock, at 90% of its pace: full speed burnt too fast, and half and 70% felt too long (user's call). Crust is judged by the better side, and any side close to burnt loses points. Replaces averaging the two sides.

**Details (proposed; tunable in `TuningConfig`):**
- **One bar at a time,** as two gauges were too much: the crust gauge until the first flip, then the stages of doneness, with a needle following the steak's middle. It reads the hottest the middle has been, as doneness does.
- **The warning:** the bar pulses red from browning 1.5 (burnt is 2) on the side in the pan. Flipping clears it.
- **The slower speed is for food and pans only:** the clock, customers and staff run as before. Resting and cooling take longer in real time too.

**Measured for a medium order at half speed, scale by 5/7 for 70%:** full heat to the flip (about 4 s), then 40%, takes about 18 s with full crust marks. Full heat throughout takes 15 s, but crust falls to 0.20. A steady 70% scores as well as searing and takes about as long, so searing isn't the only good way; it's worth watching in play.

**After play (user's call: too slow, and medium went straight to burnt, so well done was impossible):** heat moves through a steak faster (surface to core conductance 0.9 to 1.2) and it browns 15% slower. Medium comes about 3 s sooner, and at full heat well done now comes before burnt.

**Not built:** smoke as a burn warning.

---

## 2026-11-06 — Steak has five stages

**User's call:** a steak goes raw, rare, medium, well done, burnt. Replaces the seven stages, which also had medium-rare and medium-well.

**Details (proposed; tunable on `ING_Steak`):** rare from 45 °C, medium from 55 °C, well done from 65 °C, with orders aiming for 50, 60 and 70 °C. Orders come 30% rare, 50% medium, 20% well done, in the same spirit as the old mix.

---

## 2026-11-06 — Confirmed: mornings, office saves, the first style, keeping a name

**User's calls (the first four were flagged as proposals in the entry below):**
- **Yesterday's mess is cleaned during Prep,** and how messy it is depends on how the service went.
- **The boss's collection is worked out as Review starts and shown on the dashboard.** Warnings and repossessions happen in person the next morning.
- **Everything done in the office saves immediately,** including changing a player's appearance.
- **Only finished days count towards a restaurant's 7-day fee.**
- **The first restaurant is a grill with a fixed service style.** Players choose only its name, and can change the style later from the tablet. The first real style choice comes with the second restaurant.
- **Keeping a restaurant's name keeps its old reviews; a new name starts with none.**
- **Combinations are judged preliminarily like this:** service style sets most expectations, each cuisine adds a few of its own, and only combinations that earn it get custom review lines.
- **Customer photos, if built, stay inside the game,** with no export. The photo system itself is still a proposal in `ideas.md`.
- **Unusual combinations are worth trying in play:** they attract fewer regular customers but more influencers, so an odd pairing is a gamble and not simply harder. To judge once influencers exist.

---

## 2026-11-06 — Review is a read-only scene; saving, style at purchase, decoration choices

**User's calls:**
- **Review is a read-only scene,** a dashboard players browse: the service, customers, dishes, money lost and earned, overall progress, and more. It starts as soon as the day ends and the last customer leaves. Replaces the Review card over a walkable kitchen.
- **The game saves as Review starts, and the moment a restaurant is bought in the office.** Leaving a restaurant at any other point in the day loses that day's progress, and players must be clearly told so. Replaces the two saves proposed earlier today.
- **Review and the tablet station give the decoration hints:** "great steak, but I expected candles" appears in Review, and on the tablet's history during Prep.
- **Decoration includes floor patterns, wall colours and windows.**
- **Cuisine and service style are two choices, and any combination is allowed.** Reviews and tips judge how well it works, so fine-dining pizza is possible.
- **Style and name are chosen when buying a restaurant.** Players pick a new name or keep the previous owner's.
- **Recipes cost money to unlock.** Players browse them freely and decide which to buy.
- **A customer who orders something out of stock** loses patience as usual.

**Details (proposed while writing this up; flagged for review):**
- **Anything else done in the office also saves immediately,** such as borrowing from the boss, so it can't be undone by quitting.
- **Only finished days count towards the 7 days before fees fall due.**
- **Because Review is read-only, the boss's collection is worked out as it starts and shown on the dashboard. Anything he does in person, such as a warning or repossession, happens the next morning.**
- **Service mess is cleaned the next morning,** because Review starts as soon as the last customer leaves. Dirt is saved with the restaurant.

**Reason:** A read-only Review makes one clear save point, right after the day is decided, so no result can be undone by quitting during it.

---

## 2026-11-06 — The cookbook, hidden decoration taste, changing style, saving at Review

**User's calls:**
- **The cookbook sets the menu.** It opens from the management menu. A restaurant's style decides which ingredients the market sells and which recipes the cookbook offers. Players choose which recipes to unlock, which configures the menu, and that tells them which ingredients to keep in stock. Replaces "buying a new ingredient unlocks recipes".
- **Each recipe shows** its complexity, the appliances and ingredients it depends on, its approximate cost and the value it generates.
- **Recipes carried over from an earlier campaign can be used at any restaurant,** ignoring the style's limits, which gives a new chef a real head start. The cookbook is the same thing as the recipe book.
- **Players discover the cookbook on the first day:** after buying the plate pile, the boss asks for the dish he wants, so players see why they next need steaks and onions.
- **Decoration should match the style, and players aren't told what that means.** Fine dining expects bigger tables, lamps, plants, art, solemn colours and more staff; a burger place values functionality such as bar tables, bins and game stations, and more eclectic decoration.
- **Style can be changed at any time from the tablet station,** at a real cost, such as selling furniture that doesn't suit the new style.
- **The game saves at the Review phase and only then,** before the next day starts. Replaces continuous saving.
- **"Vibe" isn't a separate system:** a restaurant simply starts with its previous owner's style and decoration.

**Details (proposed while writing this up; flagged for review):**
- **Two saves within Review:** one on entering it, so a bad service can't be undone by quitting during the Review, and one on leaving it, so a purchase made in the office between days counts. Trips to the office happen at this point.
- **Collection and the boss's escalation happen in Review,** so they're saved straight away.
- **Quitting at any other time replays that day from the morning.** This means players can quit mid-service to retry a bad one. That seems an acceptable price on a couch game.
- **Changing style has no fee of its own.** The cost is half-price furniture sales, new decoration, and reviews suffering until the place matches.
- **The boss's dish is free to unlock.** Later recipes may cost money (open, see `ideas.md`).

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
- **The building is the first restaurant, a small steakhouse, 8 × 9 m (user's call):** a kitchen 5 m deep and a dining room 4 m deep to its south. A low wall runs between them, so the camera sees over it. The door is 1.2 m wide, west of the hatch, and swings open for whoever's near. It never blocks anyone. The sink and dirty pile start on the east wall, to leave about 2 m of the dividing wall free for tables.
- **Tables are bought with none at the start,** and only go in the dining room; kitchen furniture stays in the kitchen. Within a room, layouts are still free. Tables bought in Plan or Prep count from that day's service.
- **No pass and no stools (user's call):** the wall between the rooms closes up, and the restaurant starts with three bar tables against its dining side, served across it. This frees that row of the kitchen. It replaces the counter service of 2026-11-02.
- **The dishwasher clears any table against the wall,** from the kitchen side. Tables away from it are for players, and later the waiter.
- **A customer with no way to their seat gives up:** a lost cover, no penalty, and the seat goes to the next. Someone leaving always gets out, and someone blocked by people for 2 seconds walks through, so a bad layout costs money but can't freeze a service.
- **Pushing a seated customer moves them,** and they walk straight back.
- **The camera** keeps its 55 degree tilt and leaves 27% of the screen clear above the floor at the top, as the back wall stands up into that (user's call: more room above the wall). It doesn't show much beyond the building's walls, and centres on it once it all fits. It pans quickly and zooms slowly (user's call: subtle and effortless). Staff don't count towards the framing, as they stay in the kitchen. Tunable on `FramingCamera`.

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

## 2026-11-08 — The main menu's night city is modelled in the kitchen's style

**Decision:** The main menu backdrop is a modelled low night city (`SM_City.fbx`, built by `blender/build_pilot_models.py`) in the kitchen's toon look: bevelled blocks with cornices, framed lit windows with sills, shopfronts with striped awnings, roof water towers, lantern lampposts, rounded cars with head- and taillights, and round-crowned trees. `MainMenuBuilder` swaps the models onto its greybox pieces and keeps the primitives as a fallback when the FBX is missing. The menu UI and camera are unchanged.

**Reason:** The user asked for the menu city to follow the kitchen's 3D toon style (chunky models, toon ramp, soft ink), matching their mockup: a skyline under stars, warm windows, and glowing city lights in the lower corners that show around the UI panels.

## 2026-11-10 — Claude drives the open Editor through the Unity CLI, within limits

**Decision:** The project has the `com.unity.pipeline` package (0.10.0-exp.1), so Claude Code can drive the user's open Editor with the `unity` CLI. Its uses are running `Tools/Game/...` builders, reading the console and compile errors, entering Play mode, taking Game view screenshots and inspecting the live scene. Rules:
- Only one session uses it at a time, normally the orchestrator (Main build).
- It never hand-edits scene or prefab objects. Builder scripts stay the source of truth, because the next rebuild would wipe live edits.
- Claude asks before entering Play mode or forcing a recompile, since that interrupts the user's own play.

**Reason:** It removes most of the "close Unity / run this menu item / send me a screenshot" round trips. Tests run inside the open Editor too: `unity command test run --mode editor` ran all 730 EditMode tests in about 12 s on 2026-11-10, with no need to close Unity. (`unity test`, the batch-mode command, still refuses a project that's open; use it only when the Editor is closed.) PlayMode tests enter Play mode, so Claude asks before running them.

**Cost:** The package is experimental. If it breaks the Editor or the build, remove the line from `Packages/manifest.json`.

## 2026-11-10 — The office camera pulls back on the terrace

**Decision:** While any player is on the office terrace, `OfficeCamera` holds a still shot of the whole terrace and 20 m of ground past its rail, over the near rooftops (about 26–30 m up at 55°, instead of 10–14 m following a chef). Players walking around the terrace don't move it; anyone still inside is kept in the shot, up to the usual 55 m. A 0.5 m band at the terrace's edge keeps the shot as it was, so standing in the doorway doesn't flip it. The move out uses the kitchen's 0.45 s smooth time and the move back in its 0.7 s (decision 2026-11-08), one arc each that starts at once; once closed in, the usual 0.5 s follow takes over. The tilt stays at 55°; a lower tilt (say 45°) for a wider view of the skyline is the first thing to try if the view feels cramped. The maths is `TerraceFraming` (simulation), tested in `TerraceFramingTests`.

**Reason:** The user asked for the camera to pull back on the terrace so players can see and enjoy the detailed rooftops, and close in again inside. The office scene's follow camera stopped at the building's edge, the rail, so the roofs never showed.

## 2026-11-10 — The terrace's pigeons fly off when a chef comes close

**Decision:** The nine pigeons on the office terrace (six on the rail, three on the deck and helipad) fly off when a chef comes within 2.5 m. Each waits a random 0–0.2 s first, and any pigeon within 3 m of a startled one goes too, so they leave as a little flock. A flight is one 2.5 s arc, 25 m across the ground, peaking about 5.5 m up and ending 8 m below the perch, shrinking away over its last fifth. Flights lean out over the rail, so a bird never flies into the office. The logic is `TerraceBirds` (simulation), tested in `TerraceBirdsTests`; `TerraceBirdsView` moves the birds, stops their pecking and flaps their wings in flight. Every value is tunable on the terrace's `TerraceBirdsView`.

**Proposed with it (flagged for review):** the pigeons come back. After 20–40 s each glides back to its own perch along the same arc in 2.5 s, slowing to land, but only once no chef is within 4 m of the perch (wider than the startle distance, so it doesn't land and flee at once). A chef stepping up to a landing bird sends it off again. Setting the shortest time away below zero keeps them away for the rest of the visit.

**Reason:** The user asked for the terrace's birds to react to players, as part of making the terrace a place to enjoy the city.

## 2026-11-10 — The money card: the campaign's balance top right, at all times

**Decision:** A money card in the top-right corner shows the campaign's balance everywhere players are: the office floor and every kitchen, through Plan, Prep and Service. It mirrors the office's player cards on the right: 108 px tall (a player card's height), at least as wide as one, 24 px from the edges, the same 18 px corners and 4 px outline (gold here, the card's colour), a gold coin and the amount in the display face (Fredoka) at 44 px, cream. The user's calls:
- **Money in:** the number rolls up to the new figure in green (0.6 s, easing out so the last digits land readably), holds green 0.8 s and fades back to cream over 0.4 s.
- **Money out:** the new figure at once, red, with a gentle shake (3 wiggles, at most 6 px, dying away over 0.4 s), then fades back over 0.4 s. Changes of a few cents don't flash the card.
- **Below zero (owed to the boss):** a minus sign and the amount in red, e.g. `−$120` (`−120 €` in Spanish), through `Locale`.
- **During service it counts each payment as it's made:** the card shows the balance plus this service's takings (payments and tips less walk-outs), although the books bank them as Review starts. Wages, energy and the boss's collection come off as Review starts.
- **The old money beside the clock is gone** (in Prep the balance, in service the takings, with +/− pops and a red flinch on purchases). The card replaces all of it, and the clock is centred.

It hides (fading over the HUD's 0.12 s) behind the Review dashboard, the pause menu and the city miniature's panel, and stays off on the street menu and through the opening until the camera lands. A change made while it's hidden plays when it comes back, so a purchase on the miniature still shakes the card as the panel closes. Every timing, the size and the colours are in `HudStyle` (Money card). The animation is `MoneyTicker` and the in-service sum `KitchenMoneySource.Live`, plain C# tested in `MoneyTickerTests`. The office reads the balance on `OfficeCampaign.BalanceChanged` (new: a purchase or a loan), not every frame. `Tools/Game/Art/Capture Money Card` takes 1920×1080 stills.

**Reason:** The user asked for the money on screen at all times, in the game's style, with green numbers rolling up and a gentle red shake going down, so the balance is felt as it moves without being alarming.

**Not covered:** world-anchored labels (tickets, shop lists) aren't told to avoid the corner, so a station near the top-right edge can put one over the card. In the office, four players all dressing at once at a 4:3 screen or narrower would bring the fourth dressing card close to it.

## 2026-11-10 — The day card: day, phase and clock, bottom left in the kitchens

**Decision:** In every kitchen (not the office), a day card sits in the bottom-left corner, 24 px from the edges: "Day 3" on top in the display face (32 px), and under it the phase and the game clock in the body face (28 px), "Service · 18:42". In Plan and Prep the clock gives the opening time ("Prep · opens 18:00"); near the end the phase word gives way to "Last orders · 20:50" or "Closed · 21:00" in the notice amber. It's the money card's quieter sibling (the user's calls): 88 px tall, the card a little see-through (78%), a thin 3 px muted sand outline instead of gold, softer cream text, and nothing moves but the minutes. **The top-centre clock is gone**; the card carries it. It hides with the money card behind the Review dashboard and the pause menu. `DayCardText` (plain C#, `DayCardTextTests`) writes it, `DayCardHud` draws it, and the look is tunable in `HudStyle` (Day card). The day is `DayCycle.Day`, which already counts from 1.

**Reason:** The user asked for a day and time indicator in the restaurant, with the money card's visual treatment but less present. The bottom left was free (the boss's phone is bottom right, the Prep strip bottom centre).

**Not covered:** world-anchored prompts and tickets aren't told to avoid the corner, so a station near the bottom-left edge can put one over the card.

## 2026-11-10 — forkcast is the morning's screen; hiring moves to its Staff tab

**User's calls:**
- **forkcast opens on arrival.** When a kitchen loads into Plan, the dashboard opens on the Overview. It also stays up from Review into the next morning instead of closing as the day changes. Review keeps its own behaviour: the close photo, the reset to Overview for a new day, and "Hold: start day N".
- **In Plan, B puts forkcast away** so the chefs can walk around. **Y, while walking, brings it back.** Holding Menu still readies up with it open or closed.
- **View stays the rearrange toggle,** one press, in Plan as in Prep. Pressed with forkcast up, it also puts forkcast away. (Asked because View already toggled rearranging; the user chose this over "View reopens forkcast".)
- **Hiring moves to the Staff tab,** for anyone, at any time in Plan. The D-pad picks a row, A hires the chosen candidate and Y lets the chosen team member go. In Review the tab is read-only. This replaces "Hiring stays on the Plan card for now" from the shop-cards entry above, and the face-button hiring of 2026-11-04.
- **The old Plan card goes** ("Looking for work: [A] … [X] … [Y] …", "Press Select…", "Hold Menu when ready"), along with `HiringMenu`. The lost-controller pause card stays.

**Done:**
- `ForkcastScreen` (Simulation, on `KitchenServices.Forkcast`) holds the open/closed rules: open for Plan and Review as they start, B and Y only in Plan. The chef waits while it's open (`PlayerInteractor.IsReviewing`), and the money card, day card and centre card hide behind it.
- `StaffBoard` (Simulation) holds the Staff tab's rules. The list is the team first, then today's candidates; anyone hired today leaves the candidates. A refusal gives a reason ("job taken", "out of reach") shown in red under the list, and in Review it says "Hire in the morning, before prep."
- Staff tab: "Your team" cards (wage, skill and speed bars, mood, today's work, wages a day), then "Looking for work today" cards with the candidate's own words and wage, greyed and with the reason when they're out of reach. The chosen row has a gold edge.
- forkcast in Plan: a "Plan · opens 18:00" pill in the header, since the day card is behind it. Its hint bar adds B Close, View Rearrange and "Hold: start prep", plus A Join when nobody's here. With no close photo yet, it shows the kitchen as you arrive ("Your kitchen this morning"); the menu's slot photo still comes only from a close.
- **The Prep strip's hints are drawn buttons,** not "[Select]" and "[Menu]" text (`HintStrip`). Plan gets the same strip when forkcast is put away: Y forkcast, View Rearrange, Menu "Hold: start prep". Who's ready and the morning's delivery sit just above it.

**Side effects to know:**
- **The Cookbook tab works in Plan too** (Player 1). Nothing saves mid-day, so a morning unlock or price change is kept with the rest of the day as Review starts, and quitting before then undoes it, as with hiring.
- **Bringing forkcast back with Y takes every player out of rearranging** (anything carried goes back), because forkcast covers the whole screen.

**Capture:** `ForkcastPlanCapture` (explicit PlayMode, visible editor) shoots the Staff tab and Overview in Plan at 1920×1080.

**Not checked in play:** whether players find Y to reopen forkcast, how the shared dashboard feels when one player puts it away mid-browse, and the Staff tab with four players at once.

## 2026-11-10 — forkcast after the playtest: a Start button, tonight's receipt, a boss card with urgency

**User's calls** (from playing the morning and Review):
- **A big Start button replaces holding Menu,** bottom right in forkcast: "Start day N" in Review, "Start prep" in Plan. Holding A fills it like a progress bar, and with two or more cooks each has a pip on it, lit when they're ready. Holding Menu now readies up only in Prep; in Plan and Review a tap on Menu opens the pause menu at once.
- **The A clash, solved the way the user suggested first:** a tap of A acts on what's chosen (hire a candidate, unlock a recipe), on release; only a hold (0.8 s, `_startHoldSeconds`) fills Start. The reply wheel's A still acts on the press, and that press never fills Start.
- **The left column's "Tonight's guests" chart goes** for a receipt of tonight's money: meals, tips and anything else in; ingredients, wages, energy, purchases and walk-outs out; the profit or loss in big type. The boss's collection and loan instalment stay off it (the boss card has them). In the morning it's last night's. The left column is shared by every tab, so the receipt is on all of them.
- **The boss card leads with the collection:** "Collection in 3 days: $610" ("tonight" or "tomorrow" when it's next). Under it, a bar of how much the balance covers, red when short, with "You have $X" and "Short by $Y". A last line says what missing it brings, from `BossEscalation`'s next step ("Miss it and he comes to warn you."). On collection night it says what he took and what comes tomorrow. Short with two days or fewer left, the card's glow pulses gently and the boss appears, frowning.
- **The footer lists only LB/RB (tabs) and the D-pad (browse).** Every other action is on the UI it acts on: Y Reply on the chosen review (as before), A Hire and Y Let go on the chosen Staff row, A Unlock and X Suggested in the Cookbook, the Start button, and in the morning B Close and View Rearrange as chips beside Start. The old left-hand "P1 ready" line is gone; the pips replace it.
- **Whole amounts throughout forkcast,** as on the money card ($557, not $556.56), Menu & Books included.

**Done:**
- Plain logic with EditMode tests: `StartHold` (tap vs hold per player, built on `MenuPress`), `BossCard` (due, covered, shortfall, urgent, the next step), `NightReceipt`.
- PlayMode: holding A on the Overview starts Prep with one cook and hires nobody; holding Menu in Plan no longer starts the day.
- The boss's phone call rises above forkcast's footer while forkcast is open, so it never covers Start.
- Captures (1920×1080): the Overview in Plan (day 1, and the morning of a short collection) and in Review (urgent), plus the Staff tab.

**Not checked in play:** whether 0.8 s feels right for the hold, whether a tap-on-release hire feels late, the pulse's strength, and four pips on the button.

## 2026-11-10 — forkcast's header shows the money, not the phase

**User's call:** the header's top right shows the balance as a chip (coin and amount) at the very right, animated as the money card is everywhere else, so an unlocked recipe or a hire shows its cost there. The phase and opening time ("Plan · opens 18:00") go from the header: the day card carries them on screen through Prep and Service.

**Done:** `ReviewDashboard` runs its own `MoneyTicker` (the money card's rules: money in rolls up in green, money out drops at once with a short red shake, owed money in red) for a chip right-aligned at the header's edge. It's sized for where a roll ends, and the Day pill and new-review pill sit to its left. A change made while forkcast is closed plays when it opens again, as the money card does.

## 2026-11-11 — forkcast is the end-of-day summary; one "Start day N" goes straight to Prep

**User's calls** (from playing the 2026-11-10 build):
- **One screen between days.** forkcast is the summary at the end of the day: it opens as Review starts (when the game saves), and arriving from the office opens it too. It has one action: holding A on **"Start day N"**, which goes straight into that day's Prep. There's no Close, no Rearrange and no reopening; the dashboard isn't seen again until the next day's summary. N is tomorrow after a service, and the day about to be played on arrival; a brand-new restaurant shows the same screen with "Start day 1" ("No reviews yet").
- **Hiring stays on the Staff tab here,** in Review as well as on arrival. In Review a hire or a goodbye saves at once, as replies and the cookbook do.
- **The boss card follows the user's simpler frame:** "THE BOSS"; "Debt collection in 3 days: $450" (or tonight / tomorrow); an orange bar as far as the balance covers it; "You have $X" on the left, "Short by $Y" in orange on the right. The loan line and "Miss it and…" go. Short with two days or fewer left, it still pulses and the boss appears, frowning; the line shrinks to fit beside him.
- **The left column is a listing card after a Google Places card,** kept calm (the user found the first version too busy): the name, stars and count, price band and category; then four tiles, each with a big value over a small label: **known for** (the most ordered dish; the menu's first before any orders), **per person** (the menu's price range, rounded to fives), **guests fed** (all served so far), **closed · opens** (the next opening). Under them, one quote: the best line from the latest reviews, or the owner's one-liner before there are any. "Last night's money" goes.
- **During Prep, players see how many guests to expect:** the Prep strip says "Prep · about 14 guests tonight". `GuestForecast` works it out from the service's own arrival rule (interval, rush, the menu's demand, until last orders) and the group size the tables bring, so it follows the tables as they're moved.

**Behind it:**
- `ForkcastScreen` has no close or reopen; `StepsToPrep` takes Review through the next morning without stopping (`DayLoop` advances twice).
- **Rearranging is Prep-only.** The boss's morning visit and his phone call now start as Prep starts, in plain view, and the doors can't open until he leaves.
- Tomorrow's candidates are drawn as Review starts, so the Staff tab shows the people the next day would.

**Plan is no longer a phase players stay in:** it's passed through between Review and Prep, and is only seen as the arrival screen from the office. `docs/game-design.md` §4 (the daily loop) is now out of date: its table gives Plan the menu and prices, suppliers, hiring and layout editing. Those now happen in forkcast between days (prices, hiring) and in Prep (layout, the street vendors). §4's line 226 ("Layout editing happens in Plan") and the drop-in line 39 ("during Plan/Prep") are out of date too. I haven't edited the design doc.

**Not checked in play:** whether "about N guests" matches what turns up often enough to trust, and the tiles with long dish names.

## 2026-11-11 — forkcast always describes the last finished day; "Books"; day 1 starts in Prep

**User's calls** (from leaving and rejoining mid-day, which showed the new day's empty books):
- **The summary is the day summary.** The game saves as it opens (Review starts). Leaving during the next day and joining again lands on the same summary, with no new save: the header ("Day 2 · Tue"), the Overview's stats, the boss card, the review cards and the money all describe the last finished day, and the button says "Start day N" for the day after it.
- **No summary before day 1:** a brand-new restaurant starts straight in day 1's Prep.
- **The money tab is one word: "Books"** (the user chose it over "Bookings", which reads as reservations). It shows the finished day: money in and out line by line and the net, what was ordered (per dish: how many, walk-outs, stars, the price it sold at), where the money went, and the trend.

**Done:**
- **The finished day keeps what the summary shows.** `DayRecord` (saved with the restaurant's history as Review starts) gains the ledger's lines, the night's dishes (`DishNight`), and whether the boss collected and how much of it was paid. All have defaults. An older save has no lines, and its Books fall back to the day's In/Out totals.
- **`DaySummary`** reads the last finished day; forkcast draws everything from it. The boss card counts the next collection from the end of that day ("tomorrow" is the next service).
- **`DayLoop.NewRestaurantsStartInPrep`** moves a restaurant with no finished day from Plan to Prep on its first frame. The play-test suite turns it off (`SaveIsolation`) so its tests can still drive a new kitchen's day by hand from Plan; `ForkcastSummaryPlayTests` turns it on and checks the real start.
- Tests: `DaySummaryTests` (EditMode: Review, the next morning, a JSON round trip, an older save, a collection night); `ForkcastSummaryPlayTests` (day 1 starts in Prep; finish days 1 and 2, leave during day 3's Prep, rejoin: the summary is day 2's, Books match day 2's ledger, Start says day 3).

**Not checked in play:** a real relaunch through the office with several restaurants, and an older save's fallback.

## 2026-11-11 — Starting the next day: a bar of player lanes, the A at the bottom left

**User's calls** (the old gold pill didn't read as a progress bar, nor show who had pressed): three options were mocked up (a sweep bar, a ring round a big A, player lanes). The user then placed it:
- **A bar at the foot of the left column,** under the restaurant tiles (the featured review goes). It has one lane per cook, each filling in their colour as they hold A, with their name (P1–P4) readable at any fill. An empty lane is someone still to hold. With one cook it's a single gold sweep with no names. Each lane's fill eases after the hold, and a white flash pops as each hold completes.
- **The A interaction on the very bottom left:** a big green A that pulses gently while nobody holds and sinks while someone does, then "Hold to start day N" and, with two or more cooks, "· Waiting for P2, P4". A cook whose controller is lost isn't waited for.
- **LB/RB (tabs) and the D-pad (browse) move to the footer's right.**

**Then, from the next look (same day):** the bar is a card like the restaurant tiles above it, holding one rounded pill per cook: pale in their colour when empty, solid when ready, and filling across as they hold, with no dark track. The A is bigger (62 px), with "Hold to start day N" in larger type. The footer's right reads word first, then the buttons: "Tabs [LB][RB]", "Browse [D-pad]".

**Tunable** (`ReviewDashboard`, "Start bar"): fill speed, flash time, pulse rate and size, and how far the A sinks. The hold itself is still `_startHoldSeconds`.

**Not checked in play:** whether the flash is noticed before the screen leaves on the last hold (it closes the moment everyone's ready), and the pulse's strength from the couch.

## 2026-11-11 — forkcast: the footer is the start bar; six restaurant cards; the boss card shows success

**From the user's solo playtest:**
- **"The books still show empty":** the days in that save were played before Books kept every line and the night's dishes (saved before 00:29 on 2026-11-11; the file's later timestamp came from an office save rewriting old day records). Nothing was being lost. For such a day Books now says "only the totals were kept" and "Not kept for this day" instead of "Nobody ordered." A new day keeps its lines and dishes through Review and a relaunch (tested), and a dish's price is what the guest paid.
- **Two pills for one player:** the bar counted a player whose controller had gone missing, whom the day loop doesn't wait for. forkcast now counts only joined players with a connected controller, as the day loop does (tested).
- **The photo caption** says "Your photo · captured at close" after a service and "Your kitchen, as you left it" on arrival (it was "this morning").

**User's calls:**
- **The footer is the start bar,** full width: the A and "Hold to start day N" sit on it, and it fills as A is held. With two or more cooks it splits into one segment per joined cook in their colour, each reading "P2 waiting" or "P2 ready" at its right end (rather than a "Waiting for…" line, which ran into the segments). With one cook it's a single gold bar. The footer's control hints go: the tab bar shows LB/RB, and lists browse with the D-pad as everywhere.
- **The left column:** the pills card goes. Under the name and rating are six cards in the tile style (the user's pick from nine): known for, per person, guests fed (with "· 1 fled" when someone walked out that night), closed · opens, best plate tonight (by the guests' stars), and tips tonight ("· they liked you").
- **The boss card shows success:** green when the balance covers the collection ("Covered · $360 to spare"), amber when only just, under 10% to spare ("Just covers it · $18 to spare"), and orange with "Short by $X" when short; a paid collection night is green ("He's happy. For now."). The card pulses only when short with two days or fewer left. There's no boss avatar (the user's call). `BossCard.Stands` holds the rule (EditMode tests).

**Not checked in play:** the segment tags with long translations, and whether amber reads as "fine, but careful".

## 2026-11-11 — forkcast after the user's design: four cards, a green start button, a footer of player segments

**User's design and answers:**
- **Left column:** under the name and rating, four cards: total earnings tonight (money in: meals and tips, not profit), guests fed (· 1 fled), tips tonight, and Closed · Opens. "Known for", "per person" and "best plate tonight" go.
- **The start button** is a standalone, round-ended green pill (the Xbox A's green, white edge, the A as a white ring, "Hold to start day N" in white), centred in the gap between the cards and the footer. It animates as a unit: a gentle pulse at rest, sinking while anyone holds A (`_buttonPulse`, `_buttonPressed`, `_buttonPulsePerSecond`).
- **The footer** is a strip flush with the panel's bottom edge, with one segment per joined cook in their colour, even alone. A waiting segment is pale, reading "Waiting for Player N". Holding A fills that cook's own segment; once ready it's solid, reading "(B) Player N READY".
- **B takes a ready back** (`ReadyCheck.Set`). A second hold of A no longer toggles.

**Tested:** `ReadyCheck.Set` (EditMode). Holding A readies and B takes it back, with two pads (PlayMode).

**Not checked in play:** whether the pulse and sink read well from the couch, and the footer's tags in Spanish.

## 2026-11-11 — forkcast polish: key cards, tips caption, coin, avatars, one glyph kit, ready pop

**User's calls:**
- **The four cards** are now: today total earnings (money in), tips tonight, Most ordered meal (the finished night's, counted by dish across donenesses; the all-time favourite for a day saved without dishes), and Closed · Opens. "Guests fed" goes. `DaySummary.MostOrdered` holds the rule (EditMode).
- **The tips caption follows how generous guests were** for what the meals took: none "not a penny", under 5% "polite, at best", 5–15% "they liked you", over 15% "they loved you". `DaySummary.TipsMood` (EditMode).
- **The boss card** shows the money chip's coin before "You have $X" (not on a collection night, where the line isn't the balance).
- **The start button** reads "Hold to start Day N". Its A is the kit's badge, the same glyph as the footer's B and the Reply Y, only bigger; badge rings now scale with their size (4 px at 31 px).
- **The footer** centres "Waiting for Player N" and "(B) Player N READY" in each segment. The B is the red badge. As a player becomes ready, their label pops gently (18%, 0.45 s, `_readyPop` / `_readyPopSeconds`).
- **Reviews** show each guest's face in a perfect circle instead of the oval (`UiKit.GuestAvatar`: their colour, a skin tone, hair and eyes from their name, the same each visit). Customers share one model, so it's a drawn stand-in rather than a portrait. The quote is bigger (32 px in the list, shrinking only for a line too long).

**Not checked in play:** the pop's feel, and the avatars next to real guest names in Spanish.

## 2026-11-11 — A save records the last finished day; the map card shows a restaurant's day

**The bug (user's report):** "technically this restaurant is on Day 3, however once I hold to start day 4 it lands on day 5." A reply on the arrival summary saved the campaign with the live day (already the next, unplayed day 4), so the next load moved it to day 5, while the summary still said day 3.

**Fixed in one place:** a save always records `DayCycle.LastFinishedDay`: today in Review, otherwise the day before. Anything saved on the arrival summary (replies, the cookbook, hires) is kept with that day, and the day can't run ahead. **Existing saves heal on load:** `RestaurantSave.FinishedDay` falls back to the last recorded day when the saved day is ahead of the history, as the user's slot is now. **"Hold to start Day N" uses the same number the day loop advances to** (`LastFinishedDay + 1`), so the button and where the hold lands can't disagree.

**The map card (user's call):** an owned restaurant shows its day as its last summary did ("Day 3 · Wed"), or "Not opened yet" if never played, and A reads "Open for day 4".

**Tests:** EditMode covers the last finished day in each phase, healing (ahead, equal, without history) and the user's case end to end. PlayMode finishes day 1, relaunches, replies, relaunches, and checks day 2's morning, a day-1 summary and "Start day 2". `SavePlayTests` now finishes the day before saving, as the game does. Its old mid-morning save counted an unplayed day.

**Not checked:** the map card in a capture or in play.

## 2026-11-11 — Two flaky customer play tests: a guest shoved round the table, and a walk longer than its wait

**Why they failed now and then:** each customer's look sets their walking pace (0.7 to 1.25 of the speed), picked with `UnityEngine.Random`, whose state depends on whatever ran before. So the same test walked at different speeds in different runs and suite orders. Batch runs also go at about 1000 fps. Waits were already in game seconds (`Time.time`), so frame rate and `timeScale` weren't the cause. Neither was a NavMesh: customers don't use one.

- **`AGroupSitsAtATableForFourEatsAndLeavesItsPlates` exposed a game bug.** Guests planned their way avoiding every chair. Round a table for four by the front wall that leaves no gap: the south chair nearly touches the wall, and the north chair nearly touches the bar stools. So the planner fell back to the furniture-only path behind the south chair. A walker who started before their friend sat down also kept that path. They then pushed the seated friend around the table for 5–10 s, and with slow paces that ran past the test's 20 s. **Fixed in `CustomerDirector`:** ways avoid only the chairs someone is sitting in, an empty chair being no obstacle, and every walk is planned again when someone sits down or gets up. The group now sits in 4–7 s, each guest in their own chair, and the test now checks that too. Still open: with all four seats taken first, the last guest's only way may still be behind a friend (see `docs/ideas.md`).
- **`ADishwasherClearsABarTableFromTheKitchenSide` was a test bug.** Its guest walked up the street, about 27 m, which takes up to 24 s at the slowest pace, inside a 20 s wait. The test is about the dishwasher, so like the other dining-room tests its guest now arrives at the door at once (`AtTheDoorAtOnce`). The street walk has its own tests.

**Rule for these tests:** a timeout is in game seconds and has to cover the slowest look (0.7×), not the average one.

## 2026-11-11 — The Prep HUD: hold Y, tiles bottom left, player cards top left, the shop as a picture grid

**User's calls, from their wireframe and two rounds of mockups:**
- **Y starts the day, everywhere.** On the forkcast dashboard the Start button is yellow and filled by holding Y. A tap of Y still replies to a review or lets a staff member go; it fires on release, so a hold can't also open the reply wheel. A hires and unlocks on the press. In Prep, holding Y readies up, and holding it again takes the ready back. Menu only ever pauses. The user accepted that Y does both jobs in the kitchen: a press still takes the fourth slot's item at storage or a stall, and holding on fills the ready bar.
- **Bottom left in Prep: two tiles (option C, icons option 2).** A tear-off calendar page ("FRI 5") with "Prep / Opens 18:00", and two of the customer portraits with "About 5 guests / Groups of one". The group words follow the tables' average group (`DayCardText.Groups`). Halfway between two sizes names both ("Groups of one or two"). Service keeps the plain day card; signs for closing time and late shifts come later.
- **Along the bottom in Prep: the dashboard's ready bar** (`ReadyLanes`, now shared by both). Each cook's segment reads "Y Hold to start service" instead of "Waiting for Player N", and fills in their colour as they hold. Ready reads "Y Player N READY".
- **Top left in Prep: the office's compact player cards, joined players only** (no open seats). In Service the space is kept for orders, later.
- **The shop card grows from its player's card** and is a grid of baked pictures (Tools/Game/Bake Shop Pictures), three to a row with prices. Under it are the chosen piece's name, price and what it's for, in two lines at most (`ShopDescriptions`, written from what the game does). The D-pad moves left and right a piece and up and down a row (`ShopGrid`).

**Dropped:** the old Prep strip (its "Rearrange" and "Hold: open up" hints and the "P1 not ready" line). Nothing on screen names the View button for rearranging now.

**Not checked in play:** how holding Y feels, at a storage slot especially; the pictures' size in the tiles; and the faces in the cards for real chefs.

## 2026-11-11 — The wheelie bin moves, appliances stay inside the walls, and kitchens and dining rooms can mix

**User's calls, after the bin got its model (SM_WheelieBin):**
- **The bin moves in rearranging, along the building's outer walls.** It always stands flush against an outside wall and doesn't follow the cursor at once: it rolls after it on its wheels, a little behind.
- **A bin near the customer door costs a little, by distance.** Within a few metres of the door, guests lose some satisfaction, more the closer it is, and some reviews mention the smell. Learnable in play, never a disaster.
- **Appliances stay on the premises.** While being moved they never poke into or through a wall.
- **Appliances may go in the dining room, and tables in the kitchen.** Placing one shows a warning that it makes mess. It always makes more mess. Guests' reaction depends on how good the restaurant is: with great food and service it's a show they love (a chef's-table bonus and glowing reviews, Michelin style); with average or poor service, they complain about the mess and noise.

## 2026-11-11 — At the tailor, A wears; the player number is bigger and centred

**User's calls:** the dressing card shows both A and B, as the kitchen's shop card does, and the compact player cards' number is bigger (46 px) and centred in the column right of the face, in the office and the kitchen.

**What A does (my proposal, from "both A and B should appear"):** browsing only previews; **A Wear** puts on the item shown and saves it; **B Done** hops down, and anything only browsed goes back. A dims when there's nothing to wear (already worn, or locked); a worn item reads "3 / 6 · wearing". Wearing flashes the card's edge, and A on a locked item shakes it, as the shop's buy and refusal do. Until now every item browsed past was worn and saved at once; that's gone.

**Not checked in play:** whether players expect browsing to keep what they last saw. If they do, B could wear the shown item instead of dropping it.

**Superseded (2026-11-11 playtest round):** the user found A to wear unnecessary; the tailor saves as you go again (the entry "The office after the playtest" below).

## 2026-11-11 — Prep: only the shop is see-through, D-pad down opens it, Rearrange under the money

**User's calls:**
- **Only the shop card is see-through** (`HudStyle._shopCardOpacity`, 0.6, with an outline ring so the player's colour doesn't tint the fill). The tiles and the player cards stay as they were.
- **"Rearrange" docks top right under the money card**, as wide as it, instead of under the bottom-left tiles.
- **A player rearranging the kitchen gets a tab under their card:** the D-pad with its down arm lit, a little grid for the shop, "Shop" and a down-arrow that bobs. **D-pad down opens the shop**, not Y; B closes it. Y in rearranging mode does nothing now (holding it doesn't ready up either, as before). The cursor's own hint line no longer names the shop.

**Not checked in play:** whether 0.6 is readable over a busy kitchen; it's one number in the HUD style.

## 2026-11-11 — The shop card's fill; B cancels a ready in Prep

- **The shop card is a dark shade of the player's colour** (18% of it in the card background), at 0.9 opacity: like the office's cards, with the kitchen faintly through (`HudStyle._shopCardTint`, `_shopCardOpacity`).
- **In Prep, B takes a ready back**, as on the dashboard; a ready lane reads "B Player N READY". Holding Y only readies now (it no longer toggles). B at a station also does the station's B, as a quick Y does.

---

## 2026-11-11 — Rearranging: pieces stop at the walls, and the open kitchen's warning

**Why pieces showed inside walls:** `KitchenPlan.Check` already refused a spot where any corner reached past a wall's face, but the carried piece was still drawn wherever the cursor was, and the cursor only stays inside the building's outer walls. So a piece pushed against a wall, or a turned piece near one, showed half inside it (red, refused), and one carried from the kitchen to the dining room passed through the dividing wall. Square pieces whose centre was in the area never clipped; turned ones near a wall did too, as the wall snap only worked square on.

**Fix (Simulation):** `KitchenPlan.Snap` now keeps the landing inside the room, by how far the piece's corners reach along each axis at its turn. Pushed further, a piece stays flush against the wall and slides along it, at any of the 8 turns and any size.

**Tables' chairs while carried:** a carried table now shows only the chairs that would have room where it would land (`TableStation.PreviewChairs`), as placing already did. Before, a bar table turned towards a wall showed its stool half a metre inside it.

**Checked against the real models** (`OpenKitchenPlayTests`): every piece in the grill kitchen, pushed into each wall at all 8 turns, stays out of the walls, with one known exception. Cabinets' worktops (CabinetArt) overhang their 1 m body by 2.5 cm, so against a wall the lip sits 2.5 cm into it, as it already did in the builder's own layout. That's hidden inside the 20 cm wall, so the test allows 3 cm. A stool can stand in the kitchen door's gap, which isn't a wall.

**User's calls on the open kitchen (this session's questions):**
- **The warning shows while carrying**, as an amber line on the carried piece's label once it's over the other room ("cooking in the dining room makes mess" / "guests in the kitchen make mess"), before A. Hovering a placed piece that's out of its room repeats it.
- **Placing still takes one press.** The warning informs, never blocks.
- **The dividing wall is jumped at its middle:** a carried piece sits flush against it on the cursor's side and moves to the other room once the cursor passes the wall's middle. Nothing may straddle it.

**Rules now:** the plan has two rooms (`Room.Kitchen`, `Room.DiningRoom`); any piece may stand in either. Each piece has a usual room (tables `DiningRoom`, the rest `Kitchen`). Doors, other pieces, people and chairs still block as before. The street door keeps its own rule. `KitchenPlan.OutOfTheirRooms()` and `LayoutEditor.OutOfTheirRooms()` list the pieces out of their usual room, for the mess and guests' reaction (built in another session).

**Not checked in play:** whether guests and the waiter reach a table in the kitchen comfortably through the kitchen door, and how the jump over the dividing wall feels with a stick.

## 2026-11-11 — Guests feel the bin and the open kitchen

**User's calls, building on "The wheelie bin moves…" above:**
- **The bin's smell reaches 4 m from the customer door** and costs up to **1 star** with the bin right at the door, fading in a straight line to nothing at 4 m (`Surroundings.BinSmell`). Reviews mention it when it costs at least half a star (bin within about 2 m). Today's layouts keep the bin about 5.5 m from the door, so they're untouched. Distance is from the door to the bin's nearest side, read each time a guest is served, so a moved door or bin counts at once.
- **The bin has flies over it** (`BinFlies`, added by the street builder), so players can tell why guests by the door grumble.
- **A great night is tonight's average stars ≥ 4.5, this guest's own dish included, and this guest not kept waiting** (patience at serving above the slow-service mark). Stars are counted before the bin and the open kitchen, so a show doesn't feed itself and a complaint doesn't spiral.
- **An open kitchen moves a guest's score up to ±1 star:** half a star per appliance in the dining room or table in the kitchen, capped at one. On a great night it's a bonus and praise-worthy guests write chef's-table reviews (`ChefsTable`); otherwise it's a penalty and guests complain about mess and noise (`OpenKitchenMess`).
- **An open kitchen always makes more mess:** every 3 guests served, each out-of-place piece leaves a wipe of floor dirt beside it (grease by an appliance, footprints by a table), scaled by the hygiene dial.

**Review priority:** food problems, then dirt, then the bin, then open-kitchen complaints, then a slow wait; a praise-worthy meal during a show becomes a chef's-table review. `ChefsTable` counts as praise for replies, sharing and the summary.

**All values are in TuningConfig** ("Guests' surroundings", plus two in "Dirt").

**Which pieces count** comes from the layout (`LayoutEditor.OutOfTheirRooms()`), asked as each guest is served. A table is a table in the kitchen; anything else is an appliance in the dining room. Each piece's extra mess lands 0.35 m in front of its front edge, where people stand to use it.

---

## 2026-11-11 — How the wheelie bin rolls

**Built from the user's call (the bin moves in rearranging, along the outer walls, rolling after the cursor):**
- **Where:** against the west, front (south) and east outer walls, its whole length against one, with its back 0.15 m off the wall and its front facing out. Not the north wall: the grounds end 0.9 m behind the building, less than the bin's depth.
- **How it moves:** the cursor reaches out 1.8 m past the inside walls (not behind the building). A over the bin picks it up. It goes to the nearest spot against a wall and *rolls* there along `BinTrack`, about 0.3 s behind (`WheelieBin._rollLag`, top speed 7 m/s), with its wheels turning. Between walls it rolls round the front corners on a 1 m quarter circle, turning as it goes, never through the building. B rolls it back home. It can't be turned with LB/RB.
- **Where it can't stand:** in the street door's doorway (the door's width plus 0.5 m either side), on the guests' way from the doorstep straight south to the pavement (so not between a side-wall door and the street), or on anyone. The HUD says "keep the door clear". Moving the street door so the bin would then block it is refused the same way. Next to the doorway is allowed: guests smell it (the guest-reactions rules).
- **The queue outside** lines up along the door's wall on the side away from the bin when the bin stands that way (`CustomerDirector.WallLeft` stops at it).
- **The restaurant's sign** moved out from the front-east corner (1.5 m east, 1.7 m south) so the bin clears its pole as it rounds the corner.

**Not yet:** where the bin stands isn't saved, the same as the street door (neither is a station). A load puts both back where the builder put them.

---

## 2026-11-11 — Playtest round: controls, cleaning, HUD, day transitions, Restaurant tab, office

From a longer playtest. All the user's calls:
- **Movement:** turning gets its own speed, slower than walking, so a small stick push no longer swings the chef off the counter. When the chef stops near a station, a small assist turns them to face it.
- **Cleaning has one input:** washing, wiping, mopping and putting out fires all start by holding X. While X is held, the chef stays planted and the stick works the cleaning or turns them on the spot. Letting go of X frees them to walk. Washing becomes circles with the stick, counted in either direction and forgiving of rough shapes. X still flips food at a hob: what the chef holds or faces decides which.
- **The kitchen HUD shows only what's built into the scene:** debug panels go behind a developer toggle (off by default), guests' technical panels are removed, and per-counter control hints are gone. The only hints left are RT/LT (heat, browsing storage) and X (flip, clean), as a small button picture on the appliance that fades in when you're close and facing it. Action HUDs hide when the end-of-day dashboard opens.
- **A transition into and out of service, with a 4-second countdown before it opens.** Chefs move and work freely during the countdown. Guests wait at the door and walk in at zero.
- **The Cookbook tab becomes "Restaurant":** the menu plus opening times. Opening times set how long service lasts and how many guests come. Books is the third tab.
- **Dashboard navigation** moves between neighbouring cards along explicit paths, instead of jumping to whatever is nearest on screen.
- **The boss's hat** is smaller, so he doesn't hide the office.
- **The tailor saves as you go:** no A to wear. Every change applies at once, and B leaves. This replaces "At the tailor, A wears" from earlier today.

## 2026-11-11 — The office after the playtest: a smaller boss's hat, and the tailor saves as you go

The two office items from the playtest round above.

- **The boss's hat** (`OfficeBuilder.Boss`, which also dresses his kitchen visitor, `PF_BossVisitor`): the brim is about two thirds its old width (0.68 of the fit to his head) and the hat a fifth lower (0.8); the crown keeps more of its width (0.84) so his head and grey fringe never poke through it. It's shrunk about where it sits on his head, so it stays seated. Still a white cowboy hat with its walnut band, so he reads as the boss. *Tools/Game/Build Boss Visitors* rebuilds only his and the bodyguard's kitchen prefabs; *Tools/Game/Art/Capture Boss* renders him in the office and the kitchen.
- **The tailor saves as you go** (`TailorSession.StepItem`, `TailorPlinth`): every open item shown, in every section, is worn and saved at once, as before "A wears". A locked item still shows while browsing and isn't kept; B hops down and the chef goes back to the last open item. A does nothing while dressing. The dressing card loses "A Wear" and "· wearing", and shows only B Done (Xbox buttons only); their Spanish strings are gone from `es.csv`.

**Not checked in play:** how the smaller hat reads from the office camera with chefs about, and whether the boss still stands out on his kitchen visits.

## 2026-11-11 — Full screen: a Display row in Settings, Esc still pauses

The user asked for a way to play in full screen, switched from Settings. Their call, from the options offered:
- **A Display row in Settings: Full screen / Windowed** (`SettingsModel`, so the main menu and the pause menu both have it). It's saved in `settings.json` (`PlayerSettings.FullScreen`, on by default, so older files start full screen) and a built game starts the way it was left (`WindowModeKeys`). Full screen is Unity's borderless full-screen window, which suits macOS best. Windowed is three quarters of the display, and the window can now be resized (Player Settings → Resizable Window, on).
- **Esc keeps pausing.** It doesn't leave full screen: one key doing two things would throw players out of full screen whenever they paused mid-service. To leave full screen, open the pause menu and flip Display, which works the same on a controller.
- **Ctrl+Cmd+F**, the Mac's own shortcut, toggles full screen from anywhere, as a keyboard extra for testing. No hint names it (hints show Xbox buttons only).
- **In the Editor**, where Unity's full-screen calls do nothing, *Tools/Game/Play Full Screen* (also the Display row and Ctrl+Cmd+F, in Play mode) opens a frameless Game view covering the Editor's display (`FullScreenPlay`). Leaving Play mode closes it. It isn't saved, so playing in the Editor never changes how the built game starts. It relies on Unity's internal `GameView` class, so a Unity upgrade may need it updated.

**Fix the same day:** in play the HUD sat short of the right and bottom edges. Unity lays out the game for its "main" play mode view, which was still the docked Game view (1709×966 against the full-screen view's 1920×1050). While open, the full-screen view is now kept as the main one (`PlayModeView.s_LastFocused`, set again every Editor update), and it covers the display's usable area below the menu bar, placed from the main window's left and bottom edges (exact when the Editor is maximized).

**Not checked in play:** whether the Editor's full-screen view covers the display exactly (Retina scaling, the menu bar, a second monitor) and keeps controller input; a built game's switch both ways; and whether macOS also reacts to Ctrl+Cmd+F in a build, which would toggle twice.
