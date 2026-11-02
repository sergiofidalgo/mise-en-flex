# Phase 1 plan: Cooking core

*Approved 2026-11-02. Decisions referenced here are in `../decisions.md`.*

**Done when:** two players stock up from a fridge, dice an onion on the board, sear a steak on a hob in a close-up while watching the heat, let it rest, plate it, and hand it in at the pass. The pass then shows a quality breakdown. Customers come in Phase 2, so the pass judges the dish instead.

## Milestones

Each one ends playable, tested and committed.

| # | Milestone | Playable result |
| --- | --- | --- |
| 1 | **Cooking simulation** (plain C#, tested): heat moves from hob to pan to food; doneness, browning and burning; preheating, crowding, carryover and resting. Ingredient and equipment data, a first `RealismSettings`, and a **debug overlay** with the numbers. | Put a steak in a hob's pan, change the heat with Use, and watch it cook in the overlay. |
| 2 | **Pans, plates and bin:** pans and plates are carryable containers; a plate stack hands out plates; the bin destroys items. Placeholder food colours (raw → browned → charred). | Move a steak from pan to plate, read doneness by colour, bin a burnt one. |
| 3 | **Close-up framework:** `ICloseUpTechnique`, a panel per player near the station (fading when the partner is behind it), the transition growing out of the station, CloseUp controls, one player per station, kitchen dim, edge pings. | Enter and leave a hob's close-up. |
| 4 | **Steak technique:** heat dial on the triggers, A flips, hold X bastes; rest and slice on the board. | The full steak, judged by eye. |
| 5 | **Board and onion technique:** stick moves the knife, A chops, Y rotates for dice; 3-slot rail; cut size affects cooking; sautéing in a pan. | Dice an onion, sauté it, plate it next to the steak. |
| 6 | **Fridge and pass:** 4-slot fridge with the button prompt and limited stock replaces the crates; the pass shows a quality card (doneness, serving temperature, browning, cut evenness). | The complete loop, for a playtest. |

Seasoning and freshness are tracked from milestone 1, without techniques yet.

## Key decisions

- **Time:** one clock at 60x for everything (1 real second = 1 in-game minute). A steak cooks in ~8–10 s.
- **Simulation units:** real-world units (°C, joules, watts, in-game seconds); gameplay feeds it real time × 60.
- Pans and plates are carryable containers.
- Doneness is read mostly by eye; numbers only in the debug overlay.
- Close-up panel near the station, fading when the partner is behind it (fallback: bottom corner).
- Steak and onion controls as above; fridge replaces crates in milestone 6.

## How we'll know it's fun

- **Steak:** you glance between the close-up and the kitchen, and sometimes save a steak at the last moment. If you never leave the close-up, cooking is too slow; if you never need it, the close-up adds nothing.
- **Onion:** dicing feels like a satisfying rhythm, not a quick-time event.
- **Co-op:** one preps while the other cooks, and hand-offs at the island happen without being forced.

## Risks

- Biggest phase so far: keep the simulation and close-up framework minimal and well tested.
- At 60x, technique windows are short; sear + flip + baste may not all fit in ~8–10 s (basting may become optional).
- Two close-up cameras rendering into panels: keep their resolution modest and check frame times in milestone 3.
