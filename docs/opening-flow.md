# The opening — from launch to the office

*Spec, 2026-11-09, from the user's outline and their answers. References: the skyline mockup (`10_menu_skyline.svg`), the main menu mockup (`Art/Direction/concepts/2026-11-08_main-menu_new-player.png`), `office-scene.md`. Not built yet.*

The first half-minute takes players from the city's streets up the boss's tower and into his floor, in one unbroken camera move. The menu and the office become one place: the game happens in the glowing penthouse you see from the street.

## The steps

| # | Step | Status |
| --- | --- | --- |
| 1 | **Logos** (engine, production) | Open, to work out later (user, 2026-11-09). Unity 6 no longer requires its splash. Proposed: 2–3 s each, first launch only, any button skips afterwards. |
| 2 | **Save notice**: what the save icon means, and not to quit or turn off while it shows | Replaces the bad-language disclaimer idea (user, 2026-11-09). Whether there's a content note too is open. Proposed: one card, about 3 s, first launch only and then from Settings. The same icon shows as Review starts and whenever the office saves (`decisions.md`, 2026-11-06). |
| 3 | **Fade out, fade in** | Agreed. Short, about 0.5 s each, so it doesn't feel like loading. |
| 4 | **The fly-through**: the camera moves through the city at night, a park with people walking, birds, lit windows and people passing behind them | Agreed. 8–10 s. Uses the office's `Ambience` life (walkers, flocks, windows, silhouettes). Pressing A skips to step 5. |
| 5 | **The stop**: the camera eases to rest with the boss's tower framed on the left, the sign hanging in front of its spire, and the menu fades in | Agreed. The framing of the main menu mockup. The lit penthouse is the teaser. |
| 6 | **The menu**: New campaign or Continue (Free kitchen fades to the kitchen) | Agreed. The menu's city needs reworking with this in mind (user, 2026-11-09): see *The city* below. |
| 7 | **The climb**: the menu fades out, the camera rises up the tower's glass and lands on the office's wide shot, looking at the elevator | Agreed. See *The hand-over* below. |
| 8 | **Joining**: a hint to press A; each join rings the elevator and a chef steps out | Proposed, not yet answered. Whoever drove the menu is already player 1 on that controller, so they don't press A twice; others press A. Slots shown: 2 (the supported count) unless 3–4 players becomes a goal. |
| 9 | **Free play** on the floor | Proposed. Later, the guided first day (Phase 8) starts here: the boss's call or a first task. |

**Pacing:** first launch, about 25–30 s before control. Returning players reach the menu in a few seconds: logos and the save card show once, and A skips the fly-through.

## The city

The fly-through, the menu's resting shot and the office have to be one city:
- **The same tower:** the boss's tower, with the penthouse floor as built in `Office_Greybox`, a lit band at the top and a spire, stands where the menu's camera can frame it.
- **A park at street level** for the fly-through to start in, among the existing street, plaza and buildings.
- **The office's city** (the near roofs and their life) and the menu's city become one layout seen from two heights, not two different cities.

## The hand-over (step 7)

The menu and the office are separate scenes. Proposed:
1. **Load early:** while the menu is up, the office loads additively in the background, so it's ready when New campaign or Continue is chosen.
2. **Climb the tower:** the camera rises up the tower's curtain wall. A short whoosh past the glass hides the moment the office's camera takes over.
3. **Unload:** the menu scene is unloaded once the office's camera has taken over.
4. **Land on the wide shot:** the office camera's pre-join wide shot is the landing, so the first thing players see is the whole floor and the elevator they'll come out of.

## Open questions

1. **Logos:** which, and how long. Later.
2. **Content note:** is there one, beside the save notice? Is there bad language in reviews at all, and would a "clean reviews" setting suit?
3. **Joining:** two slots or four, and does the menu's player join automatically?
4. **Loading state:** a loading moment may be needed after the save card or the climb if the office isn't ready (user's note). The save icon could double as the loading indicator.
