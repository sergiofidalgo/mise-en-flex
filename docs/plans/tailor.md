# Plan: the tailor

*Proposed 2026-11-09. Decisions in `../decisions.md` (chef customisation, 2026-11-09), design in `../game-design.md` §15.4, the room in `../office-scene.md`.*

**Done when:** in the boss's office, a player steps onto their colour's plinth in the tailor, presses A and changes their chef: hat, face (with its hair), skin, accessory and outfit. B steps off. Each change saves at once to a player profile that outlives campaigns. The same chef shows up in the kitchen and the office from then on. Crazy hats and outfits show with a padlock until there's a way to earn them.

**The user's calls (2026-11-09):**
- Looks are kept **per player slot** (Player 1's chef, Player 2's chef...), not in named profiles.
- **Crazy items are visible but locked** until unlock rules exist.
- **Changes happen in the room:** the chef turns to the camera, and a card above them shows the choice. Both players can dress at once.

## Steps

Each step ends with something testable.

| # | Step | Result |
| --- | --- | --- |
| 1 | **The look and the profile (Simulation).** A `ChefLook`: one choice per module (hat, face, skin, accessory, outfit). A catalogue of options, each marked kitchen or crazy (locked). `profile.json` holds one look per slot, with a version number and a migration path; an unknown or missing option falls back to the default chef. EditMode tests for the round trip, migration and fallbacks. | Looks save and load, tested |
| 2 | **A wardrobe on the player.** The player prefab carries every option, built like the customers' roll. A `ChefWardrobe` shows a given look. Each joining player wears their slot's look in the kitchen and the office. The player colour stays on the hat band or headband, the neckerchief and the apron. | Edit `profile.json`, see your chef change |
| 3 | **The plinths.** Each colour plinth is a station for its slot's player:<br>• A opens the tailor;<br>• up/down picks the module, left/right picks the option;<br>• B steps off;<br>• the chef faces the camera while dressing;<br>• a card above them shows the module, the option's name, and a padlock on a locked one, which can be looked at but not worn;<br>• every change saves at once. | Dress your chef in the office |
| 4 | **What's on the rail.** See the list below. A couple of new hat models are needed for the crazy set. | A real choice of looks |
| 5 | **Check.** Tests, captures of the tailor room from the game camera, and a play checklist. | Ready to play |

**What's on the rail:**

| Module | Options |
|---|---|
| Hats | none (headband), toque, cap, bandana; locked: devil horns, a crown, a party hat |
| Faces | about 8 presets, each a hair style and colour with a nose and maybe facial hair |
| Skin | the 8 tones |
| Accessories | none, glasses, sunglasses, aviators |
| Outfits | chef whites, line cook (tee and apron), wok cook (tank top and apron), pastry (whites with a short apron); locked: a suit, a tourist |

## Not in this plan

- How crazy items are earned (Phase 7, with content).
- Named profiles.
- A mirror close-up.

## Risks

- **Another session is building the office** (`OfficeBuilder`, the city and the rooms). I'd only touch the tailor room and add new files, and commit only my own changes.
- **The player prefab gets more parts.** That's about 20 extra hidden parts per chef; check the frame rate with two players.
