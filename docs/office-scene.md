# The boss's office — scene spec

*Greybox built 2026-11-09: `Tools/Game/Build Office` → `Assets/_Project/Scenes/Office_Greybox.unity` (`OfficeBuilder`). Draft 2026-11-09, from the user's lo-fi floor plan (`Art/Direction/concepts/2026-11-08_boss-floor_full-plan.png`, `…_boss-office.png`). Not yet reviewed by the Art Director: check with them before the art phase (user's call, 2026-11-09). Gameplay is Phase 6; this spec sets the space so that work has somewhere to land.*

Design references: `game-design.md` §15 (campaign, office, money trouble, ending), `art-bible.md` (palette, rendering, world & scenography). Decisions: `decisions.md`, 2026-11-09 "The boss's floor".

---

## 1. Vision and story

The office is the **penthouse floor of the boss's skyscraper**, in the same city the main menu flies over, built from its models (`SM_City`). It is the game's lobby: players join here, change their look, buy restaurants, borrow money, practise, and come back between days.

**Mood: cozy money with a hint of menace.** Inside it's warm: lamplight, polished wood, brass trim, the kitchen's palette dressed up for an oligarch. Outside it's dusk: the boss's tower is glass all the way down, over a warm brick-and-sandstone city a few storeys below that fades into a violet haze, and a windy terrace. Players should feel welcome, but never quite at home. The money is his.

**What the place should say without text:**
- The boss is rich and a little absurd: a helipad, a hall of trophies, a red carpet up to his door.
- Everything leads to him: the carpet, the bodyguards and the lighting all point at his door.
- The empire grows here: the city miniature fills with your restaurants, and the hall of fame fills with plinths.
- People lose here too: in the hall of fame, erased chefs leave dusty portraits, and their cookbook sits waiting for the next chef (§15.3). The wall starts empty: one portrait goes up for each campaign the boss erases (2026-11-10).

## 2. Layout

### The floor

A **3 × 3 grid of rooms** with a terrace along the back, matching the user's draft. The camera looks north, so the front row is nearest the camera.

```
          ┌──────────────── TERRACE (decking, planters, umbrella, helipad NE) ───────────────┐
  back    │  TAILOR              │  THE BOSS'S OFFICE       │  BACK ROOM (city miniature)     │
  middle  │  TEST KITCHEN        │  GUARDED DOORS           │  HALL OF FAME                   │
  front   │  WAITING LOUNGE      │  RECEPTION + ELEVATOR    │  ACCOUNTANT                     │
          └──────────────────────────── (façade drops away to the city) ─────────────────────┘
```

| Measure | Value | Why |
| --- | --- | --- |
| Rooms | Columns 8.5, 18 and 8.5 m wide; rows 9, 8 and 10 m deep (front to back) | The middle column (reception, guarded doors, the boss's office) is the grand one; the side rooms need less space (user's calls, 2026-11-09) |
| Floor | 35 m × 27 m, plus a 6 m terrace | Each room still fits the follow camera (up to 22 m high) |
| Doorways | 2 m gaps (3 m from reception to the guarded doors) | Two chefs pass without snagging; forgiving to walk through |
| Walls | Outer walls on the sides and back: 2.8 m glass on a panelled sill, walnut mullions (user's note, 2026-11-09). The front: a 1.2 m glass parapet. Inner walls: 1.0 m, walnut panelling with a brass cap | A penthouse walled in glass, with the city showing on every side. The front stays low (the art bible's dollhouse cut) because the camera looks over it, and inner walls stay low so the camera sees into every room |

**Connections (user's call, 2026-11-09):** the tailor and the miniature room open only from the boss's office, and so does the terrace (a door either side of his desk). The boss's office is reached only through the guarded doors. The other six rooms all connect: lounge, reception and accountant across the front; lounge to test kitchen and accountant to hall of fame; test kitchen and hall of fame to the guarded doors, which open from reception.

**Walking distances:** most trips start at the elevator (front centre). The miniature, the tailor and the boss are each two rooms away, about 18–22 m. At the chef's walking pace (4.2 m/s) that's about five seconds, which is acceptable on a between-days visit. If playtests show it drags, the first fix is wider doorways and diagonal shortcuts, not moving rooms.

### The camera

- **On arrival, and before anyone joins:** a wide establishing shot of the whole floor, as in the draft. Same rule as the kitchen: "before anyone joins, the whole restaurant shows" (decision 2026-11-07).
- **Once someone joins:** it follows the players at the kitchen's tilt, zooming out as they spread apart until both fit, up to 55 m (user's note, 2026-11-09). The office has no leash, so the kitchen's 100 px chef rule gives way when players split up; together, the chef stays large.
- **On the terrace:** while anyone's out there, it pulls back to a still shot of the whole terrace and the rooftops past the rail, and closes in again when they're all back inside (decision 2026-11-10).
- **Interiors aren't cut by the camera:** the 1.0 m walls handle it, so nothing needs to fade.

