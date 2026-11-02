# Phase 1 plan: Cooking core

*Approved 2026-11-02; revised the same day after dropping the picture-in-picture close-up. Decisions referenced here are in `../decisions.md`.*

**Done when:** two players stock up from storage, sear a steak at a hob (heat on the triggers, Use to flip), let it rest, plate it and get it to the pass while it's hot, and the pass shows a quality breakdown including serving temperature. Customers come in Phase 2, so the pass judges the dish instead.

## Differentiators this phase builds towards

1. **Storage with capacity** (main differentiator).
2. **Serving temperature:** food cools between pan and table.
3. **Food keeps cooking** off the heat (carryover, resting).
4. **Hobs (and later grills) with adjustable power.**

## Milestones

| # | Milestone | Status |
| --- | --- | --- |
| 1 | Cooking simulation (heat, doneness, browning, carryover, freshness), hobs, debug overlay | Done |
| 2 | Carryable pans, plates, plate stack, bin; cues by eye (steak colour, cooked edge bands, smoke, heat pips) | Done |
| 3 | ~~Close-up framework~~, built then dropped | Superseded |
| 3′ | **In-world hob controls:** triggers set the heat while facing a hob, Use flips, one piece of food per pan, doneness chip above the pan while facing it. Remove the picture-in-picture code. | Done |
| 4′ | **Storage:** 4-slot fridge or shelf with the A/B/X/Y prompt and limited stock, replacing the crates. Fridges keep food cold; shelves don't. Plus button prompts at every station that has actions. | Done |
| 5′ | **Pass and serving temperature:** plated food keeps cooling while it waits; the pass shows a quality card (doneness, browning, serving temperature). | Next |
| 6′ | **Board and onion:** rhythm chopping (tap Use), 3-slot rail, sautéing; resting and slicing the steak on the board. | |

## Key decisions

- One clock at 60x for everything (1 real second = 1 in-game minute); a steak cooks in ~6–8 s.
- Real-world units in the simulation; gameplay feeds it real time × 60.
- Pans and plates are carryable containers; one piece of food per pan.
- Doneness is read mostly by eye, with a doneness chip while facing a hob; numbers only in the debug overlay.

## How we'll know it's fun

- **Steak:** you watch the heat and the chip, flip at the right moment and pull it before carryover overshoots. Burning should feel like your fault, never the controls'.
- **Storage:** stocking the fridge before service visibly saves trips during it.
- **Serving temperature:** you feel the pressure to plate and send quickly, and a cold dish costs you.
- **Co-op:** one stocks and preps while the other cooks; hand-offs at the island and the pass happen naturally.

## Risks

- At 60x, timing windows are short; the doneness chip and smoke must make them readable.
- Storage must stay spatial and visible, not turn into inventory menus.
