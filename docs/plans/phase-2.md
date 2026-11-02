# Phase 2 plan: First service (first vertical slice)

*Drafted and decided 2026-11-02. Decisions in `../decisions.md`.*

**Done when:** you press Start, and over a dinner service customers arrive, sit at the counter, and order a steak done how they like it (sometimes with onions). You cook, plate and serve each order while it's hot. Each customer pays and tips based on the dish and the wait, and leaves a short review. Customers who wait too long walk out. At the end, a service summary shows covers served, money earned, average stars and the reviews.

## Decisions

- **Counter service at the pass:** stools along the dining side of the pass; plates are served onto the pass in front of each customer. No front-of-house work yet (the smallest size tier, "counter").
- **Menu:** steak to order (rare, medium-rare, medium, well done), sometimes with sautéed onions. The order's doneness replaces the medium-rare house standard.
- **A service lasts ~6 real minutes** (~6 in-game hours at 60x, e.g. 18:00–00:00), with arrivals in waves and a mid-service rush. Tunable.
- **Wrong dish:** the customer eats it unhappily; off-target doneness is scored low (less pay and tip, a grumpy review line). No send-backs.

## Milestones

| # | Milestone | Playable result | Status |
| --- | --- | --- | --- |
| 1 | **Customers and seating:** arrivals in waves, walking to a free stool at the pass, eating, paying, leaving (greybox capsules) | Customers come in and sit down | Done |
| 2 | **Orders and tickets:** each customer orders (steak + doneness, maybe onions); tickets on a rail; a plate served on the pass in front of a customer completes their order | Cook to order | |
| 3 | **Patience:** drains while waiting (realism dial), shown above each customer; at zero they walk out | Time pressure | |
| 4 | **Payment, tips and reviews:** price × quality, tips for quality and speed, short review lines from the score parts, money counter | Score turns into money and words | |
| 5 | **The service loop:** Start, service clock, last orders, end-of-service summary, restart | The full slice | |

## Out of scope

Staff, the rest of the daily loop, reputation over time, allergies, critics and influencers (Phases 3–6); HUD polish (later HUD pass).

## How we'll know it's fun

- Mid-service you're juggling two or three tickets and a pan that's starting to smoke, and plan ahead (prep onions, stock the fridge) to stay on top.
- A good service feels like your doing: the summary should make it clear what went well and what cost you.
- Co-op: one keeps prep and storage flowing while the other cooks and serves, without being told to.
