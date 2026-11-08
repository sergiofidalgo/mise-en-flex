# Phase 5 plan: Persistence and economy

*Proposed 2026-11-08, waiting for the user's OK. Decisions in `../decisions.md`, design in `../game-design.md` §4, §11, §12, §15.4–15.8.*

**Done when:** you can quit and come back to the same restaurant on the same day number, with your layout, stock, staff and money intact. Every day costs something (ingredients, wages, energy, a weekly fee), so the Review dashboard shows a real profit or loss. You buy your own ingredients from street vendors, choose and price your menu from the cookbook, clean up yesterday's mess, and can put out a gas fire.

## Milestones

Each milestone ends with something playable. Order chosen so each one builds on the last.

| # | Milestone | Playable result |
| --- | --- | --- |
| 1 | **Saves.** One campaign file (this restaurant: layout, bought items, stock with freshness, staff, money, day number, days towards the fee, reviews) and one cookbook file, both JSON with a version number and a migration path. Saved as Review starts; loaded on launch with Continue / New campaign. Leaving mid-day asks first and says that day is lost. | Quit, relaunch, same restaurant and day |
| 2 | **Money out and the Review dashboard.** Review becomes the read-only scene from the 2026-11-06 decision: the service, customers, dishes, money in and out (ingredients, wages, energy, fee), progress. Rent is a fee every 7 finished days, scaled by restaurant size. Plates can be bought, so broken plates are no longer replaced free. | A day can lose money, and you can see why |
| 3 | **Street vendors.** Fixed deliveries go. The front door leads to a short street with stalls (butcher, greengrocer to start), each with a cheap and a premium option: premium starts fresher and scores better. You carry crates in. You can go out during service, which costs time. | Shopping is a planning and budget choice |
| 4 | **Cookbook and menu pricing.** The cookbook opens from management mode: browse recipes, see complexity, appliances, ingredients, cost and value, pay to unlock. Unlocked recipes form the menu; you set each dish's price. Customers order only from the menu, and price shifts how many come and what they expect. A handful of recipes from what we can already cook (steak, onion steak, onion sides); real content is Phase 7. | Your menu and prices are yours |
| 5 | **Dirt, cleaning and fire.** Service leaves dirt on floors, tables and counters, saved overnight and cleaned in Prep: hold X and move the stick to match a pattern, with a mop or cloth. A pan left burning on gas catches fire (2026-11 decision); the extinguisher uses the same stick game. Induction and the grill never catch fire. | Mornings start with the mess you made |
| 6 | **Realism settings menu.** Presets (Relaxed / Standard / Chef) with individual dials under "advanced", read by the simulation through `RealismSettings`. | Tune the challenge |

## Technical notes

- Save data lives in `Game.Simulation` as plain C# records; `Game.Gameplay` reads and writes the file under `Application.persistentDataPath`. EditMode tests cover round-trips and every migration step.
- Saves come first even though later milestones add fields: each milestone adds its fields with a default, and a test proves an older file still loads.
- Restart (`PlayerRejoin`) keeps working, and now reloads the last save instead of starting fresh.

## Decided (2026-11-08, the user's calls)

- **Vendors are stalls you walk to,** on a short street outside the front door. Crates are carried back by hand.
- **Prices are free,** in steps of 1, with a suggested price shown.
- **Money can go below zero.** It shows as owed to the boss, with no consequences until Phase 6.
- **Dirt comes from what happened:** spills, burnt pans, customers eating, busy floors.

## Out of scope

The office, the boss in person, borrowing, escalation and more restaurants (Phase 6); the tablet station and quick-restock delivery (Phase 6); allergies, inspections, critics (Phase 7); the first-day story (Phase 8). Until Phase 6, money owed to the boss is only a number on the dashboard.
