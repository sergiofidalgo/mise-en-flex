# Phase 6 plan: Campaign

*Draft 2026-11-10, waiting for the user's OK. Design in `../game-design.md` §8.2, §9, §11, §15; the office in `../office-scene.md`; the opening in `../opening-flow.md`.*

**Done when:** you start in the boss's office, buy a restaurant on the city miniature, name it and pick its style, and run it. Later you buy a second one and move between them, each keeping its own kitchen, stock, mess, reviews and fee clock. Falling behind on what the boss is owed has consequences you can see, up to losing everything but the cookbook. Owning every restaurant on the miniature ends the campaign with the takeover.

## What already exists

- **The office:** the floor, its rooms, the tailor, the elevator and joining, plus the opening from the street up the tower.
- **The way out:** the miniature table, which starts the day in the one kitchen.
- **Saves:** three campaign slots, each holding one restaurant.
- **Money:** a fee every 5 finished days, and money owed to the boss shown only as a number.

## Milestones

Phase 6 is big (decision 2026-11-06), so it's split in two halves. Each half ends with something playable.

### 6a: the empire

| # | Milestone | Playable result |
| --- | --- | --- |
| 1 | **A campaign of restaurants.** The save holds a list of restaurants, each with its own layout, stock, dirt, reviews, staff, day count and fee clock. Money and the loan belong to the campaign. A migration step makes today's save the first restaurant. Restaurants are data: a `RestaurantDefinition` gives size, seats, rent, inherited appliances, previous style, old reviews, its street and vendors. | Old saves load as restaurant 1; nothing looks different yet |
| 2 | **The city miniature and buying.** The miniature shows 5 or 6 lots with price, size, previous style, old reviews and inherited appliances. Buying asks for a name (a letter grid with suggested names) or keeps the old one and its reviews, and asks for a style (the first, a grill, has a fixed style). Choosing a restaurant you own starts the day there. | Buy, name and open a restaurant from the office |
| 3 | **A second restaurant.** At least two sizes really play (the grill counter and a mid-size dining room), each a kitchen built from its definition. A restaurant only runs while you're in it: the others sit idle, their fee clocks paused. | Run two places, come back to each as you left it |
| 4 | **The boss and money trouble.** The accountant lends money. A collection is worked out as Review starts (that restaurant's cut plus a loan instalment) and shown on the dashboard. The boss phones on the personal mobile (bottom right). Falling behind escalates (§15.3): a warning visit, bodyguards repossessing appliances on screen, the boss taking a restaurant, then erasure. Erasure ends the campaign, and the cookbook stays in the hall of fame for the next chef. | Debt is a real threat |
| 5 | **The takeover.** Once every restaurant on the miniature is yours, you take over the empire: the bodyguards follow player 1 and guard the door. | The campaign can be won |

### 6b: identity

| # | Milestone | Playable result |
| --- | --- | --- |
| 6 | **Service styles.** Fast casual, bistro and fine dining set patience, price expectations, plating demands and how many guests come. Chosen when buying. | The same kitchen plays differently |
| 7 | **Decoration.** Furniture, objects, floors, wall colours and windows, bought in management mode, carrying tags (formal, functional, eclectic, warm). An ambience part of each review, apart from the dish score, comments on the worst mismatch ("great steak, but I expected candles"). | The room matters to reviews |
| 8 | **The restaurant station.** A bought tablet that opens the listing on a shared screen, explains reviews in more depth, offers quick-restock delivery of a limited range, switches restaurants without a trip to the office, and changes style (§15.10). | Manage from the kitchen |

**What can run at the same time:** milestone 1 comes first, because everything sits on it. Then 2 and 4 can run side by side, and 3 follows 2. In 6b, 6 and 8 are independent; 7 follows 6.

## Questions for the user

1. **Fee period:** Phase 5 set a fee every 5 finished days, but §15.4 still says 7. Recommendation: keep 5, counted per restaurant on days played there.
2. **Loan repayments:** recommendation from `ideas.md`: each collection covers that restaurant's cut plus a loan instalment.
3. **How many restaurants:** recommendation: 5 or 6, built from a modular street kit, with 2 sizes playable in 6a and the rest in 6b or Phase 7.
4. **Kitchen per restaurant:** recommendation: the builder generates one scene per restaurant definition, as it does for today's kitchen. Changing a definition and rebuilding updates that restaurant.
5. **Escalation pace:** how many missed collections before each step? Recommendation: one step per missed collection, so a warning, then a repossession, then a lost restaurant, then erasure. Paying in full resets it.
6. **The split:** 6a first, playtest, then 6b.

## Docs out of date

`game-design.md` §15.4 says one campaign with no slots and a fee every 7 days. The user chose three campaign slots and a 5-day fee (decisions 2026-11-08). Fix when this plan is agreed.

## Out of scope

Cuisines, allergies, inspections, critics and influencers (Phase 7); the guided first day (Phase 8); art (Phase 9).
