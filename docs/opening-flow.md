# The opening — from launch to the office

*Spec, 2026-11-09, from the user's outline and their answers. References: the skyline mockup (`10_menu_skyline.svg`), the main menu mockup (`Art/Direction/concepts/2026-11-08_main-menu_new-player.png`), `office-scene.md`. Steps 2–9 built in `Office_Greybox`, the boot scene (2026-11-09).*

The first half-minute takes players from the city's streets up the boss's tower and into his floor, in one unbroken camera move. The menu and the office become one place: the game happens in the glowing penthouse you see from the street.

## The steps

| # | Step | Status |
| --- | --- | --- |
| 1 | **Logos** (engine, production) | Open, to work out later (user, 2026-11-09). Unity 6 no longer requires its splash. Proposed: 2–3 s each, first launch only, any button skips afterwards. |
| 2 | **Save notice**: what the save icon means, and not to quit or turn off while it shows | Replaces the bad-language disclaimer idea (user, 2026-11-09). Whether there's a content note too is open. Proposed: one card, about 3 s, first launch only and then from Settings. The same icon shows as Review starts and whenever the office saves (`decisions.md`, 2026-11-06). |
| 3 | **Fade out, fade in** | Agreed. Short, about 0.5 s each, so it doesn't feel like loading. |
| 4 | **The approach**: at street level close to the tower, from the avenue's mouth, the camera walks in through the crowd to the tower's main entrance | User, 2026-11-10: no long run up the road. 8 s. **Skippable every time** with A, which jumps to step 5 (user, 2026-11-09). |
| 5 | **The stop**: right in front of the main entrance, with the boss's restaurant on the ground floor, its queue, its marquee and the flashes. After a 2 s beat on the bustle the menu fades in; A brings it at once | User, 2026-11-10. The menu covers most of the frame, so the beat is when the entrance is seen whole. |
| 6 | **The menu**: New campaign or Continue (Free kitchen fades to the kitchen) | Agreed. The menu's city needs reworking with this in mind (user, 2026-11-09): see *The city* below. |
| 7 | **In and up**: the menu fades out; player 1's chef walks in through the tower's door to the lift, which stands inside the lobby where the boss's elevator is upstairs; the camera watches, then rises up the glass beside the cab, past real floors, over the cornice and onto the office's wide shot, looking at the elevator | User, 2026-11-10. See *The hand-over* below. |
| 8 | **Joining**: a hint to press A; each join rings the elevator and a chef steps out | **Four join slots** (user, 2026-11-09). Whoever drove the menu is already player 1 on that controller, so they don't press A twice; the other three press A (user left it to us, 2026-11-09). |
| 9 | **Free play** on the floor | Proposed. Later, the guided first day (Phase 8) starts here: the boss's call or a first task. |

**Pacing:** first launch, about 25–30 s before control. Returning players reach the menu in a few seconds: logos and the save card show once, and A skips the fly-through, every time.

## The city

The fly-through, the menu's resting shot and the office have to be one city:
- **The tower's front** (user, 2026-11-10): the ground floor is the boss's restaurant, MISE EN FLEX in pink neon over five stars, packed, seen through clear glass: a lit bar with bottles, pendant lights, diners at high tables, a waiter. Outside:
  - a queue behind a rope, most of it on phones;
  - a red carpet with a couple posing and photographers flashing;
  - selfies by the glass, and an influencer filming under a ring light;
  - people going in and out;
  - the tower's own door with its doorman.
