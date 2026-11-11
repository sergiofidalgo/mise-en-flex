# Phase 5 plan: Persistence and economy

*Started 2026-11-08. **Closed 2026-11-10:** all six milestones built, merged and played by the user; what's left is small fixes, cosmetics and interaction design, to pick up as longer play sessions turn them up.*

*Decisions in `../decisions.md`, design in `../game-design.md` §4, §11, §12, §15.4–15.8.*

**Done when:** you can quit and come back to the same restaurant on the same day number, with your layout, stock, staff and money intact. Every day costs something (ingredients, wages, energy, a weekly fee), so the Review dashboard shows a real profit or loss. You buy your own ingredients from street vendors, choose and price your menu from the cookbook, clean up yesterday's mess, and can put out a gas fire.

## Milestones

Each milestone ends with something playable. Order chosen so each one builds on the last.

| # | Milestone | Playable result |
| --- | --- | --- |
| 1 | **Saves (done 2026-11-08).** One campaign file (this restaurant: layout, bought items, stock with freshness, staff, money, day number, days towards the fee, reviews) and one cookbook file, both JSON with a version number and a migration path. Saved as Review starts; loaded on launch with Continue / New campaign. Leaving mid-day asks first and says that day is lost. | Quit, relaunch, same restaurant and day |
| 2 | **Money out and the Review dashboard (built 2026-11-08, waiting for play).** Review becomes the read-only scene from the 2026-11-06 decision: the service, customers, dishes, money in and out (ingredients, wages, energy, fee), progress. Rent is a fee every 5 finished days (decision 2026-11-08), scaled by restaurant size. Plates can be bought, so broken plates are no longer replaced free. | A day can lose money, and you can see why |
| 3 | **Street vendors (built 2026-11-10, waiting for play).** Fixed deliveries go. The front door leads to a short street with stalls (butcher, greengrocer to start), each press buys one fresh portion (decision 2026-11-11), straight into the fridge or onto a shelf (decision 2026-11-10). Old food shows its age: stale below 35%, mouldy below 10%. You can go out during service, which costs time. | Shopping is a planning and budget choice |
| 4 | **Cookbook and menu pricing.** The cookbook opens from management mode: browse recipes, see complexity, appliances, ingredients, cost and value, pay to unlock. Unlocked recipes form the menu; you set each dish's price. Customers order only from the menu, and price shifts how many come and what they expect. A handful of recipes from what we can already cook (steak, onion steak, onion sides); real content is Phase 7. | Your menu and prices are yours |
| 5 | **Dirt, cleaning and fire.** Service leaves dirt on floors, tables and counters, saved overnight and cleaned in Prep: hold X and move the stick to match a pattern, with a mop or cloth. A pan left burning on gas catches fire (2026-11 decision); the extinguisher uses the same stick game. Induction and the grill never catch fire. | Mornings start with the mess you made |
| 6 | **Realism settings menu (built 2026-11-10, waiting for play).** A pause menu (tap Menu; hold Menu readies up) with presets (Relaxed / Standard / Chef) with individual dials under "advanced", read by the simulation through `RealismSettings`. | Tune the challenge |

## The user's mockups (2026-11-08)

**Review dashboard: "Plates", a review-listing app.**
- **Header:** the app name, a search bar ("Restaurants near Old Town"), the day ("Day 1 · Mon") and a "3 new reviews" badge.
- **Left column:**
  - a photo of the restaurant taken at close;
  - the restaurant's name and star rating with the review count;
  - a price band, the style and the opening hours ("€€ · Bistro · Open 18:00–20:00");
  - a "Tonight's guests" bar chart in half-hour buckets, served in green and walked out hatched orange, with a "Rush · 2 left" callout.
- **Right column:**
  - tabs Overview / Reviews / Menu / Books, switched with LB and RB;
  - stat cards for served, walked out (highlighted when it's a problem) and turned away, plus a dark balance card with today's change;
  - "Guests mention" chips, such as "Slow service · 3", "Steak · 2" and "Wrong order · 1";
  - review cards: a customer avatar and name, their order and the time, a quote, stars, a chip with the money result ("Walked out · −€12", "0.5 stars · €0 + €0 tip") and the reason in plain words.
- **Footer hints:** LB/RB Tabs, LS Browse, Y Reply, ≡ Start day 2.

**Street: the restaurant's surroundings, where the vendors are.**
- **The restaurant** faces a brick sidewalk and a road with passing cars. Customers queue on the sidewalk at the entrance. The dining room is open to the street behind a low fence with string lights. A tall sign with a bull's head stands at the right corner.
- **To the left,** a paved plaza holds three market stalls under coloured canopies (blue, red, green), each with a counter of goods.
- **To the right,** there's a parking lot. One trash container goes by the restaurant's side wall; the mockup's recycling area is too much (the user's note).
- **Behind,** houses, trees, fences and power lines are backdrop only.

## Technical notes

- Save data lives in `Game.Simulation` as plain C# records; `Game.Gameplay` reads and writes the file under `Application.persistentDataPath`. EditMode tests cover round-trips and every migration step.
- Saves come first even though later milestones add fields: each milestone adds its fields with a default, and a test proves an older file still loads.
- Restart (`PlayerRejoin`) keeps working, and now reloads the last save instead of starting fresh.

## Decided (2026-11-08, the user's calls)

- **Vendors are stalls you walk to,** on a short street outside the front door. Crates are carried back by hand.
- **Prices are free,** in steps of 1, with a suggested price shown, and set from the management interface.
- **The Review dashboard and the street follow the user's mockups** (above).
- **Money can go below zero.** It shows as owed to the boss, with no consequences until Phase 6.
- **Dirt comes from what happened:** spills, burnt pans, customers eating, busy floors.

## Out of scope

The office, the boss in person, borrowing, escalation and more restaurants (Phase 6); the tablet station and quick-restock delivery (Phase 6); allergies, inspections, critics (Phase 7); the first-day story (Phase 8). Until Phase 6, money owed to the boss is only a number on the dashboard.