## 3. Rooms

Each room has one job and one hero prop that says that job from above.

| Room | Job (Phase 6 unless noted) | Hero prop | Interaction |
| --- | --- | --- | --- |
| **Reception + elevator** | Join; arrive back from a restaurant | **The elevator** (see below) and the reception desk | None: joining is a button press anywhere |
| **Tailor** | Change look; saves at once (§15.4) | Three-way mirror and colour plinths (the coloured discs in the draft) | Stand on a plinth: A = change look |
| **The boss's office** | Meet the boss; story beats | Desk with the boss behind it, pool of lamplight | Story beats (borrowing moved to the accountant, user's call 2026-11-10) |
| **Back room** | Buy restaurants; **start a day** in one (§15.2) | **City miniature** table, built from `SM_City` pieces at about 1/50 scale | A at the table = look at the miniature: the stick moves one shared highlight across the lots and a card shows the highlighted one. A on a lot for sale buys it (name, then style); A on one you own starts the day there, only when **every joined player** is at the table looking at it (user's call, 2026-11-09; buying: 2026-11-10) |
| **Accountant** | **Borrowing** (user's call, 2026-11-10), plus characters and objects to poke at, easter eggs (2026-11-09). The books stay on the Review dashboard | Desk, adding machine, filing cabinets | Stand at the visitors' chairs: A = sit down to the books; left and right pick an amount; A borrows (saved at once); B leaves |
| **Hall of fame** | The cookbook; owned restaurants; past chefs | Lectern holding the **cookbook**, plinths with each restaurant's trophy, portraits of erased chefs | A at the lectern = cookbook |
| **Test kitchen** | Practise any learned recipe (new, see below) | Minimal kitchen island | Normal kitchen controls |
| **Guarded doors** | Antechamber; the bodyguards' post; they eat and review the test kitchen's food | Two bodyguards flanking the boss's door, red carpet | Serve them a plate from the test kitchen (see below) |
| **Waiting lounge** | For fun: characters and objects to interact with, easter eggs (user's call, 2026-11-09) | Sofas, coffee table, a TV | Small one-off interactions, no menus |
| **Terrace** | Scenery; the ending (§15.4) | **Helipad** | None at first |

### The elevator (user's call, 2026-11-09)

One big elevator, not four. It replaces the four coloured lifts in the draft.

- **Joining:** each time a player presses a button to join, the elevator rings as if it has just reached the floor, the doors open, and that player's chef steps out into reception. Then the doors close.
- **Several joins:** if someone joins while the doors are still open, it rings again and the next chef steps out. No waiting.
- **Leaving is not the elevator's job.** A day starts at the city miniature, once every joined player is at the table looking at it (user's call, 2026-11-09). A player who's elsewhere holds it, and the hint says who's missing. *(Open: what the transition to the restaurant looks like, e.g. the camera diving into the miniature.)*
- **Coming back:** arriving from a restaurant is the same ring and doors, with every chef stepping out together.
- **Feedback:** a floor-indicator dial above the doors sweeps up as a chef arrives; the bell is the loudest sound on the floor, so a join never goes unnoticed.
- **Size:** 3.8 m wide inside, so four chefs fit (3–4 players is a stretch goal) and nobody blocks the doors.
- **Height:** the door wall is 2.5 m (doors 2.0 m) so the dial and lamp sit above the doors on the car's side, which is the side the camera sees. It hides about 1.75 m of reception just outside the doors, so a chef stepping out is briefly behind it. If that feels wrong in play, the fix is a lower wall with the dial on top.

### The test kitchen (user's call, 2026-11-09)

An office kitchen with **bare-minimum appliances** where players can try things out: searing, cooking, cutting.

- **Appliances:** one hob, one board, a small fridge, a bin and a counter or two. No sink or plate pile unless a learned recipe needs one.
- **Ingredients appear as recipes are learned.** The fridge stocks every ingredient of every recipe in the **cookbook**, so it grows as players buy and learn recipes across restaurants, and it outlives campaigns with the cookbook. It starts with the boss's dish (onion steak).
- **Free and endless:** nothing costs money, no spoilage, no clock, no customers, nothing saved.
- **Fits the earlier rule:** "a new ingredient and its mechanics are introduced when it's first bought" (2026-11-06) still holds. An ingredient only reaches the test kitchen after it's been bought and learned in a restaurant.
- **Same code as the kitchen:** the same station prefabs and cooking simulation, so it costs little and stays true to service.
- **The bodyguards are the tasters** (user's call, 2026-11-09). Food from the test kitchen is served to them; they eat it and review it, as customers do, but pay nothing. Their posts at the guarded doors are next door, which keeps the walk short.
  - *Proposed:* a serving hatch in the wall shared with the guarded doors. Put a plate on it and the nearer bodyguard takes it, eats and gives a review line, the same feedback a customer's review gives.
  - *Proposed:* these reviews are practice. They don't reach any restaurant's listing or reputation.
- **Not the free kitchen.** The free kitchen sandbox is a separate mode (`ideas.md`): a restaurant already bought, with real customers and phases and unlimited money.

## 4. Assets

**Reuse first (user's note, 2026-11-09).** The office is built from the kitchen's own pieces wherever they exist, so the two read as one game:
- **Floors and walls:** the kitchen's room shader (planks, tiles, panelled walls), in office colours.
- **Furniture:** copies of the kitchen's counters, hob, fridge, cutting board, knife rack, shelf, tables and bar tables with stools (visual only, their station scripts removed), and the dining chair and pictures.
- **People:** the chef's model, recoloured and without the toque.
- **City:** the menu city's blocks, cornices, window frames, sills, trees, lamp posts and water towers.

Only what neither has comes from the new `SM_Office` model, built by the same Blender script (`-- --only SM_Office.fbx`): a bevelled block for simple shapes, sofa, armchair, executive desk, office chair, filing cabinet, potted plant, floor lamp, trophy, cowboy hat, tailor's plinth, clothes rail and jacket, umbrella, lounger and helicopter.

*The list below was the first plan, kept for the art phase.*


All models come from `blender/build_pilot_models.py` (generated, reviewable as text), named per `CLAUDE.md`, chunky with bevelled edges (art bible "Modeling"), on the one palette texture.

**Hero props** (up to 1,500 tris each)
- `SM_Elevator`: doors that slide, floor dial, bell; frame in brass
- `SM_BossDesk`, `SM_BossChair`
- `SM_MiniatureTable`: the table only; the city on it is `SM_City` pieces placed by the scene builder
- `SM_ReceptionDesk`
- `SM_Lectern` (cookbook) and `SM_Cookbook`
- `SM_Helipad` (a decal-like slab with a painted H)

**Supporting props** (up to 600 tris each)
- Furniture: `SM_Sofa`, `SM_Armchair`, `SM_CoffeeTable`, `SM_OfficeDesk`, `SM_FilingCabinet`, `SM_Bookshelf`, `SM_Plinth`, `SM_Mirror`, `SM_ClothesRack`, `SM_TailorPlinth`, `SM_Portrait`
- Terrace: `SM_DeckChair`, `SM_Umbrella`, `SM_Planter` (conifer), `SM_GlassRailing`

**Filler props** (up to 300 tris each)
- Potted plants, a lamp, a phone, a TV, a typewriter or adding machine, books, trophies
- Reused from the kitchen: hob, board, fridge, bin and cabinet for the test kitchen

**Building kit** (up to 600 tris per module)
- Wall modules: 1.0 m capped wall, sill + glass wall, doorway, corner
- Floor tiles in three woods (office parquet, lounge carpet, terrace decking), cream test-kitchen tile, one checker in two close tones for reception
- Red carpet runner (a mesh strip, not a texture)

**Exterior**
- The top of the boss's tower (façade, cornice, ledge)
- A ring of neighbouring tower tops below the floor, with windows lit through the palette texture, so no void shows at any framing (art bible). All of it within the 60k-triangle exterior budget.

## 5. Scenography

**Focal point:** the boss's office. The carpet runs from the elevator straight up through the guarded doors to his desk, so the first view after the doors open is a line to him. His desk gets the warmest light on the floor.

**Secondary focal points:** the miniature (its own soft spot, the city glowing on it) and the elevator (the brass frame catches light).

**Colour (readability first):**
- **Floors are mid-dark and low-contrast:** the white chef must pop against every floor. The draft's black-and-white checker at reception would swallow a white chef from above, so it uses two close wood tones.
- **The red carpet is deep burgundy**, not the "Bad / B" red `#D9493B`, so feedback colours keep their meaning.
- **Player colours** (the draft's tailor plinths and markers) don't use two blues. Players 1 and 4 in the draft are both blue, which colourblind players can't tell apart.

**Lighting:**
- **Interior:** warm key `#FFD9A8`, as in the kitchen; brass and wood catch it.
- **Exterior:** the city is lit by the same toon light as the kitchen, in warm facade colours with a touch of the dusk violet in each. Linear fog (from 70 to 230 m, `Dusk` colour, also the camera's clear colour) fades it with distance, so the near roofs are detailed and the far city is quiet.
- **Lamps:** desk lamps, a reception pendant and the miniature's spot as the only extra real-time lights.
- **It's night here (user's note, 2026-11-09).** A low, cool moon replaces the sun, the city fades into a deep blue haze, and lamps do the work. Each room has its own light:
  - **Reception:** warm, welcoming pendants.
  - **Guarded doors:** dim and watchful, amber sconces at the columns and a cold light on the bodyguards.
  - **Boss's office:** a golden desk-lamp pool, amber floor lamps, a flickering fireplace.
  - **Tailor:** showroom spots on each plinth.
  - **Test kitchen:** cold fluorescent tubes, one failing.
  - **Miniature room:** dark, one spot on the table and the model city's blue glow.
  - **Hall of fame:** museum spots on the trophies, the cookbook and the portraits.
  - **Lounge:** cosy floor lamps and a television's flickering blue.
  - **Accountant:** a green banker's lamp under a stuttering tube.
  - **Terrace:** string lights along the rail and lit lamp posts.
- **Lamps light surfaces naturally,** not in toon bands (user's call). The shared toon shading takes point and spot lights only when a scene turns them on (`OfficeLighting`), so the kitchen is unchanged.

**Small stories:**
- A stack of unpaid invoices on the accountant's desk, with one restaurant's name on top (yours, when you're behind).
- The bodyguards' coffee cups on the lounge table.
- The tailor's mannequin wearing a chef's jacket a size too big.
- Erased chefs' portraits gathering dust in the hall of fame.
- The helicopter on the pad, rotor tied down. At the ending, it's gone.

**Life (user's note, 2026-11-09: looking at the surroundings should bring joy):**
- **Near roofs** each have one recognisable thing going on: a heliport with a helicopter warming up, roof gardens with twinkling pergola lights and a gardener, a yellow crane swinging its jib, a giant spinning steak sign, a pool with someone on a rubber ring, washing on a line.
- **People** smoke on their roof decks.
- **Birds:** pigeons wheel round the roofs and peck along the terrace rail, flying off when a chef comes close and coming back later (decision 2026-11-10); gulls circle high.
- **A helicopter** laps the skyline.
- **Windows** light up and go dark, and a few flicker with televisions.
- **Lights:** red aviation lights blink on the tallest towers.
- **The office's people** breathe, and the bodyguards glance about.

All of it is driven by the `Ambience` components, which are pure functions of time.

## 6. Technical notes

- **Building it:** the scene is built by an Editor menu item (`Tools/Game/Build Office Scene`), like `MainMenuBuilder`. No hand-edited `.unity` YAML.
- **Shading:** one toon shader, one palette texture, outlines from the pending `2026-11-08_outline-pass` handoff. Window glow is in the palette, not real lights.
- **Lights:** one directional moonlight plus up to 6 local lights, all with no shadows except the moonlight's.
- **Batching:** building kit and furniture are static and batch together. The miniature's city pieces are instanced.
- **Budgets:** interior about 40k triangles, exterior up to 60k. Target: a steady 60 fps on a MacBook with two players (`CLAUDE.md`).
- **Test kitchen:** reuses the kitchen's station prefabs and composition-root wiring with a "practice" setting (no money, no spoilage, no customers). This is the only gameplay system the office needs before Phase 6.
- **Overdraw:** the glass on the back wall and railing is the only transparency. Keep it to a few large quads.

## 7. Open questions

*Answered 2026-11-09: starting a day (everyone at the miniature), the lounge and accountant (fun and easter eggs), tasting (the bodyguards review), the free kitchen (stays, as a separate mode).*

1. **The trip to a restaurant:** what plays when everyone's at the miniature: a camera dive into the miniature, the elevator ride, or a plain fade?
2. **Easter eggs:** which characters and objects in the lounge and at the accountant. A list to fill in during Phase 6 or 7; the rooms should leave space for them.
3. **The bodyguards' reviews:** confirm they're practice only, never on a listing.
4. **Art Director review:** this spec turns the user's draft into dimensions and assets. The AD should check the colour notes and the asset list before the art phase.