- **Social media first** (user, 2026-11-10: the game is about social media and reviews): flashes every few seconds on every phone, and hearts, stars and comments popping over them (`SocialPop`).
- **The avenue** (the user's diagram, 2026-11-09) is still there, but now only as the backdrop behind the camera's start.
- **The same tower:** the boss's tower, with the penthouse floor as built in `Office_Greybox`. It's glass all the way down, in the city's own style: a stone podium, stone piers, ledges, bronze mullions and louvred plant floors.
- **Floors you can see into** (user, 2026-11-10; "Mixed" chosen):
  - Where the camera climbs (the middle of the front), clear glass shows real rooms, one of five kinds per floor: open-plan, meeting room, kitchen with a sofa, a creative floor with ping-pong, a floor being fitted out. They're varied, mostly lit, with people about.
  - Every other window paints its room with depth (`SH_InteriorCard`, interior mapping): lit or dark, desks, someone at the back, blinds, a TV's flicker.
- **The lift is inside** (user, 2026-11-10): a glass cab between bronze posts, in the same place as the boss's floor's elevator, seen through the glass on the way up.
- **The sky** (user, 2026-11-09: "the sky is super important too"): stars that twinkle, a big moon with a halo in the open sky beside the tower's top, slow clouds silvered near the moon, and airliners crossing high over the city, blinking.
- **Hustle all along the avenue** (user, 2026-11-09), dense with clutter, movement and detail, built from the city kit and the office's `Ambience` life:
  - **clutter:** parked cars and scooters, bins and bin bags, newspaper boxes, fire hydrants, café tables, crates;
  - **movement:** people walking and waiting, a delivery rider, cars passing with their lights, pigeons, steam from a manhole;
  - **detail:** shop awnings and lit shopfronts, neon signs, fire escapes, string lights, posters;
  - **signs:** blade signs over the sidewalks, with neon words (BAR, NOODLES, HOTEL...) or lit boxes with a logo (a ramen bowl, a cocktail, a slice...); wall neon with made-up shop names and icons, its colour washing the wall; "OPEN" and "WE'RE OPEN" in the shop windows;
  - **people:** every 24 m a moment: a selfie, a portrait on a phone, friends chatting, a queue behind a rope, a tourist photographing the tower; flashes going off; a food market on a little square; twice as many walkers;
  - **cars:** sedans, hatchbacks, taxis and vans (`SM_Cars`), the moving ones lighting the road ahead;
  - **graffiti:** tags and murals on shutters and side walls, painted textures in the game's colours, never real-world logos or words that read as brands.
- **The office's city** (the near roofs and their life) and the menu's city become one layout seen from two heights, not two different cities.

## The hand-over (step 7)

**One scene** (user, 2026-11-09): the office scene holds the street, the menu and the floor, so there's nothing to load between them. The climb runs from wherever the camera is drifting at the stop, up the tower's glass, and turns onto the office camera's pre-join wide shot. The first thing players see is the whole floor and the elevator they'll come out of.

## How it's built (2026-11-09, reworked 2026-11-10)

- **`OpeningSequence`** (Presentation/Office) runs the steps:
  1. save notice (first launch only, a `PlayerPrefs` flag; A skips it);
  2. 0.5 s fade in;
  3. the approach, 8 s (A skips, every time);
  4. a 2 s beat, then the menu fades in;
  5. the chef walks in, about 6 s, the camera easing in to watch;
  6. the climb, 12 s;
  7. land.

  On landing, the follow camera takes over and joining opens. The menu's controller joins as player 1, coming out of the floor's elevator: the chef who rode up. The cab's chef hides, the empty cab sinks back down, and the street level switches off, since nobody sees it from the floor.
- **`OpeningChef`**: player 1's chef, in their look from the tailor (`ChefProfileFile`) and their colour, waits at the kerb. They walk a set path through the tower's door, round the shaft to the cab's doors (which slide open), and ride up.
- **The camera path** is a centripetal Catmull-Rom curve through poses set in `OfficeBuilder.Opening.cs` (`OpeningPath`, tested). It's sampled by distance, so the pace is even however far apart the poses are. The climb goes straight up the front, about 11 m off the glass, beside the lift, then over the cornice.
- **The lift** (`OpeningElevator`) rides level with the camera on the climb, so the chef stays in the middle of the frame.
- **The haze** is thinner (170–650 m) in the street and over the menu, and eases back to the office's on the climb, so the tower reads from the street.
- **`OpeningHud`**: the black veil for fades, the save card (turning icon and two lines), "A Skip" bottom right, and the four join slots in player colours along the bottom.
- **`MainMenu`** starts hidden in the office (`_startHidden`). When the office listens to `Launching`, the menu hands over instead of loading the kitchen.
- **Captures:** *Tools/Game/Capture Office* renders `opening_flight_*`, `opening_entering_*`, `opening_climb_*` and `opening_climbing_*` along the path. For the stills it freezes a believable instant: one flash in five going off, every other phone popping, the cab and chef where they'd be.

- **Leaving for the kitchen:** `MiniatureTable` in the back room. Once every joined player stands around the table, any of them presses A and the kitchen loads. A hint above the join slots counts who's there ("Everyone to the table: 2 of 3"), then shows "A Start the day". The players walk into the kitchen as they are, on the same controllers and in the same order, so the same colours and chefs, without joining again (`PlayerRoster`, user's call 2026-11-09). A player whose controller went away on the way joins as usual.
- **Boot order** (`BootOrder`, run by the office and menu builders): `Office_Greybox`, then `Kitchen_Greybox`. `MainMenu.unity` stays in the project for the menu's UI captures but isn't in builds.

**Still to do:**
- The join slots stay on screen on the floor; proposed: hide them a few seconds after the last join.
- Leaving cuts straight to the kitchen; a fade would be kinder.

## Open questions

1. **Logos:** which, and how long. Later.
2. **Content note:** is there one, beside the save notice? Is there bad language in reviews at all, and would a "clean reviews" setting suit?
3. **Loading state:** a loading moment may be needed after the save card if the office scene takes a while to load (user's note). The save icon could double as the loading indicator. With one scene there's no load between the menu and the floor.
