# The opening — from launch to the office

*Spec, 2026-11-09, from the user's outline and their answers. References: the skyline mockup (`10_menu_skyline.svg`), the main menu mockup (`Art/Direction/concepts/2026-11-08_main-menu_new-player.png`), `office-scene.md`. Steps 2–9 built in `Office_Greybox`, the boot scene (2026-11-09).*

The first half-minute takes players from the city's streets up the boss's tower and into his floor, in one unbroken camera move. The menu and the office become one place: the game happens in the glowing penthouse you see from the street.

## The steps

| # | Step | Status |
| --- | --- | --- |
| 1 | **Logos** (engine, production) | Open, to work out later (user, 2026-11-09). Unity 6 no longer requires its splash. Proposed: 2–3 s each, first launch only, any button skips afterwards. |
| 2 | **Save notice**: what the save icon means, and not to quit or turn off while it shows | Replaces the bad-language disclaimer idea (user, 2026-11-09). Whether there's a content note too is open. Proposed: one card, about 3 s, first launch only and then from Settings. The same icon shows as Review starts and whenever the office saves (`decisions.md`, 2026-11-06). |
| 3 | **Fade out, fade in** | Agreed. Short, about 0.5 s each, so it doesn't feel like loading. |
| 4 | **The fly-through**: the camera starts low in a busy street at night and rises through the city: clutter, movement, details, graffiti, people walking, birds, lit windows and people passing behind them | Agreed; starts in a street, not a park (user, 2026-11-09). 8–10 s. Uses the office's `Ambience` life (walkers, flocks, windows, silhouettes). **Skippable every time** with A, which jumps to step 5 (user, 2026-11-09). |
| 5 | **The stop**: the camera eases to rest with the boss's tower framed on the left, the sign hanging in front of its spire, and the menu fades in | Agreed. The framing of the main menu mockup. The lit penthouse is the teaser. |
| 6 | **The menu**: New campaign or Continue (Free kitchen fades to the kitchen) | Agreed. The menu's city needs reworking with this in mind (user, 2026-11-09): see *The city* below. |
| 7 | **The climb**: the menu fades out, the camera rises up the tower's glass and lands on the office's wide shot, looking at the elevator | Agreed. See *The hand-over* below. |
| 8 | **Joining**: a hint to press A; each join rings the elevator and a chef steps out | **Four join slots** (user, 2026-11-09). Whoever drove the menu is already player 1 on that controller, so they don't press A twice; the other three press A (user left it to us, 2026-11-09). |
| 9 | **Free play** on the floor | Proposed. Later, the guided first day (Phase 8) starts here: the boss's call or a first task. |

**Pacing:** first launch, about 25–30 s before control. Returning players reach the menu in a few seconds: logos and the save card show once, and A skips the fly-through, every time.

## The city

The fly-through, the menu's resting shot and the office have to be one city:
- **The same tower:** the boss's tower, with the penthouse floor as built in `Office_Greybox`, a lit band at the top and a spire, stands where the menu's camera can frame it.
- **A street to start in** (user, 2026-11-09), dense with clutter, movement and detail, built from the city kit and the office's `Ambience` life:
  - **clutter:** parked cars and scooters, bins and bin bags, newspaper boxes, fire hydrants, café tables, crates;
  - **movement:** people walking and waiting, a delivery rider, cars passing with their lights, pigeons, steam from a manhole;
  - **detail:** shop awnings and lit shopfronts, neon signs, fire escapes, string lights, posters;
  - **graffiti:** tags and murals on shutters and side walls, painted textures in the game's colours, never real-world logos or words that read as brands.
- **The office's city** (the near roofs and their life) and the menu's city become one layout seen from two heights, not two different cities.

## The hand-over (step 7)

**One scene** (user, 2026-11-09): the office scene holds the street, the menu and the floor, so there's nothing to load between them. The climb runs from wherever the camera is drifting at the stop, up the tower's glass, and turns onto the office camera's pre-join wide shot. The first thing players see is the whole floor and the elevator they'll come out of.

## How it's built (2026-11-09)

- **`OpeningSequence`** (Presentation/Office) runs the steps: save notice (first launch only, a `PlayerPrefs` flag; A skips it) → 0.5 s fade in → the flight, 9 s, eased (A skips, every time) → the menu fades in over a slow drift → the climb, 5 s → land. On landing the follow camera takes over, joining opens, and the menu's controller joins as player 1.
- **The camera path** is a Catmull-Rom curve through poses set in `OfficeBuilder.Opening.cs` (`OpeningPath`, tested). The flight: the street at eye height, rising out of it while looking up the tower into the sky, back over the shops' roofs into the open corridor, then the stop.
- **The haze** is thinner (90–360 m) in the street and over the menu, and eases back to the office's on the climb, so the tower reads from the street.
- **`OpeningHud`**: the black veil for fades, the save card (turning icon and two lines), "A Skip" bottom right, and the four join slots in player colours along the bottom.
- **`MainMenu`** starts hidden in the office (`_startHidden`). When the office listens to `Launching`, the menu hands over instead of loading the kitchen.
- **Captures:** *Tools/Game/Capture Office* renders `opening_flight_*` and `opening_climb_*` along the path.

- **Leaving for the kitchen:** `MiniatureTable` in the back room. Once every joined player stands around the table, any of them presses A and the kitchen loads. A hint above the join slots counts who's there ("Everyone to the table: 2 of 3"), then shows "A Start the day".
- **Boot order** (`BootOrder`, run by the office and menu builders): `Office_Greybox`, then `Kitchen_Greybox`. `MainMenu.unity` stays in the project for the menu's UI captures but isn't in builds.

**Still to do:**
- Players join again in the kitchen; carrying the office's players across, on the same controllers in the same order, is proposed.
- The join slots stay on screen on the floor; proposed: hide them a few seconds after the last join.
- Leaving cuts straight to the kitchen; a fade would be kinder.

## Open questions

1. **Logos:** which, and how long. Later.
2. **Content note:** is there one, beside the save notice? Is there bad language in reviews at all, and would a "clean reviews" setting suit?
3. **Loading state:** a loading moment may be needed after the save card if the office scene takes a while to load (user's note). The save icon could double as the loading indicator. With one scene there's no load between the menu and the floor.
