# Decisions

Design and technical decisions, newest first. Each entry: date, decision, reason.

---

## 2026-11-09 — Every on-screen text goes through our own translation tables; a language setting

**User's call:** our own CSV tables rather than Unity's Localization package (step 2 of `ideas.md`, "Localising reviews and replies").

- **Keys are the English text** (gettext style): `Tr.Get("Settings")`, `Tr.Format("Start day {day}", ("day", 2))` with named slots, `Tr.Plural(n, "1 day", "{n} days")`, and `Tr.In("heat", "medium")` for an English word with two meanings ("medium" heat is "medio", a "medium" steak is "al punto"). Untranslated text reads in English, so rewording English never breaks anything.
- **One CSV per language** (`Data/Localization/es.csv`: english, translation, note), listed in the `LanguageTables` asset that `HudStyle` loads. Spreadsheets open them as they are.
- **Tools/Game/Update Language Tables** collects every key (what the code passes to `Tr`, serialized HUD texts shown through `Tr.Get`, ingredient, doneness, staff and furniture names, the review voices) and merges them into each table, keeping translations; keys nobody uses any more stay, marked `unused`.
- **Settings has a Language row** (English, Español, and in the Editor and development builds a pseudo-language that accents, brackets and lengthens every translated text, to find hard-coded strings and text that can't stretch). It's saved in the player settings and switches everything at once, old reviews included.
- **Money, dates and names follow the language** (`Locale`, `GuestNames`). Data words are translated where they're shown (`ItemCatalog.DisplayName`, tickets, tags, prompts).
- Not translated: the developer cooking overlay, the sign artwork's baked-in text, and the boss's office (another session's work in progress). The review voices' Spanish lines are step 3.
- Tests check that every text in the code has a Spanish translation and that every translation keeps its `{slots}`.

**Reason:** A small, readable system we control: no package setup, tables a translator can edit in a spreadsheet, and keys that are the English itself, matching how reviews are already stored (templates keyed by their English).

**Spanish needs a native review:** the first table (Spain Spanish, informal "tú") was written for testing the system. Some lines read stiffly in Spanish because English word order is assumed, e.g. "{dishes} are costing you." becomes "Filete, al punto te está costando caro."

---

## 2026-11-09 — Reviews are written when shown, so a language switch rewrites them all

**User's call:** changing language mid-campaign must localise everything, old reviews included, automatically. This is step 1 of the plan (`ideas.md`, "Localising reviews and replies").
- **Saves keep what was said, not the sentence.** Each review saves the templates its line was written from (`LineParts`: the line, and an influencer's hashtag) and the templates of the owner's reply (`ReplyParts`: greeting, heart, sign-off), plus the facts that filled them (`Facts`: dish, side, doneness ordered and served, wait, time left, restaurant). The guest's name comes from their name slot.
- **The key is the English template itself,** the way gettext works. Translators map English templates to their language (`Translations`, empty until step 3). Rewording an English line never breaks an old save, which keeps the old template and still reads in English.
- **`ReviewText` writes each line and reply when it's shown,** in `Locale.Language`. Templates, dish and doneness words come from that language's table, and the guest's name from their slot. Anything without a translation shows in English. The dashboard (quotes, replies, orders, the Menu tab's dishes), the main menu's best review and the in-service review popup all use it.
- **Existing saves upgrade themselves.** `ReviewTextRecovery` turns each template into a pattern, finds the one a saved English sentence came from, and recovers its facts from the words: "31 minutes, no steak. Left at 20:05. #medrare" gives back the template, 31, 20:05, steak and the hashtag. Replies built from parts are traced part by part.
  - It runs when the kitchen loads a campaign and when the menu reads the slots. The next save keeps what it found, and reviews already traced are left alone.
  - Lines from before the voices (the first fixed replies) can't be traced and show as saved.
  - `ReviewTextTests` checks every template in the grill and generic voices traces back to the same sentence.
- **The English and the old sentence are still saved** (`Line`, `ReplyText`) as a fallback, and for anything reading saves directly.

**Not yet:** UI strings (labels, hints, the Menu warning, "Guests mention" topics) are step 2, with Unity's Localization package and a language setting. The voices' translations are step 3.

---

## 2026-11-09 — Currency follows the language

**User's call:** dollars in English, and currencies follow the language.
- **`Locale` holds the language and how money is written in it.**
  - English: "$2,340.50", with the symbol first and a comma for thousands.
  - Spanish: "2.340,50 €", with the symbol after a space and a dot for thousands.
  - Negative amounts get a minus sign ("−$62.40"). Price bands follow the symbol too ("$$" or "€€").
- **Amounts are the same numbers in every language,** with no exchange rate: a $24 steak is a 24 € steak.
- **Everything that writes money goes through `Locale`:** the money HUD, review popups, the plan screen, the layout editor's prices, the Review dashboard, the main menu cards and the debug overlay. `ServiceConfig`'s fixed currency symbol is gone.
- **Names and money share one language:** `GuestNames.Language` is `Locale.Language`. It's English until the game has a language setting (`ideas.md`, "Localising reviews and replies", and the plan for the whole game).

---

## 2026-11-09 — Shorter replies; guest names that translate

**User's call: replies shouldn't be unnecessarily long.**
- **Most replies are now short:** 40% are a one-liner (50% when the owner is fed up). Most of the rest are two parts, usually the heart of the reply and a sign-off. All three parts happen 5% of the time.
- **The cap is 110 characters** (was 150). When a reply runs long, the greeting goes first, then the sign-off.

  "Thanks for being straight with us, Tony. We let the tickets pile up and you paid for it. Come back on a Tuesday. It's calmer." now tends to read "We let the tickets pile up and you paid for it. Come back on a Tuesday. It's calmer."
- **The month metrics still pass:** replies 90–93% unique, none more than twice or within 10 days.

**User's call: guests' names should be local in a translated game** ("Juan S.", not "John S.", in Spanish).
- **Saves keep a guest's name slot and initial** (`ReviewRecord.GuestSlot`, `GuestInitial`) as well as the English name. The guest book still knows guests by the English name.
- **`GuestNames` holds a first-name table per language, slot for slot.** Each slot is a role (a guy called Mike is Miguel in Spanish), not a literal translation.
  - There's English and a Spanish example; `GuestNameTests` checks every language fills every slot.
  - The dashboard and the main menu show names in `GuestNames.Language`, a stand-in until the game has a language setting and Unity's Localization package.
  - A language with no table shows English, and older saves show the name they kept.
- **Replies address the guest by their name in the player's language.**

**Flagged for later (`ideas.md`):** review and reply text is still saved as finished English sentences. Translating those needs saving which line was used plus its facts, and writing the sentence when it's shown.

---

## 2026-11-09 — Measured variety; fuller replies; an owner who tires

**User's call: every restaurant's interactions should feel unique, measured, not hoped for.**
- **`VoiceMetrics` plays a busy month in a restaurant's voice:**
  - four reviews a day, a realistic mix of complaints and praise, and every kind of guest;
  - returning guests;
  - replies to 80% of reviews (fitting 70% of the time), with the owner tiring;
  - the campaign's no-repeat memory.

  It reports how unique the reviews and replies are, the most uses of any one text, and the shortest gap between repeats.
- **`VoiceMetricsTests` holds the grill to it on three seeds.** No reply may appear more than twice in 30 days or within 7 days of itself. No review line may appear more than 3 times or within 5 days.

  When a test fails, the voice needs more lines, not looser targets. It already caught two: the "apologised, worse again" return lines were too few, and so were a tired owner's apologies.
- **Today's results:** replies 100% unique; reviews 73–78% unique, at most twice or three times a month, never within a week. **Tools/Game/Write Review Samples** now opens with this report.

**User's call: replies of varying length, more natural, using names, and rude when the day's been long.**
- **Replies come in three lengths.** About a third are the short one-liners. The rest are built from an opener (usually with the guest's name), the heart of the reply and a sign-off, up to two lines on the card. That makes hundreds of combinations per kind of reply.
- **The owner's mood:** each reply posted today and each walk-out tonight wears on them. After 3 they're tired, after 6 fed up (`ServiceConfig` → Tired After, Fed Up After).
  - Tired replies admit the long night.
  - Fed-up apologies turn passive-aggressive ("Yes, it was slow. Everyone has mentioned it. Repeatedly.").
  - Fed-up pushback turns openly rude ("Maybe try the chain place by the highway. They have a laminated menu.").
- **The no-repeat memory grew from 80 to 200 lines**, about a month of reviews and replies built from parts.
- **The dashboard measures each reply's wrapped height,** and its card grows by a line when it needs two.

**User's call: rudeness costs return visits.** Each reply's mood is saved with its review (`ReviewRecord.ReplyMood`). A guest's willingness to come back is multiplied by it: ×0.8 after a tired reply and ×0.4 after a fed-up one (`ServiceConfig` → Return After Mood). A fed-up apology still beats silence, just. A reply's rating nudge is unchanged. The metric targets and the mood thresholds are first guesses.

---

## 2026-11-09 — One reply per review, and returning guests who remember

**User's call: one reply per review, not a conversation.** The owner's reply sits under the review. Its words show what kind of reply it is: an apology sounds sorry, thanks sounds grateful (or tone-deaf, to a complaint), and digging in sounds proud. The guest's answers and the likes are gone, from the data, the writer, the dashboard and the save. This supersedes those parts of the two entries below.

**Returning guests (user's choice of next step), built:**
- **Who returns:** only guests who have posted before, since reviews are how the game knows them. The guest book (`GuestBook`) is rebuilt each day from the review history, so it needs no save of its own.
- **When:** a reviewing guest is someone who's been before 30% of the time, when anyone might come back. Each returns at most once a day.
- **Who comes back depends on how they were treated last time.** A fitting reply to a complaint (an apology) weighs 3. No reply weighs 1, and a dismissive reply (thanks or standing your ground) weighs 0.3. Thanks for praise weighs 2, and no thanks 1.2. This is the reply's real effect: players see who comes back and what they say (learn by playing).
- **What they write remembers it:** lines per memory, for a better or worse night ("Came back after the apology. Glad I did." against "Still no reply. Still not right."). There's a generic set for every memory and the grill's own. They keep their name and kind of guest, and they're 1.5× likelier to post.
- **On the dashboard,** their review's details end "back again", or "visit N" from the third.

**Proposed while building (flagged for review):** every number above (30%, the weights, 1.5×) is on `ServiceConfig` → Reviews, a first guess to tune in play. A new guest's generated name can match a known guest's by chance, and they'd then be counted as the same person. That's rare, and left for now.

---

## 2026-11-09 — Not every guest posts a review; the grill's full voice

**User's call:** like in real life, a guest can leave happy or disappointed without posting about it.
- **Each guest posts by chance.** It's likeliest after a walk-out (65%), then a complaint (50%) or praise (45%). A forgettable "decent" night is rarely written about (15%).
- **Some kinds of guest post more than others.** The chance is multiplied by the guest: regular ×0.7, foodie ×1.3, influencer ×2, critic ×3, tourist ×1.1, local ×0.8, capped at certain.

  All of these are on `ServiceConfig` → Reviews, and they're first guesses to tune in play. The rating, the review count and "Guests mention" come from the reviews posted.
- **The dashboard counts guests, not reviews,** wherever it means guests: "Tonight's guests" and the Menu tab's orders and money read every guest served or who walked out. The rating column reads the reviews. "All time" reads a per-dish tally saved with the campaign (`CampaignSave.Dishes`), so it no longer depends on reviews either.

**The grill voice, written in full** (`ReviewVoiceDefaults.Grill`):
- About 130 lines, with every complaint type covered by at least two.
- Each kind of guest has its own lines.
- Every reply combination is written to read as what it is.
- Hashtags for influencers.

**Writing rules the code now enforces** (`VoiceFitTests`):
- A review line fits one line of the card: 44 characters with the longest facts filled in.
- An influencer's hashtag is only added when it still fits.
- Replies fit 72 characters.
- Every fact a line names is one the writer knows.
- A line starting with a fact gets a capital.

**Doneness complaints have their own reply group,** so replies naming the order ("you asked for medium rare…") answer only under- and overcooked reviews. Other food complaints get general food replies.

**Tools/Game/Write Review Samples** writes a sample week (each guest, their review, a reply or none, and returning guests) to `Screenshots/ReviewSamples.md`, for reading the writing as players will meet it.

---

## 2026-11-09 — Review voices: the system for varied reviews and replies

**User's call:** build the system before writing much content. Built, with starter writing to deal from:
- **A voice per restaurant style plus a generic one** (`ServiceConfig` → Voices, editable in the inspector). Each voice has:
  - guest lines per topic, optionally for one kind of guest;
  - owner replies per reply and per what the review was about (praise, the food, the wait);
  - warm and dry guest answers;
  - how often each kind of guest comes;
  - hashtags.

  The grill/steakhouse voice and the generic one exist. The old review lines joined the generic voice.
- **Guests have a persona:** regular, foodie, influencer, critic, tourist or local, weighted by the style. Influencers add a hashtag and draw four times the likes.
- **Lines use the visit's facts:** {main}, {side}, {ordered}, {got}, {name}, {restaurant}, {left} and {wait}. A line naming a fact that isn't known is skipped, and lines with facts are favoured two to one.
- **No repeats:** the last 80 lines used are remembered and saved with the campaign, so words don't come back across days. When every fitting line was used lately, the one used longest ago returns.
- **Replies write a thread** (`ReviewReplies.WriteThread`): the owner's reply in the style's voice, about what the review said, then the guest's answer. It's warm if the reply fits, with 3–14 likes, and dry if it doesn't, with 0–2. The same reply to the same review always reads the same. Old saves' replies fall back to the fixed lines.

**Proposed while building (flagged for review):** how likes are counted, the personas and their weights, and the size of the memory (80) are first guesses to tune in play. The starter writing is a first pass.

**Not built yet (ideas.md):** returning guests who remember a reply, running jokes, and more voices (one per cuisine in Phase 7).

---

## 2026-11-09 — Reply effects show in the thread, not as chips

**User's call:** no effect chips on the reply wheel. Instead, the owner's reply as it appears in the thread, and how the guest answers it, hint at the intended effect. Players read the effect rather than seeing numbers, which keeps learn-by-playing. The design (and the wider push for varied, funny reviews tailored by restaurant style) is in `ideas.md`, "Reviews and replies with variety, wit and a voice per restaurant". Not built yet.

---

## 2026-11-09 — The boss's floor rebuilt from the kitchen's pieces, at office scale

**User's notes on the first greybox:** some textures didn't work; they expected the 3D models we already have (chairs, kitchen appliances) to be reused; the quality should match the kitchen; and the rooms felt too small to feel like walking through an office. The draft was only a reference.

**Done:**
- **Bigger rooms.** The columns are 11, 13 and 11 m wide and the rows 9, 8 and 10 m deep, so the floor is 35 × 27 m with a 6 m terrace (it was 24 × 18 m). Doors are 2 m wide.
- **The kitchen's materials.** Floors and walls use the kitchen's room shader (planks, tiles, panelled walls) at true size. The first greybox stretched the toon shader's wood grain over whole rooms, which caused the smeared stripes, and capped its walls in a dark trim the kitchen doesn't have.
- **The kitchen's furniture and models.** Counters, hob, fridge, cutting board, knife rack, shelf, tables and bar tables with stools are copied from the kitchen's prefabs, visual only. The dining chair and pictures are reused too. People are built from the chef's model, recoloured and without the toque. Trees, street lamps, water towers and the towers around come from the night city's models.
- **New models only where nothing fits:** `SM_Office` (Blender, same script, `-- --only SM_Office.fbx` so the other models aren't rewritten). It holds the sofa, armchair, executive desk, office chair, filing cabinet, plant, floor lamp, trophy, cowboy hat, tailor's plinth, clothes rail, jacket, umbrella, lounger and helicopter, plus a bevelled block for simple shapes.
- `HouseStyle.RoomMaterial` is now public, so other builders can make room-shader floors.

**Not done:**
- The people don't sit; the boss and accountant stand behind their chairs.
- The rooms' interactions are still Phase 6. There's no join hint, the exterior isn't yet cooler, and the scene can't be reached from the main menu.

---

## 2026-11-09 — The boss's floor as a greybox

**Done:** `Tools/Game/Build Office` (`OfficeBuilder`) builds `Office_Greybox.unity` to `docs/office-scene.md`: the nine rooms, terrace and helipad, the boss's tower and the night city below, with every room dressed in primitives in the toon materials (boss, bodyguards, receptionist, tailor, accountant, the miniature table, the cookbook lectern, and so on). Only walking and joining work.
- **The elevator works.** Each join rings a new arrival bell (`SFX_Lift_Ding_01`, synthesized, lower and purer than the order bell) at once. The doors open 0.35 s later. They stay open while anyone is in the car or doorway, then close after 1.5 s. A join while they're open rings again and restarts the hold, and one while they're closing reopens them. Logic in `ElevatorDoors` (tested), timings in `ElevatorConfig`. A dial over the doors sweeps up to the floor, and a lamp lights while the car is here.
- **The camera** (`OfficeCamera`): a wide shot of the whole floor before anyone joins, then it follows the players at the kitchen's 55° tilt, between 10 and 22 m up. It's a simpler cousin of the kitchen's `FramingCamera`, which depends on the kitchen's systems.

**Proposed while building (flagged for review):**
- Doors from lounge to test kitchen and from accountant to hall of fame, so no room is a dead end.
- The elevator's door wall is 2.5 m, which briefly hides a chef stepping out.
- The boss wears a camel suit and a white cowboy hat; the bodyguards wear sunglasses and red ties.
- The test kitchen's floor is the kitchen's own tile rather than the draft's cream, which would swallow a white chef.

**Not done:**
- Room interactions (Phase 6), a "press A to join" hint, Blender models (art phase), and a cooler, desaturated exterior (art bible: a local Volume).
- The office isn't reachable from the main menu yet: open the scene and press Play.

---

## 2026-11-09 — The boss's floor

**User's calls**, from their lo-fi floor plan; the full spec is `docs/office-scene.md`:
- **The office is the whole penthouse floor of the boss's skyscraper:** nine rooms in a 3 × 3 grid (tailor, the boss's office, back room with the city miniature; test kitchen, guarded doors, hall of fame; waiting lounge, reception, accountant) with a terrace and helipad behind. The full floor stays, rather than a tighter set of core rooms.
- **There's a test kitchen in the office.** It's an office kitchen with bare-minimum appliances where players try things out (searing, cooking, cutting). Its fridge holds the ingredients of every recipe in the cookbook, so it grows as players buy and learn recipes across restaurants. This replaces "No test kitchen in the office" (2026-11-06). The rest of that rule still holds: an ingredient and its mechanics are first met when it's bought, since only learned recipes reach the test kitchen.
- **One big elevator, not four.** Every player joins through it. Each time someone joins, it rings as if it's just reached the floor, the doors open and their chef steps out.
- **Spec first, Art Director later.** The scene is specced from the user's draft now; the AD reviews it before the art phase.
- **A day starts at the city miniature,** once every joined player is at it, looking at the map. The elevator is for joining and coming back, not for leaving.
- **The waiting lounge and the accountant are for fun:** characters and objects to interact with, and easter eggs. The books stay on the Review dashboard.
- **The bodyguards taste the test kitchen's food.** Players serve it to them, and they review it like customers, but pay nothing.
- **The free kitchen sandbox stays as its own mode,** reshaped: players play a restaurant they've already bought, with the recipes they've bought, real customers and phases, and unlimited money for appliances and ingredients (`ideas.md`).

**Proposed in the spec (flagged for review):** rooms 8 × 6 m like the kitchen; every wall cut to 1.0 m except a glass back wall, so the overhead camera sees into every room; a wide shot of the floor before anyone joins, then the kitchen's follow camera; the test kitchen free, endless, unsaved and without a clock; a serving hatch from the test kitchen to the bodyguards, whose reviews are practice and never reach a listing; the reception checker in two close tones and the carpet in burgundy, for readability against the chef and the "Bad" red.

**Where `game-design.md` is now out of date:** §15.2 describes the office as a single lobby and doesn't mention the floor's rooms, the elevator, the test kitchen or starting a day at the miniature.

---

## 2026-11-09 — The Review dashboard drawn to the Art Director's dashboard mockups

**User's call:** the reply wheel keeps the game's three replies in the mockup's look: X Apologise, Y Thank, A Stand your ground, B Leave it (closes without replying). No effect chips: players still learn what replies do by playing. The mockup's Invite back and Defend, with their effects, aren't built.

**Done:**
- **Measured from the six dashboard SVGs** (overview, reviews, reply wheel, reply thread, menu, books), in Inter, Inter ExtraBold and Lora. The mockups' outlined half-stars, pink forkcast logo and icons are exported as sprites. The fonts and art hang off `HudStyle` (Tools/Game/Apply UI Art), so the kitchen scene needs no rebuild.
- **Behaviour that changed with the look:**
  - Reviews sort "needs attention first": walk-outs, then the lowest stars, by the guest's own stars, so a reply doesn't move a review.
  - The Overview shows two reviews and a "N more reviews" bar.
  - Tabs or the stick close the reply wheel and carry on.
  - Replies name the guest ("Sorry, Inés. …").
  - The Menu tab warns about a dish when two or more of its orders walked out today.
  - Books shows the boss's fee as day segments, "Short by" or what's owed, and where the money went as two stacked bars. From day 3 a "How it's going" strip shows each day's net and the team's day.
- **The backdrop is the photo taken at close, blurred and dimmed.** Before, it was a flat dim. The canvas scales with Expand, so 16:10 screens fit.

**Flagged for review:**
- **Reviews filters (user's call):** the stick or arrow keys go up from the top review to the chips; left and right filter at once (All, Walk-outs, 1–2 stars, 3+ stars, Unreplied); down returns to the list. The active chip is dark, with a gold ring while the chips have the focus, and the hint reads "LS Filter". The filter resets each new day.
- The Menu warning adds "Guests wait longest for these." The mockup's tip about grill time only fits medium steaks. The user will review it while testing.
- With two or more cooks, who's ready shows at the left of the hint bar.

---

## 2026-11-09 — The main menu drawn to the Art Director's v11 mockups

**Done (the user asked for the menu to mirror the intended design):**
- **Measured, not eyeballed.** Positions, sizes, radii and colours come from the mockup SVGs (`Art/Direction/concepts/svg/ui_svgs.zip`); text is placed by its baseline, as the mockups set it. `Tools/Game/Capture Main Menu UI` renders the menu at 1920×1080 with sample campaigns that mirror the mockup, for side-by-side checks.
- **Type:** Inter (Medium, SemiBold, Bold) and Lora Italic for quotes, both SIL Open Font License, as static TextMeshPro atlases (Latin-1). They replace LiberationSans on the menu only. The art bible's "e.g. Fredoka / Nunito" doesn't match the mockups; that line is the Art Director's to update.
- **Static art is exported from the SVGs as sprites** (`Assets/_Project/Art/UI/Menu`): the sign with its stickers, the forkcast logo, icons, polaroids, the dashed slot frame. The sign follows v11 (gold chains, a hard drop shadow, an inner frame line) rather than the SVG export, which still had orange posts.
- **The canvas scales with Expand**, so the whole 1920×1080 layout fits on a 16:10 MacBook screen (before, the sides were cut off). The hint bar spans the screen's bottom edge.
- **Dark text on light panels gets a small SDF dilate**, because linear colour space draws it 20–40% thinner than designed.

**Kept different from the mockup (flagged for review):**
- **No followers chip:** followers wait for influencers (2026-11-08). The rating line already shows the review count.
- **The hints show only what works:** on the list they're Browse and Select. The mockup also shows Back and Delete slot there, but those do nothing on the list.
- **Cook dots use the game's player colours**, not the mockup's illustrative ones.
- **The sign's "@ines.eats" sticker, 12.4k bubble and polaroid are now static art**, as in both mockups (the new-player mockup shows them with no campaigns). Before, the sticker showed the best real review.
- **Free kitchen stays dimmed** until it's built (2026-11-08).
- **Selected rows show a short line of what they do** (e.g. Settings: "Cooking precision, spoilage, patience"), extending the mockup's Continue and New campaign sub-lines to every row.

---

## 2026-11-08 — A main menu with three campaign slots; the review app is forkcast

**User's calls (from the user's main menu mockup):**
- **There are three save slots, each a separate campaign** with its own restaurant, money, boss debt and days. This replaces "one campaign at a time, no slots" from 2026-11-06.

  `CLAUDE.md` (hard rules and Saves) and `game-design.md` §15.4 still describe one campaign. They're out of date, and they're the user's to update.
- **The main menu has:**
  - a sign: "Mise en FLEX";
  - Continue (the last played slot);
  - New campaign;
  - Free kitchen;
  - Settings, Credits and Quit;
  - slot cards on the right. Each card shows a photo, name, rating, style and area, the day, balance, the cooks who played it, the best review, when it was last played and the time played. Y deletes a slot.
- **Free kitchen is a sandbox for later:** no money, fee or boss, and nothing saved. It's greyed out in the menu until it's built (`ideas.md`).
- **The review app is called forkcast** everywhere, including the Review dashboard (was "Plates").
- **The menu's backdrop is a 3D night city in the kitchen's style:** toon shading and ink outlines.

**Details (proposed while building; flagged for review):**
- **New campaign takes the first free slot.** With all three full, it asks players to delete one first.
- **Opening the kitchen scene directly in the Editor** continues the last played slot, or starts a new campaign in a free slot. This is for testing, with no menu needed.
- **The city is built from simple shapes for now.** Real models are a Blender task to raise with the user.
- **Cards show the review count, not followers.** Followers wait for influencers (Phase 7).
- **Settings holds the three realism dials that exist** (cooking precision, spoilage, customer patience), saved for all slots. Presets and rebinding controls come with milestone 6.
- **The cookbook outlives campaigns.** Whether the three slots share one cookbook is open, to decide with milestone 4.

---

## 2026-11-08 — Money out, the fee every 5 days, and the Plates dashboard

**User's calls (Phase 5, milestone 2):**
- **The fee to the boss falls due every 5 finished days in a restaurant, not 7.** It's a tuning value. This replaces "every 7 days" in `game-design.md` §15.4, and the collection day also sets the next period's goal.
- **The fee is sized at about three good days' profit,** so a good period leaves money over and a bad one goes into debt. It's a tuning value, to adjust in play.
- **The morning delivery is charged at the ingredients' prices** until street vendors replace it in milestone 3.
- **Replying to a review (Y) gives a small reputation nudge.** There are three canned replies: apologise, thank or stand your ground. A fitting reply softens a bad review a little, and a poor one makes it slightly worse. One reply per review, shown under it.
- **Books shows today and the period so far, plus the boss and progress:**
  - money in and out, line by line;
  - what's owed to the boss and when the next fee falls due;
  - how the restaurant is doing day by day (rating, guests, money).
- **The dashboard follows the user's mockup** (named forkcast since the main menu decision) (`plans/vision for management UI.png`).

**Details (proposed while building; flagged for review):**
- **The dashboard is a full-screen view over the kitchen during Review,** not a separate Unity scene. It's still read-only, and it avoids reloading the restaurant every day.
- **Replies:** apologising fits complaints and thanking fits praise. Standing your ground never fits, which players learn from the listing. The nudge is about ±0.25 stars on that review's weight in the rating, as a tuning value.
- **The rating in the listing is the average of every review so far,** after reply nudges.
- **Broken plates are no longer replaced free;** plates are bought in the shop, a few at a time.
- **The restaurant's name is a placeholder until it's chosen when buying in Phase 6.** Day 1 is a Monday.

---

## 2026-11-08 — Saves: a title card, the morning after, everything as it lies

**User's calls (Phase 5, milestone 1):**
- **At launch with a saved campaign, a small title card asks first:** "Continue · Day N" or "New campaign". New campaign asks again, since there's only one campaign. Nobody can join until one is chosen. The boss's office replaces the card in Phase 6.
- **Continue starts the morning after the saved day,** in Plan, with the Review already seen.
- **Everything is kept as it lies:** food on counters, pans on hobs with what's in them, knives wherever they were put, dirty plates in the sink and on tables, stock in storage with its freshness. What was in a chef's hands goes on a free counter.

**How it works (proposed while building; flagged for review):**
- **The file is `campaign.json` in the persistent data folder.** It's written to a temporary file and then swapped in, so a crash mid-write never breaks the save. It holds:
  - the day and the money (which can be negative);
  - every station's position and angle, a table's chairs, and counted things such as clean plates and trays in their racks;
  - every item with its full cooking state;
  - the hired staff with their morale (fatigue rests overnight);
  - all reviews so far;
  - broken plates.
- **Saved once the day's results are in,** after wages, energy and staff morale.
- **A newer game's file is refused; an older one loads.** Missing fields take their defaults, and a numbered migration step covers anything a default can't. A file that can't be read starts a new campaign and logs a warning.
- **New campaign moves the old file aside** (`campaign.previous.json`) instead of deleting it. Nothing reads it; it's a safety net.
- **Quitting the built game in Plan, Prep or Service asks first:** today isn't saved. Stopping play mode in the Editor can't be caught, so it just loses the day.
- **Continue reloads the scene with the save,** carried by a small static (`CampaignLaunch`), like the earlier rejoin exception. Play tests save into a throwaway folder, never the real campaign.

**Not kept yet (TODOs):**
- what a member of staff is carrying when the service ends;
- cuts already made on a half-cut piece on the board;
- hob dial settings (hobs start the morning off).

The cookbook file comes with the cookbook in milestone 4.

**Reason:** One clear save point once the day is decided, with nothing lost or duplicated, so the restaurant really grows from day to day.

---

## 2026-11-08 — Phase 5: walk-to vendors, free prices, debt below zero, dirt from what happened

**User's calls (plan in `plans/phase-5.md`):**
- **Ingredients are bought at street stalls players walk to,** through the front door, carrying crates back. Going out during service costs real time.
- **Players set each dish's price freely,** in steps of 1, with a suggested price shown. Higher prices bring fewer customers, who expect more.
- **Money can go below zero before the boss exists.** It shows as owed to him on the dashboard, with no consequences until Phase 6.
- **Service dirt comes from what happened** (spills, burnt pans, customers eating, busy floors), so a chaotic service leaves a messier morning.

**Reason:** Each keeps decisions in the players' hands and lets them learn by playing: shopping is a trip that costs time, the right price is found from reviews, and the morning's mess reflects the service.

---

## 2026-11-08 — The current look is the reference for all future models and visual style

**Decision:** (the user's words) The direction the art has taken as of today is good, and it rules future models and visual style. That means the stylized models, the three-band toon ramp, the soft screen-space ink (a darker shade of what's underneath, mostly silhouettes, no hull outlines), and the banded contact shadows as they stand on main. New assets match this look. Changes to it are the user's call, made by playing here. They aren't delegated to the Art Director or another project.

**Reason:** The user played the kitchen after the softer ink and likes the result. A fixed reference keeps new assets consistent and stops the style drifting through outside hand-offs.

---

## 2026-11-08 — Plated food leaves the plate room to breathe

**Decision:** (the user's note) Food on a plate shows at 0.6× its size, relative to the plate (`ItemSizes.OnPlate`). The steak can be big on its own on a counter (about 1.42×), but plated it is smaller (about 1.26×, some 60% of the plate across), leaving room round it for a side, a garnish or a sauce.

---

## 2026-11-08 — Bug fixed: the fridge lid stood open at the start of the day

**Decision:** (the user's bug report) The chest fridge's lid stood open from the start of the day until a player opened and closed it. Its "last held open" frame started at `int.MinValue`, and "frame now minus that" overflowed to a negative number, which read as "held open just now". It now starts at a plain negative frame. A play test checks every fridge lid is shut and flat after the kitchen opens.

---

## 2026-11-08 — The dirty plate in the sink looks like the dirty plate

**Decision:** (the user's notes) The dirty plate in the sink wears the dirty plate's own grime, the same plate shader and dirt as a dirty plate carried in. Its material (`MAT_Plate_Dirty`) has the grime on, so it shows in the Editor too, and washing fades it as before. It lies nearly flat (tipped 12° towards the tap), 1.2× its size, just under the water with its far edge breaking the surface. A first try propped it up at 40° with separate food scraps on it; that read as a speaker cone and didn't match the dirty plate, so it was dropped. Each time a dirty plate goes in it lands differently: turned any way, tipped 6–16° in a random direction and shifted a few centimetres (`RestPoseShuffle`, the user's note).

---

## 2026-11-08 — An onion that doesn't read as a pumpkin

**Decision:** (the user's note) The onion was a bright orange ball with even dark ribs from pole to pole, a pumpkin's cues. Now it is:
- a muted copper-amber;
- a slimmer teardrop drawn up into a long pointed neck;
- five faint, uneven papery lines of different lengths;
- a longer curling dry tuft and wispy roots.
The halves share its new profile, and its menu icon is re-rendered.

---

## 2026-11-08 — A four-knife block with brass-collared slots

**Decision:** (the user's notes) The knife block holds four knives, up from three, and is wider (0.46 m) to fit them. Each slot is a wider dark opening framed by a brass collar standing proud of the face. Knives in the block show 1.5× their size, with more of the bolster and blade out of the slot. A full slot shows a big handle, and an empty slot a dark opening in brass, so it's clear at a glance how many knives are there.

---

## 2026-11-08 — Softer shadows under counters; the plate rack in light steel

**Decision:** (the user's notes) The floor under the counters went too dark, so the contact shadow is lighter (the ramp's AO step goes to 0.87, from 0.78) and so are cast shadows (sun shadow strength 0.45, from 0.6). The plate rack is the same light steel as the tray rack's frame rather than near-black.

---

## 2026-11-08 — Plating at the plate stack; plate piles with their rims showing

**Decision:** (the user's notes)
- **Food in hand at the clean plate stack goes onto a fresh plate** in one press, and the chef then holds the plated food. The prompt reads "plate it". It needs a clean plate on the stack, and works for food only, not a pan, plate or container, which hold their own (`ItemTransfer`, rule 8).
- **Plates up a dirty pile sit well off the one below:** half to all of 11 cm, before the counter's 1.4×, and tipped up to 8°. The rims of the plates underneath show, rather than only the top plate's coloured edge.

---

## 2026-11-08 — A dropped-looking plate pile; the knife block faces front; no cutting dial for the layout cursor

**Decision:** (the user's notes after playing)
- **A pile of dirty plates on a counter looks dropped, not stacked.** Each plate is nudged up to 5 cm and turned, and the ones up the pile tip up to 5°. The bottom one lies flat. Before, the plates above the first sat exactly on it and missed the plate size on counters, so they showed smaller than the bottom one (`CounterRestView` now counts a pile's levels as on the counter).
- **The knife block stands in the middle of its counter with its sloping face to the front.** It had been turned a quarter to show the handles full length; the user prefers it facing out.
- **Bug fixed:** with the knife in the chef's tool slot, the cutting dial showed under the layout cursor in management mode, for instance while placing a cutting board from the shop. There the chef's target is whatever the cursor is over, so the dial thought the chef was at the board. The dial now ignores chefs who are managing. A play test checks it shows at the board and not for the cursor; the test failed before the fix.

---

## 2026-11-08 — Steel sheet trays and gastronorm containers

**Decision:** (the user's references) The tray and container racks were piles of grey discs, and the items were flat boxes.
- **A tray is a steel sheet tray** (0.72 × 0.56 m, shallow, with a flat rim). The tray rack is an open steel frame with runners, holding its four trays one above another.
- **A container is a deep, tapered gastronorm pan** with a flat rim. The container rack is a stack of six, nested loosely: each one is nudged and turned a little, as if dropped back on the pile, not shaped exactly into the one below (the user's note).
- **Follow-up (the user's notes):** containers are rectangular and lower: 0.36 × 0.22 × 0.14 m deep. Deeper versions (0.32 and 0.20 m) and a near-square footprint were tried and found too tall. Portions start 6 cm up, as if the pan were part filled, so they show from above. The idle stack shows at 1.6×, about 0.58 m across and 0.42 m tall, using more of its counter so it's easy to spot.
- Food sits on the pans' floors. The racks lose their top tray or container first. On a counter, a tray keeps about its own size (0.75× on top of the counter's 1.4×), since it already spans most of the counter.
- `PanArt` builds them; without the models, the greybox shapes stay.

---

## 2026-11-08 — Volume from the sun; items sized by where they are; a bolder board and plate rack

**Decision:** (the user's notes after playing: things looked flat, the fridge lost its volume, and the steak, cutting board and plate rack were hard to notice at a glance)
- **The sun is high and from the left,** almost square to the camera (`HouseStyle.SunAngles`, 58° up, 96° round). Tops now land in the ramp's lit band, fronts in its mid band and right-hand sides in its shadow band, so every box reads as a box. With the sun behind the camera, tops and fronts were both fully lit, and only the removed hull outlines had told them apart.
- **Shadows fade on faces turned from the sun** (`ToonCommon.hlsl`). A face lit edge-on or from behind is already in its darker band, and at grazing angles the shadow map aliased into teeth, first seen on the fridge front.
- **Items show at a size that depends on where they are** (`ItemSizes`, the anchors' scale; an item eases between sizes as it moves):
  - on a counter or the cutting board: 1.4×;
  - carried: 1.3×;
  - on the grill: 1.15×;
  - fridge, shelves, plates and the pan: 1×.
  **Change from the choice:** food in a pan was to be 1.15×, but the steak is made to fill the pan, so the pan stays at 1×. The knife on a counter now lies at 1.15× on top of the counter's 1.4×.
- **Models evened out:** the onion is 0.8× and the steak 1.15× (`IngredientType.ModelScale`). **Change from the choice:** the steak was to be 1.3×, but then it overflows the pan. Later trimmed on counters only (`CounterScale` 0.88, the user's note): about 1.4× its old size there instead of 1.6×.
- **The cutting board is deep walnut** with a thicker rim (6.5 cm wide) standing 1.5 cm proud of the cutting surface. Food still sits on the cutting surface.
- **The plate rack and its plates show 1.25× bigger,** the frame is dark steel and plate rims are a stronger blue (on every plate).

**To check by playing:** whether 1.4× on counters crowds the counters; the size easing when items fly between places; the new light direction across the whole kitchen and dining room.

**Follow-up the same day** (the user's notes after seeing it):
- **The cutting board** fills most of its counter (0.88 × 0.75 m) and is a thick (7 cm), plain slab with rounded edges and a low lip (3 cm wide, 8 mm proud). The walnut has no grain. The deep rim and inset panel read as a picture frame, and the grain was distracting. Food sits on the 7 cm cutting surface.
- **Plates and prepared dishes on a counter** show 1.3× on top of the counter's 1.4×, about 0.58 m across, barely off centre (`CounterRestView._scale`). The plate rack stays 1.25×.
- **Food on a counter sits a little off centre (up to 9 cm) and turned** (`CounterRestView`), differently on every counter. The spot and turn come from the counter's position and the food's kind, so they're steady, and a round onion faces every which way. This applies only directly on a counter: not in containers or on the board.

---

## 2026-11-08 — Softer ink: thinner, a shade of what's underneath, mostly silhouettes

**Decision:** The user played the screen-space ink and found it far too strong ("it completely removes the fun"). They approved these changes to the brief below (relayed by the Main build session):
- **Thinner:** 1 px at 1080p and never more than 2 px (`_Thickness` 1, new `_MaxThickness` 2). The brief's 1.5 px scaled with screen height, so it came out at about 3 px on the MacBook's Retina display.
- **Coloured by what's underneath:** the line is the scene colour × 0.45 (`_InkShade`), with a quarter of the house brown mixed in (`_InkColorMix` 0.25), at half strength (`_InkOpacity` 0.5). It was the fixed #332117 at 0.92. Wood edges read as darker wood, and the white chef gets a soft brown-grey line.
- **Silhouettes, not creases:** `_NormalThreshold` 1.1 (was 0.6), so cabinet doors, counter lips and plank grooves mostly stop inking.
All of these are material properties on `MAT_Post_InkOutline`, set by `RenderLook`.

**Hull outlines removed everywhere** (the user's call, after being shown that the chef is barely outlined by the softer ink alone; keeping them on the chef only was the other option). The Outline passes are gone from `SH_Toon`, `SH_Plate` and `SH_FoodLayered`, along with their helpers in `ToonCommon.hlsl`, the per-material widths (thinner on plates and wires) and the debug toggle. Materials still carry `_OutlineColor` and `_OutlineWidth`, which nothing reads now; they can go in a later material clean-up.

**To check by playing:** whether the chef stands out enough without its own outline.

**Smoother ink lines** (the user's note after playing: "some outlines still look pixelated"; fix suggested by the Main build session). The depth and normals buffers aren't anti-aliased, and SMAA mostly misses a soft line, so each pixel was either inked or not and diagonals stair-stepped. Two changes:
- The edge test is averaged over four points a quarter pixel apart (`_SubSamples`, on). This gives partial coverage along diagonals. It costs four times the taps: 32 instead of 8 per pixel.
- The fade-in ramps are wider: threshold to 2.5× it (`_EdgeRamp`), where it was 1.5× for depth and 1.3× for normals. Weak edges now fade in instead of switching on.
In 1080p renders the improvement is modest, mostly on thin diagonals like the plate rack's wires. If the GPU cost shows, `_SubSamples` 0 keeps only the wider ramps.

---

## 2026-11-08 — Screen-space ink, three-band ramp, banded SSAO

**Decision:** (the art director's brief, approved by the user)
- **Ink outlines drawn over the screen** (`SH_InkOutline`, `MAT_Post_InkOutline`, a Full Screen Pass before post-processing). They come from depth and normals at a constant on-screen width (1.5 px at 1080p), on every opaque surface, room surfaces included. The ink colour comes from `HouseStyle.OutlineColor`, so it has one source.
- **The old inverted-hull outlines stay for now.** At the kitchen shot they're about half a pixel wide. *Tools > Game > Debug > Toggle Hull Outlines* turns them off and back on, each material keeping its own width, so the two can be compared. They will be removed once the user approves.
- **A three-band toon ramp** (lit, mid, shadow) in `ToonCommon.hlsl` replaces the two-band step and each material's `_ShadowTint`, which is retired. The sky light is lower (0.12).
- **SSAO, banded by the ramp into one flat contact shadow:**
  - Blue Noise, intensity 1, radius 0.25, falloff 30, depth normals;
  - normal quality, samples and blur as below;
  - no After Opaque, direct lighting strength 0.
  **Deviation from the brief:** the brief asked for a Medium blur at half resolution. That left grainy, speckled fringes once the ramp banded the noisy AO, so the AO runs at full resolution with the High (bilateral) blur. This costs more GPU; if frame time is tight, going back to half resolution is the first thing to try.
- **Shadows:** 25 m in 2 cascades (split 0.4), low soft-shadow quality, depth and normal bias 0.6. No acne on counter tops in the renders.
- **The camera clears to a near-black warm brown,** #120D0A.
- The pipeline settings live in `RenderLook`, which the kitchen build applies (*Tools > Game > Apply Render Look*).

**Reason:** At the overhead kitchen shot the hull outlines barely showed. Screen-space ink holds a steady width and reaches the room's edges. Three bands and contact shadows separate tops from fronts.

**Still to check:** 60 fps with two players on the MacBook. SSAO and the full-screen pass are the main cost; check with the Stats panel or the Profiler in the Editor.

---

## 2026-11-08 — An onion cuts into rings, then on into dice; a clearer knife

**Decision:**
- **An onion cuts in two steps** (the user's note): two chops make **onion rings**, a new ingredient. Three more chops on the rings make dice: five in all, as before. To keep rings, stop and take them off the board. The board keeps cutting what it just cut whenever that can be cut further, with a fresh cutting dial for each step.
- **Rings cook like dice but a little slower**, being thicker. This is starting tuning, copied from diced onion and not yet tested in play. No dish asks for rings yet (`ideas.md`).
- **The prep assistant follows the chain:** asked for dice, it fetches an onion and cuts it through rings into dice.
- **The cut shows its stage on the board:** the onion's first chop leaves two halves lying cut face up; then the fan of rings; each chop into the rings pushes fewer rings aside as the dice pile up; then the dice heap.
- **A knife put down on a counter lies across it, diagonal and 1.6× its size**, so it reads from above. At its real size, pointing straight away, it was a sliver on a whole counter.
- **The sheath only shows while the chef has a knife**, so you can tell at a glance whether a chef is carrying one (the user's note). This replaces "the sheath is there even without a knife" from the entry below.

**Reason:** The user's notes from playing: an onion should go from halves to rings to dice, with stopping for rings; a knife on a counter was hard to see; and it should be clearer whether a chef has a knife.

---

## 2026-11-08 — The knife rides in a sheath; an onion comes apart as it's cut

**Decision:**
- **A leather sheath on the right hip**, a little behind, holds the knife blade-down with only its handle and bolster showing (the user's choice). It replaces pushing the knife into the belt, which read as stabbed into the thigh. The knife still comes out only facing the board with empty hands. The sheath is part of the chef, so it is there even without a knife.
- **Cutting shows its progress** on the board (the user's choice):
  - the first chop splits the onion into two halves lying cut face up, their rings showing;
  - each later chop piles up more dice beside them, and one half goes before the last chop;
  - the last chop leaves the cut onion's heap;
  - bits of onion fly with every chop.
  It's only looks (`CutProgressView`); the cut itself is unchanged.
- **A cut onion is a heap of chunky cream dice** everywhere it shows: on the board, in the pan (browning as one), on a plate and in its menu icon. It used to be a pale flattened ball.
- **Rings on a first pass and dice on a second** stay an idea for later (`ideas.md`).

**Reason:** The user's notes: the knife looked stabbed into the thigh, and the onion cut should be more visual.

---

## 2026-11-08 — Chef arms that act out the work; a real chef's knife; the knife rides in the belt

**Decision:**
- **The chef has arms**: chunky sleeves with mitten hands, hanging from the shoulders and turning about them (`ChefArmsView`). They swing as the chef walks and blend between poses. Hands only reach to points just in front of the chef at counter height, so they don't reach into walls or furniture.
- **A pan is carried by its handle**: both hands side by side on it and the pan out in front, after the user's reference. The pan's handle is longer so both hands fit. Other carried things are gripped with a hand on each side.
- **The knife rides in the belt** at the right hip, handle out so it shows from above (the user's note). It only comes out facing the cutting board with empty hands. There it is held pointing away from the chef with its edge down, while the other hand steadies the food, after the user's reference. Each cut is a fast chop.
- **A flip at a hob tosses the pan**: it lifts and tips, with one hand on its handle. The other hand keeps hold of anything carried, such as a plate (the user's references and note). At a grill there's no pan, so a flip is a flick of the hand.
- **Washing** holds the plate in the left hand and scrubs it in circles with the right, after the user's reference.
- **The knife is a chef's knife** at a cartoon but believable size: a 0.26 m blade whose spine runs straight to the tip, a steel bolster, and a black handle with silver rivets. The big 0.36 m blade read as a sword.
- **Stations report what they did:** the chef's interactor raises `Acted` when a station accepts a press (a chop or a flip), which is what the arms act out.

**Reason:** The user's notes: a carried pan was hard to understand, the knife looked like a sword, the knife should sit in the belt until the board, and cutting, washing and flipping should show on the chef.

**Still to check by playing:** how the chop, toss and scrub feel in motion; that hands never pass through a counter in any layout or facing; the belt knife from every camera angle.

---

## 2026-11-08 — A big cartoony knife, an angled knife block, an onion, and bigger held items

**Decision:**
- **The knife** is big and cartoony (after the user's chef reference): a 0.36 m blade, wide and curved to its point, and a chunky dark wooden handle with brass rivets.
- **The knife rack** is an angled wooden block, after the user's reference. The knives stand in three slots in its sloping face, a little blade and the whole handle showing. The block stands turned a quarter, its slope to the side, so the knives lean across the overhead camera's view; facing the camera, their handles were foreshortened to stubs.
- **The onion** is a coppery bulb with a pointed neck, a few fine stripes, a dry tuft and roots, after the user's references. It lies on its side with its neck tilted up; from above, a round striped top read as a pumpkin. It shows wherever an onion does: in hand, on a board, in the shelf baskets and in its menu icon.
- **What a chef carries shows 1.3 times bigger** (the carry and tool anchors), so a carried pan or knife reads at a glance. It's back to its own size once put down.

**Reason:** The user found the props so realistic in proportion that it was hard to tell a chef was carrying a knife, or even a pan.

---

## 2026-11-08 — A full bin refuses more, with a 🚫 over it

**Decision:**
- **The bin's bag fills after 8 things** (`BinStation._capacity`, was 24 and only for the look). A plate scraped into a nearly full bin keeps what didn't fit.
- **A full bin takes nothing more.** A red 🚫 sign pops up over it, bobbing, and rocks when a player with something in hand faces it. Players have to leave food somewhere else (`BinFillView`).
- **A prep assistant** with cut food and a full bin keeps hold of it and waits by the bin; it's dealt with when they're let go at the end of the day.
- **For now a fresh bag goes in each morning** (start of Plan). Later, once the restaurant's outside exists, players take the bag out to street containers round the building (docs/ideas.md).

**Reason:** The user's notes: the bin should fill with less, and once full, a small sign should show and players must put things elsewhere. Supersedes "for now it only fills, and being full changes nothing in play" in the 2026-11-08 sink and bin entry.

---

## 2026-11-08 — 4x MSAA on the PC pipeline

**Decision:** The PC pipeline asset (`Assets/Settings/PC_RPAsset.asset`) renders with 4x MSAA, and the game camera adds SMAA (High) on top. SMAA smooths the steps inside surfaces too, such as toon light bands and thin lines. SMAA needs the camera's post-processing on, so the PC pipeline no longer carries the template's sample effects profile (bloom, vignette, tonemapping); post-processing adds nothing else. The mobile asset is unchanged.

**Reason:** The user found the shapes' edges pixelated and noisy to look at. MSAA smooths geometry edges and the toon outlines, which are geometry too (an inverted hull). It's cheap for a scene this size, within the 60 fps target on a MacBook with two players. Still to check in play.

---

## 2026-11-08 — Effects carry the fun: bubbles, fog, sparks and flies

**Decision:** Effects are a deliberate layer of fun and liveliness in the scene, not understated touches (the user's note).
- **Washing:** a few big soap bubbles (a shiny ring texture, pastel tints, about five a second) drift off the sink in every direction, towards the chef too, then wobble, swell and pop. Splashes are chunky droplets thrown well up.
- **The fridge:** opening it releases a short breath of cool fog, nothing while it stays open. The fog is dense and pushed out over the floor to where the chef stands, so it wraps round them. The fog uses a texture that's white all over, fading only in its alpha (TEX_Puff). The built-in particle texture's darkening edges showed as a dark shade.
- **Cooking:** sparks are big, bright and plentiful from middling heat up (`HobView._emberRate`).
- **The bin:** once it's past half full, flies circle it, more the fuller it gets (`BinFillView`).
- **The sink basin** is deeper (0.23 m), with walls darker than its floor and the water at 0.79 m, so its depth reads from the camera. Under it the cabinet's body stops at 0.66 m.

**Reason:** The user's notes that these effects should be noticeable and funny, and that the sink still lacked visible depth.

---

## 2026-11-08 — A deep sink with water, a bin that fills, plain dining furniture, a less yellow scene

**Decision:**
- **Dining furniture** (tables, the bar, chairs and stools) is plain wood colour, without grain.
- **The sink** has parts in their own tones so its shape reads: a light steel top, a raised rolled rim round the opening, a darker basin, a dark drain and a chrome tap. It's always half-full of see-through blue water, so its depth shows, with the plate being washed visible under it.
- **While anyone washes** (chef or dishwasher), water runs from the tap onto the water with splashes, and soap bubbles float on it (`SinkWashView`). It's seen from the wash progress moving and lingers a moment after.
- **The bin** is an open tub lined with its black bag, so it looks deep and empty. A rubbish mound rises up the bag with each thing thrown away (`BinStation.ThrownAway`, `BinFillView`), full at 24.
  - For now it only fills, and being full changes nothing in play.
  - Taking the bag out comes with the restaurant's outside (docs/ideas.md).
- **The scene is less yellow:** a near-white sun, neutral sky light, slightly cool shadows, and cream walls and countertops instead of yellow ones. The wood keeps its warmth.

**Reason:** The user's notes on the dining furniture, the sink, the bin and the overall colour.

---

## 2026-11-08 — Gas hob: fire is the only heat cue; soft gas flames; a grey knob with a red pointer

**Decision:**
- **Heat lights:** the gas hob has none; its heat shows only in its fire. The induction hob keeps its three heat lights, lit green, orange and red.
- **The gas hob's top** has three greys: the rim lightest, the cooking surface a step darker, the grate almost black.
- **The fire** is soft and see-through (`Game/FX/Flame`: additive, fading at its tip and silhouette), after the user's photo and reference image.
  - 16 thin tongues in a ring at the pan's edge, so even a low flame peeks out from under the pan, with an inner ring of short blue cores.
  - Blue at low and middling heat. Past about a third of the dial the tongues turn yellow at the base and orange at the tip, the orange glow and embers appear, and the cooking surface glows round the burner.
- **The knob** is dark grey with a red pointer stripe, the only red part. Off, it points to the top of the hob; it turns clockwise through 270° to full heat, like a real gas knob.

**Reason:** The user's notes on the hob, the fire references, and the knob being hard to pick out.

---

## 2026-11-08 — A grate with presence, layered fire, and counters that join

**Decision:**
- **The range's top** is a steel rim round a recessed black spill tray, so the grate has room to show. The rim is wide at the front as a control strip, where the knob now sits, and at the back, where the heat pips sit.
- **The grate** is heavy cast iron: a thick frame with corner knuckles and feet, four thick arms on the diagonals, and pan supports whose tops are the pan's resting height, 0.91 m.
- **The fire** mixes cartoon and realistic. Orange tongues lean out past the pan, with shorter yellow-hot cores between them. Soft additive flame puffs rise from a ring just outside the pan, and embers drift off. All of it grows with the heat (`HobView`), and embers only really start at higher heat.
- **Counters right beside each other join into one continuous top.** Each top is rounded along sides open to the room and square and flush where it meets another counter, chosen from 16 versions of the top (`CounterJoins`, tested). Cabinet bodies and bases run the full width, so they touch. It's worked out in each counter's own axes and follows the layout editor. Only counters join: hobs, sinks and the rest keep their own edges.

**Reason:** The user found the thin grate sluggish and without personality, wanted fire that mixes cartoon and realistic effects, and wanted side-by-side counters to read as one surface.

---

## 2026-11-08 — Notes on the overhaul: plain tops, a snug round bin, flames and a grate

**Decision:**
- **Countertops** are plain: one pale colour, no grain.
- **The bin** is a round grey tub with a black bag over the rim, about 0.54 m across. It keeps its 1 m slot, so layout and gameplay are unchanged. Within the slot it slides to sit snug against whatever is beside it: a wall or another station, into the corner if there are two, and centred if it's boxed in on opposite sides (`SnugFit`). It re-checks every frame, so it follows when players rearrange the kitchen. Its collider still fills the old 0.7 m block.
- **The gas hob** has a cast-iron grate whose tops are at the pan's resting height (0.91 m), over a small burner head. A ring of twelve flame tongues leans out from under the pan. They're off at zero heat, then grow, widen and flicker with the dial, going from blue at the very bottom to orange. At high heat they lick well past the pan's edge, so heat reads at a glance, like the user's reference.
- **The chefs' player colour** is on the apron and neckerchief. The rest of the chef stays white jacket, dark trousers and toque, so co-op players stay easy to tell apart.

**Reason:** The user's notes and reference images on the overhaul. Earlier the grate was left out because the old wide burner ring sat at the pan's height; a small burner head under the grate removes that clash.

---

## 2026-11-08 — Art overhaul toward the vision image

**Decision:** The scene is restyled toward the user's vision image, one step at a time:
1. Warm light, warm shadow sides and thin dark-brown outlines.
2. The room: tiled kitchen floor, planked dining floor, panelled cream walls.
3. One wooden counter family.
4. Restyled range, sink, fridge, bin and racks.
5. Dining furniture and wall details.
6. Person-shaped chefs.

Each step's palette and light settings live in `HouseStyle` (Editor), which the kitchen builder applies to the scene and to every toon material.

- The fridge stays a chest with a glass lid, so stock shows from the camera, but gets a white enamel look. The image's tall upright fridge would hide its stock again.
- The kitchen floor is a mid-tone warm grey tile, a step lighter than the image's dark slate, so dark pans and food stay visible on it.
- Countertops stay light wood, so plates, food and the cutting board stand out.

**Not in scope:** the HUD and build menu shown in the image, which belong to the UI work, and the camera angle, which belongs to the Camera session. The image's camera is higher and closer to isometric.

---

## 2026-11-08 — Kitchen-real colours and counter-sized proportions

**Decision:**
- **Sink:** the tap stands at the model's back. The greybox layout turns the sink so its back is against the wall, and players turn it themselves when they move it. Nothing works out which wall is nearest. The basin is wider and fills most of the counter.
- **Hob:** stainless steel (light steel body, charcoal top) instead of red. One big light-steel heat knob sits on the top's front-right corner and turns clockwise with the heat, up to 270°, next to the heat pips along the back. The front is plain steel: the dial and oven window that were there read as unexplained circles.
- **Fridge:** the four baskets are steak-sized (0.32 × 0.26 m) and sit towards the middle (±0.18, ±0.15).
- **Plate rack:** plates stand upright again, as in the first design. The rack has thin rounded light-steel wires and short tines between the plates. Plates get a soft blue rim, and plates and wires get thinner outlines.

**Reason:** The user's references show appliances that are real-coloured and fill their counter the way real ones do, with detail in lines rather than in thick shapes. The exaggerated rack and the red hob read as toys.

---

## 2026-11-08 — Style test on the hob; notes on the sink, fridge, board, plates and rack

**Decision (user's calls):**
- **A style test before restyling everything.** The gas hob, its pan, the steak, plates and the board get toon shading: flat bands of light, a cool shadow tint, a soft rim light and a dark outline. They also get a bold palette, with a red enamel hob, a dark top, a cream dial and a teal oven window. The hob is reshaped: one huge dial and a big round oven window instead of three knobs and a door. The pan is mostly rim and a stubby ball-ended handle. Everything else keeps its current look until the direction is agreed.
- **The sink's tap rises from the middle of its back edge** and arches forward over the basin, so water always comes from the back of the sink, whichever way it's turned. The sideways lever that read as a spout is now small, at the back.
- **The fridge's four baskets are small and gathered in the middle,** so its stock shows from every side.
- **The cutting board is big,** 0.7 × 0.55 m, using most of its counter, with wood grain drawn by its material.
- **Dirty plates show a texture, not shapes:** short broken wipes, splatter, a faint tide mark, grease and crumbs, in dull browns and oranges. Solid stains could be mistaken for meat.
- **Plates and the plate rack are toy-like and exaggerated:** a thick plate with a fat rolled rim, and a rack of fat round bars.

**Reason:** the earlier pass was chunky but still realistic. Shading, outlines and colour do most of the cartoony work from the overhead camera, so they're tried on one station first.

**Not verified:** how it reads in play. The grime is subtler than before, so check it from the game camera.

---

## 2026-11-08 — A cartoony style for every model, and dirty plates that look dirty

**Decision (user's calls):**
- **Models are exaggerated and cartoony, not realistic,** so they read in a rush: chunky shapes, fat rounded edges, oversized details such as knobs, handles and the tap. The reference photos guide what something is, not its proportions. Sizes the game relies on stay exact: footprints, the counter height, where pans and plates rest, hinges, slots and how far the fridge lid swings.
- **This covers every model now:** hob, pan, steak, fridge, shelf, chef, plates, plate rack, sink and board. The chef is a bean shape with a white toque. Plates, the board and the steak are thicker.
- **Dirty plates show their mess:** a sauce puddle with a smear dragged from it, tomato red or gravy brown, greasy patches and clusters of crumbs. Each plate looks different, and gets a new mess every time it's used. A plate in the sink visibly comes clean as it's washed.

**Reason:** in a frantic kitchen, things have to be recognised at a glance from the overhead camera, which exaggerated shapes and a messy-looking dirty plate do better than realistic ones.

**Not verified:** how the chunkier shapes and the grime read in play, and whether the toque crowds the camera when chefs stand close together.

---

## 2026-11-08 — The sink only washes; dirty plates pile on any counter

**Decision (user's calls):**
- **The sink washes one plate at a time and keeps none.** Drop a dirty plate in and wash it, and it comes out clean in your hands, ready to carry to the plate stack. The cleaner works the same way. Replaces the sink holding 2 plates, dirty and clean (2026-11-04).
- **The dirty pile station is gone.** Dirty plates pile up to 3 on any counter. A clean plate, food or anything else can't go on a pile, and a pile only takes dirty plates. Where the pile stood, by the sink, is now an ordinary counter.
- **The waiter** clears stools onto counters, onto an existing pile before an empty counter, so plates gather in a few piles. With nowhere to put a plate, they leave the stool for now.
- **The cleaner** washes whatever's in the sink first, then clears stools, then collects plates from counter piles. They take each clean plate to the stack. If the sink's busy, they pile the plate they're carrying on a counter.

**Reason:** the sink becomes a quick station that players pass through, and dirty plates become visible clutter wherever they're left. That makes falling behind on washing up something you see across the kitchen.

**Details (proposed while building):** a plate dropped with nowhere to go breaks, and is replaced in the morning like other broken plates. The pile limit and spacing are settings on each counter.

---

## 2026-11-08 — Models for plates, the plate stack, the sink and the cutting board

**Decision (user's call):** these get Blender models, following the user's reference photos. Only meshes change: footprints, anchors, slots, colliders and rules stay as they were.
- **Plates** are white with a well and a gold rim, everywhere a plate appears.
- **The plate stack** is plates standing upright in a black wire rack, facing the camera. The front plate is the first to go.
- **The sink** is a stainless unit with one big basin, a swan-neck tap, open legs and a slatted bottom shelf. Its drainer went when the sink stopped keeping plates (see the entry above).
- **The cutting board** is bamboo with rounded corners and a juice groove, without the photo's logo. It's lighter than its counter so it stands out.

**Reason:** it brings the kitchen's most-used stations up to the look of the hob and storage, without touching how they play.

---

## 2026-11-08 — The fridge is a chest at counter height with a glass lid

**Decision (user's call, after play; details proposed):** a tall fridge with two ingredients per shelf hid stock and blocked the view, so it becomes a chest fridge, like a counter.
- **Counter height (0.9 m), with all four slots on one level,** the chest's floor at 0.7 m. They copy the face buttons' diamond seen from above: Y and B at the back, X and A at the front.
- **A glass lid in a white frame covers it,** so the stock shows from above whichever way the chest is turned. The sides are solid white, with a pale lining inside.
- **The lid lifts while RT is held:** hinged along the back, it opens to 75° in 0.25 s with a small overshoot, never past upright, and closes in 0.3 s. It stays over the chest's own footprint, so no wall, furniture or chef can be in its way. Being glass, it hides nothing even when it stands on the camera's side. Tunable on `FridgeDoorView` (`_openAngle`, `_openSeconds`, `_closeSeconds`, `_overshoot`). The cold air spills over the front edge as it opens.
- **The chef bends at the waist to reach in,** as at the shelf.
- **Replaces** the glass fridge from earlier today: its sliding doors front and back, its glass sides, and the rule making sides glass or solid. The shelf stays as it is (below). The 2026-11-07 entry's "tall storage in the back-left corner" now holds only for the shelf.

**Reason:** one level under a glass top shows all four ingredients at once from the overhead camera, in any layout, and a lid that only rises can't clash with anything around it.

---

## 2026-11-08 — Stock shows from any side: a glass fridge and an open shelf

**Decision (user's call, details proposed):** players can turn storage any way, so what's in it should show from every angle.
- **The fridge has a glass top and glass shelves,** in a white frame of corner posts, a solid base and a rim round the top.
- **Each of its four sides is see-through, unless it's right against a wall and doesn't face the camera** (user's call, revised after play). A wall within 0.2 m of a side makes it solid white (`_reach` and `_gap` on `FridgeShellView`).
  - **Furniture never counts:** the fridge shows above a counter beside it. Neither do people or loose items.
  - **The side facing the camera** (south, within 45°) **stays see-through even against a wall,** because the camera looks over the low walls into the fridge.
  - It checks every frame, so it follows the layout as pieces move, even while the fridge is carried.
  - The first version also made sides solid against furniture and against walls on the camera side. In play, that hid stock that should have shown.
  - **Open, the left and right sides are glass, and the front and back are sliding doors.** The back door is the front one turned round.
  - **A chef opens the door on their side,** and the half in front of them slides over the other, as before. If the door on their side is solid, the other one opens. The cold air puffs out of whichever door opened.
- **The shelf is an open rack:** shallow boards 0.4 m deep (were 0.7 m), centred. The upper tier's board is its top, with no empty posts above it (user's call, after play), so it's 1.05 m tall (was 1.5 m). From the camera's 55° view, the upper board no longer hides what's on the lower tier.
- **Stock sits in the middle of both,** where it used to sit towards the front. It's then the same from the front, the back and either side.
- **Replaces** the fridge's solid sides and back, its pale lining, the shelf's deep boards, its top board and its 1.5 m height, and slots sitting towards the front.

**Reason:** with every piece placeable and turnable, any side might face the camera. Stock that only shows from the front would hide what's in storage for half of the possible layouts.

**To check in play:** whether the glass and the shallow boards still read clearly as a fridge and a shelf from the overhead camera.

---

## 2026-11-08 — The cutting board is a counter of its own

**Decision (user's call):** the cutting board works like the knife rack. It comes built into its own counter, and the two can't be separated.
- **One piece:** it's bought, carried, placed, rotated, moved and sold like any counter. The last board still can't be sold.
- **Price 50:** a counter (30) plus the board (20).
- **The starting kitchen's board** is now this piece, where its counter stood.
- **Controls unchanged:** A puts food on the board or takes it off, and X cuts with the knife.
- **Replaces** the 2026-11-07 rule that boards sit on counters and move from one free counter to another. Nothing sits on a counter any more, so the "it goes on a free counter" placement check is gone.

**Reason:** the same as the knife rack. Every piece is placed the same way, so the board is a clear layout choice with nothing extra to learn.

---

## 2026-11-08 — The fridge has sliding glass panes

**Decision (user's call, after play; details proposed):** the fridge's hinged door becomes two half-width glass panes on two tracks across its front, like a drinks fridge.
- **The left pane runs on the back track, the right one in front.** Each covers half the opening plus a 4 cm overlap.
- **Opening slides the half in front of the chef:** the left pane slides right behind the other, or the right pane slides left in front of it. The side is chosen when it opens from shut, by whoever opened it.
- **It never leaves the fridge's width,** so walls, furniture and chefs can never be in its way.
- **Motion:** 0.2 s to open and 0.25 s to close, eased in and out with no overshoot. A pane on a track glides and stops. The cold mist and the open and close sounds are unchanged. Tunable on `FridgeDoorView` (`_slide`, `_openSeconds`, `_closeSeconds`).
- **Replaces** the 2026-11-07 hinged door: checking for room to swing, choosing a hinge side, and chefs stepping aside, along with the menu keeping its station while a chef shuffled. Also replaces "the door hinges on the left" in the tall-storage entry.

**Reason:** a swinging door has to share floor with chefs, and moving them out of the way took control from the player. A sliding door needs no floor, so it works the same in every layout.

---

## 2026-11-08 — The knife rack is a counter of its own

**Decision (user's call, from playtest feedback):**
- **A knife rack comes built into its own counter, and the two can't be separated.** It's bought, carried, placed, moved and sold as one piece, like a counter. It sells only when empty, like any piece.
- **Price 55:** a counter (30) plus the rack (25).
- **A takes and hangs knives at it**, as before. Food can't go on it.
- **A bought knife hangs on a rack with room** before going to a free counter. This replaces "a bought knife appears on the first free counter" (2026-11-07) for kitchens that have a rack.
- **Replaces** the 2026-11-07 rule that a knife rack sits on a counter like a cutting board. Boards still do.

**Tried and dropped the same day:**
- **Sharing the board's counter, with the board answering A for both.** The controls got complicated, and real 3D knives laid flat would have crowded the counter.
- **Putting a bought rack on the free counter nearest a board.** Building the rack into its own counter is simpler, and placing it is the players' choice, like any counter.

**Reason:** a rack fused to its counter is placed like everything else, so it's a clear layout decision, with no fixtures to manage.

**For the art phase:** model it as an upright knife block on its counter, which reads better from the overhead camera than knives lying flat.

---

## 2026-11-07 — Playtest fixes: cooked food in storage, furniture landing on chefs, HUD on screen, fridge door

**Decision (from playtest feedback; details proposed):**
- **Cooked food gets its own storage slot.** A slot holds one ingredient in one state: cooked (any doneness level or any browning) never shares a slot with raw stock. Before, a cooked steak joined the raw steaks' slot and, taken oldest first, could never be reached, while the bin only showed raw steak, so it seemed to vanish. Its own view stays in its bin, and the RT menu says "cooked steak". With no free slot, the fridge refuses it, as when full. Staff never fetch cooked food as stock.
- **Furniture never lands on anyone.** A piece being carried in management mode is a preview without collision, so it can't shove anyone. It can't be put down within a body's own radius plus 0.1 m (`_personalSpace` on `LayoutEditor`); before, the check used 0.3 m against a 0.35 m chef. A piece sent back home by Cancel may land on someone who walked into its spot: they step out to the nearest free floor. As a safety net, a chef who ends up 1 m below the floor is put back on the nearest free spot.
- **HUD cards always stay on screen.** Every world-anchored card (shop list, tickets, food tags, review popups, the chop dial and flip gauge) slides back inside the screen, 12 px from the edges (`ScreenMargin` on the HUD style). Prompt cards already did.
- **The fridge door knows its surroundings.** Each time it opens from shut, it checks how far it can swing before touching walls or furniture, hinges on whichever side has more room (left unless the right has 10° more), and eases to a stop 4° short of anything in the way, without overshooting into it. This replaces "the door hinges on the left" in the fridge entry below.
- **The chef who opens the fridge steps aside (user's call, after the door went through the chef).** If they're standing in the door's swing, they shuffle at 3 m/s to just past the door's free edge, no closer to the fridge (backing off just enough to clear the door if they were nearer) and turning to face it. The door holds until they're clear, then follows. The stick cancels the shuffle at once. If there's no free floor there, the chef stays and the door only opens as far as they allow. Anyone else in the swing holds the door back too, and it gives way at once to anyone who walks into it. While the storage menu is held, the chef stays on that station as long as it's in reach, so the shuffle can't switch them to the shelf next door. Tunable on `FridgeDoorView` (`_stepAsideSpeed`, `_stepClearance`, `_followSpeed`).

**Reason:** these all broke the game's readability or let a player lose a chef for good. Animations have to look right wherever players put furniture, since layouts are theirs.

**Not verified:** all of it needs checking in play; a fridge hinged on the right is the door model mirrored.

---

## 2026-11-07 — Tall glass fridge and shelf, ingredient pictures, chefs bend at shelves

**Decision (user's calls):**
- **Storage shows its stock.** The fridge is an upright unit, 1.8 m tall, with a white-framed glass door, and the shelf is a tall open unit, 1.5 m. Each holds two ingredients on each of two tiers, seen from the front, so players can read what's stored from the furniture itself.
- **Slots follow the tiers:** Y and B on the top tier, X and A below, left to right. This replaces the diamond on the counter top.
- **The RT menu shows pictures.** Each slot's line shows the ingredient's picture and count beside its button, rendered from the game's own food models, and falls back to its name if there's no picture.
- **While a chef holds RT:** the fridge door swings open with a puff of cold air, and at a shelf the chef bends forward at the waist. The door raises events for a door sound, which the sound session adds.
- **The fridge and shelf stand in the back-left corner** of the starting kitchen. The knife counter moves to the west wall by prep, and the tray rack to the back wall beside the hob. The rest stays put.

**Reason:** tall furniture hides what stands behind it from the overhead camera. On the west wall, the shelf would have hidden the fridge's door. The back wall faces the camera, with open floor in front.

**Details (proposed and accepted):**
- The door hinges on the left, so the open door swings out beside a chef standing in front, not into them. Two players at one fridge keep it open until both let go.
- The chef is now two placeholder parts, legs and a torso hinged at the waist. It isn't character art: it keeps the same height, colours and nose.
- A prompt card sits above a tall station rather than over it, so it doesn't cover what's inside.
- Stored steak shows as the steak model, and pictures are rendered by Tools/Game/Art/Render Ingredient Icons. A kitchen build renders only missing ones.

**Trade-off:** a chef standing at a unit covers part of it from the camera; the pictures in the RT menu make up for that. Tests that used the back-right gap as open floor now use the floor between the island and the pass.

**Not verified:** the mist doesn't show in still captures, so it needs checking in play.
---

## 2026-11-07 — Kinds of heat and kitchenware replace equipment tiers

**Decision (user's calls):**
- **Appliances differ in kind, not tier.** Instead of basic and better versions of the same hob, there are different ways to cook: a gas hob, an induction hob and a charcoal grill now, ovens later. Any of them takes a pan. Fridges and shelves stay as they are for now.
- **Gas hob:** works as today. A dial, and the pan takes time to heat and cool, so players have to be precise.
- **Induction hob:** three heat steps. The pan gets hot or cold as soon as the step changes.
- **Charcoal grill:** customers ask for one when the restaurant specialises in meat (for example, a review asking how a steak place has no flames). No appliance changes the quality score: what differs is how they cook, not how good the result is.
- **The charcoal grill takes two floor slots:** a hot zone and a cool zone, each holding up to two pieces directly on the grate. A pan or pot takes a whole zone, and X flips what's in it, as on a hob. In the open, two players can cook from opposite sides at once; against a wall, one chef works it alone. Either way, a chef acts on the zone they face.
- **Cooking on the grill:**
  - **Placing:** food goes on whichever zone the chef faces. Starting on the cool zone gives a weaker crust, which lowers quality through the crust score.
  - **X flips the food and moves it to the other zone,** so the chef steps across to follow it. Each press sends it back again, changing how fast it cooks and how it turns out.
  - **No room:** if the other zone already holds two pieces, the food can't be flipped until there's space.
  - **A** takes the food off.
  - **Two pieces in a zone:** A and X act on the hotter one.
- **The grill is always lit,** with nothing to light or keep going. Its glow is part of the room's look, so it's decoration as well as an appliance.
- **Energy is an expense in the end-of-day summary,** next to wages. Electricity is cheapest, then gas, then charcoal and wood.
  - *Proposed, to confirm:* hobs cost by how much heat they used; the always-lit grill costs a flat amount a day; fridges aren't charged for now.
- **Cutting boards and knife racks sit on counters,** and each uses up the counter it's on. They're moved only in management mode, and bought in the shop as many as wanted.
- **Knives are items.** The kitchen starts with one knife lying on a counter and no knife rack; picking it up frees the counter. Chopping needs a knife, so nobody can chop until it's put down somewhere. A bought knife appears on the first free counter.

- **Staff chop without the knife (user's call):** a hired prep assistant cuts by skill and doesn't need the kitchen's knife, the one exception to "nobody chops without it".
- **The grill cooks like a hob at fixed heats (user's call):** the hot zone sears and cooks fast; on the cool zone food cooks more slowly and is much harder to burn, so finishing there is comfortable.
- **Only the gas hob catches fire;** induction and the grill don't. Putting a fire out works like cleaning (§15.8): hold X and the chef pulls out an extinguisher, as they would a mop or a cloth, then a short stick game.
  - *Confirmed (user's call):* a pan left on a lit flame after its food has burnt catches fire a few seconds later, so the smoke alarm is the warning. While it burns, the hob and pan can't be used and the food is lost. Left too long, the hob is ruined and has to be replaced. It doesn't spread for now. The extinguisher is always to hand, with nothing to buy. Built with cleaning in Phase 5, as it shares the stick game.

**Details proposed while building (flagged for review; tunable in `EquipmentTier` and `FurnitureCatalog`):**
- **Induction steps:** low 130 °C, medium 190 °C, high 250 °C; the pan gets there in about half a real second. Off lets the pan cool on its own, like a pan lifted off. Each pull of a trigger is one step.
- **Grill zones:** hot 260 °C, cool 150 °C (below where browning starts, so food finishes there without more crust).
- **Prices:** induction hob 150, charcoal grill 250, cutting board 20, knife rack 25 (holds three knives), knife 15, pan 25. The gas hob stays 80.
- **Energy:** electricity 0.60 a kWh, gas 1.00 a kWh, charcoal 15 per grill per day.
- **What can't be sold:** the last hob of any kind (so the gas hob can go once there's an induction hob), and the last cutting board (a hired prep assistant needs one).
- **A knife or pan bought with no free counter** isn't bought: the shop says so instead of taking the money.

**Reason:** different kinds of heat change how you cook and what the restaurant is, which serves "cooking is a craft" and "your restaurant, your identity" better than numbered upgrades. Boards and knife racks on counters extend the rule that counters are precious (2026-11-04).

**Given up:** equipment tiers (better hobs, bigger storage) and the board rail they were meant to grow. The board already only cuts.

**Later (see `ideas.md`):** ovens, pots, skewers and oven trays arrive with the cuisines that need them. Customers asking for a grill arrives with cuisine and service expectations.

---

## 2026-11-07 — Steak fat and per-steak variety

**Decision (user's calls):**
- **Steaks have fat and no two are alike.** Each has fat round its edge, a seam and marbling, with its own pattern, size and angle. Fat stays a small share of the top: big white patches looked like candy.
- **Fat covers the edge and holds its colour.** It runs down the side of the steak round most of the outline, with gaps where the lean reaches the edge. Fat goes from white to a creamy yellow and stays that way while the lean browns, colouring only when the steak is nearly burnt. Only thin marbling melts away.

**Reason:** real cuts look like this, and the variety stops every steak looking the same.

**Trade-off:** fat hides the doneness bands wherever it covers the side, so the core mostly shows only in the gaps. The doneness chip stays.

---

## 2026-11-07 — Heat pips stay ordinary objects

**Decision (user's call):** remove the pilot's change that drew a hob's heat pips over anything in front of them. Fix what hides them in the HUD pass instead (see `ideas.md`).

**Reason:** play showed the prompt card hides the pips, not the chef, so the change didn't fix the problem. It also added a shader and let pips show through walls.

---

## 2026-11-07 — An art pilot ahead of Phase 9, with a hand-written food shader

**Decision (user's call):** before Phase 9, a pilot replaces the hob, pan and steak with real Blender models, on its own `art-pilot` branch, without changing how the game plays. The look to try is chunky, soft-edged shapes with a few flat colours and little texture, with food slightly oversized so it reads from above. Food shaders are hand-written URP shaders rather than Shader Graph, which `CLAUDE.md` specified.

**Reason:** it tests the Blender pipeline, the food-state shader, overhead readability and frame rate while they are still cheap to change, and it can run alongside gameplay work. Shader code can be written and reviewed outside the Editor, which Shader Graph files can't easily.

**Given up:** tweaking food shaders visually in a graph. Colours and strengths remain material settings. The rest of the art stays in Phase 9, so these three models will look out of place next to the greybox until then.

---

## 2026-11-07 — More kitchen sounds: fire detector, footsteps and small cues

**Decision (user's call):** add a fire detector, footsteps, a clack for setting things down, a till on payment and a chime on the street door. All synthesized placeholders, like the first set.

**Reason:** the detector and chime tell players something without them having to look; the rest give actions feedback and make the kitchen feel alive.

**How it behaves (proposed and accepted):**
- **Fire detector:** one double beep a second, starting with the thick smoke at 75% of the way to burnt. One alarm however many pans are burning, panned towards the worst. It stops when the food leaves the pan, the pan cools below 140 °C, or a flip puts a paler side down. It's a warning only: no cost, and customers don't react.
- **Footsteps:** players only, very quiet, timed by distance actually walked, so walking into a wall is silent. Each chef has a slightly different pitch.
- **Clack:** whenever a chef puts something down or combines items, the same sound whatever it is, for now.
- **Till and chime:** one sound per table paying or party arriving, not one per person.

---

## 2026-11-07 — First kitchen sounds, ahead of Phase 9

**Decision (user's call):** bring a small set of sound effects forward from Phase 9: pan sizzle, bell, chopping and the kitchen door. The first clips are synthesized placeholders from `tools/synth_sfx.py`; a real recording replaces one by overwriting its file, with no code changes.

**Reason:** sound carries information (design doc §13), so it's worth having while the mechanics are being tuned, not only at the polish stage.

**How it behaves (proposed and accepted):**
- The sizzle needs food in the pan and follows the pan's temperature: louder and higher as it heats, silent below 110 °C and between shifts. Food landing or being flipped adds a rush of hiss.
- Chopping sounds by grade: a clean knock on the beat, duller when early or late, a skid on a miss.
- The door's creak is short and quiet, because it gets used constantly during service.
- Sounds are 2D and pan left and right with their place on screen. There's no distance fade, so a burning pan stays audible wherever the camera's attention is.

Tuning lives in `Data/Settings/SoundBank.asset`. This replaces the "Not built: the bell's sound" note under 2026-11-06.

**Not built:** see "Sound still to do" in `ideas.md`.

---

## 2026-11-07 — Size tiers dropped from Phase 4

**Decision (user's call):** a restaurant's size comes from which one you buy on the city miniature (Phase 6), not from upgrading one. Phase 4 ends with the waiter and equipment tiers.

**Reason:** Upgrade tiers would be replaced by the campaign as soon as it's built.

**Given up:** expanding a restaurant you already own. It could return in Phase 6 as knocking through a wall (see `ideas.md`).

---

## 2026-11-06 — Roadmap: a Campaign phase and a guided first day

**Decision (user's call):** two new phases. **Phase 6, Campaign,** covers the office, buying restaurants, the boss, the tablet station, service styles and decoration. **Phase 8, Guided first day,** comes after Content, once mechanics have settled. Content moves to Phase 7, and Art and polish to Phase 9. Phase 5 gains the read-only Review dashboard, vendors, the cookbook and cleaning.

**Reason:** The campaign design adds a lot that no phase covered, and the guided first day depends on nearly everything else.

**Trade-off:** no new player can try the full first-day flow until Phase 8, though its parts can be tested as they're built. Phase 6 is large and may need splitting when it's planned.

**Older entries** use the previous phase numbers.

---

## 2026-11-07 — Food stops cooking on a plate

**User's call:** a pan taken off the hob keeps cooking its food with its leftover heat, but food on a plate stops cooking. Plating is how a chef decides it's done. Replaces carryover cooking on the plate. The stronger carryover I proposed was not built.

**User's call:** the bar shows on pans off the hob too, and the needle slows as the pan cools.

**Details:** food on a plate still cools, so serving temperature still matters. Resting on a plate still counts towards the rest score.

---

## 2026-11-07 — The camera follows the people working; markers for customers out of view

**User's calls:**
- **The door and customers no longer count for the framing.** In a big restaurant the door may be far from the kitchen by design, and players see all of it by splitting up. Staff count like players. Replaces framing the door during service.
- **In the Review, and before anyone joins, the whole restaurant shows.** Once a player joins, the camera zooms in.
- **A customer out of view gets a marker on the edge of the screen:** a round timer while waiting for food, no timer while eating, and a face showing how it went for a couple of seconds as they leave.

**After play (user's calls):** the camera comes in closer and follows chefs more tightly, because it cut them off. It comes no closer than 6.5 m up, so two players together get a comfortable view, and from the kitchen a customer's walk from the door shows a marker for a while.

**After more play (user's calls):** chefs walk a little slower, 4.2 m/s down from 5, and the camera follows closer behind. As a player walks away from the kitchen, the camera moves with them and gently zooms out.

**Camera motion, after play (from user feedback; values proposed, tunable on `FramingCamera`):** it felt artificial, and then dizzying. Its moves are eased twice, so they build up and settle softly, and the threshold that made it move in steps is gone. A look-ahead was tried and removed: it nearly tripled the camera's travel in the kitchen. Height is now eased on its own and the floor position framed for the height it has actually reached, so zooming doesn't slide the view. It backs away at once but waits 0.75 s before closing in, which then takes about 5 s in all from the dining room. Top speeds are 6 m/s across the floor (down from 14) and 2 m/s up and down. With people far apart it leaves 20% of the screen clear at the top instead of 27%, as that room was going unused.

**The camera holds the kitchen (user's call, after a recording showed it moving on nearly every step):** following the chefs made it pan and zoom constantly, which felt chaotic and unpredictable. Now it's anchored on the kitchen, where the hobs and boards are:
- **One still shot of the kitchen:** exactly its working area, 0.5 m past its sides and back for the walls and the units on them, and 2 m in front for the customers at the wall and a little behind them. No margins are added on top (user's call: close it in a little), so it sits about 7.9 m up, down from 9.4 m. Anyone inside it doesn't count, so walking around in there never moves the camera.
- **As soon as anyone leaves the kitchen, it shows the whole restaurant,** centred and still, if that's no more than 1.4 times the kitchen's height (1.3 in the first restaurant). The move out starts at once and goes to one place, instead of creeping after them, which was part of the hook when opening. It counts as leaving 0.5 m past the kitchen, and as back 0.25 m past it, so standing in the doorway doesn't flick it. In a bigger restaurant, the kitchen's shot grows to take them in instead, always keeping the kitchen.
- **Past 18 m up, in a big restaurant, it frames the people alone,** as before.
- **A change of shot is one smooth arc (user's call, from a sketch):** height and floor position used to ease separately, the floor quickly and the height slowly, so the camera slid across first and then dropped or rose at the end, a hook. They now ease together as one point in space, starting and arriving together. It no longer waits 0.75 s before closing in (user's call, after play: it went in with too much delay): coming back into the kitchen, or a player joining, starts the move at once. `_holdBeforeClosingIn` is still there, at 0, and while it waits the camera doesn't move at all. If someone walks out faster than it moves, it hurries along that same arc rather than being pushed sideways off it, which was the rest of the hook when opening.
- **Every move starts at once, and joining is quickest (user's calls, over several plays: first too delayed everywhere, then a little too quick, backing away most of all; it also went too far out before coming back).** The two-stage easing, which started gently, is now one easing that moves off the moment the shot changes and is quickest at the start. It's most of the way there in about 0.9 s backing away (`_zoomOutSeconds` 0.45, down from 1.2), 1.4 s closing in (`_zoomInSeconds` 0.7, down from 1.5), and 0.65 s when the first player joins or a new day starts (`_joinSeconds` 0.32, down from 0.9). In between, closing in was 0.35 and joining 0.12, which the user found prompt but too quick and a bit dizzy; then 0.3, 0.6 and 0.25, still 0.1–0.2 s too quick, most of all backing away. Top speed is 40 m/s (`_maxSpeed`, up from 6), so the cap doesn't slow these moves. Going too far out was a bug: being hurried along its arc pushed the easing's target too, past the shot it was heading for, so it overshot and then crept back. Now it eases on from wherever it was hurried to, towards the same shot, and stops there.
- **The whole restaurant stays still wherever anyone is in the dining room (bug fix, after play: it moved erratically there).** Whether to show the whole restaurant compares its height with the kitchen shot's. That height was measured with the room at the top of the screen for someone far off, which shrinks as they walk away, so in the front metre of the dining room the comparison went over 1.4× and the camera followed the chef instead. It's now measured for the kitchen shot alone, so the ratio no longer changes with where anyone is.
- **The whole restaurant is framed a little closer (user's call, from a screenshot):** it keeps 0.9 m of floor beyond the back of the room instead of 1.5 m (`_wholeFarMargin`), which brings it down from 10.4 m to 9.75 m, 1.24× the kitchen shot's 7.9 m.
- **Nobody walks off screen:** under the easing, a hard limit pushes the camera at once if anyone comes within 0.6 m of the edge or into the top 10% of the screen, so players splitting up fast can't outrun it.
- **While a player manages, the camera follows their cursor, not their chef (user's call):** the chef left standing in the dining room doesn't pull the camera out while the player rearranges the kitchen; the cursor counts as if the chef were there. The chef may be off screen until the player stops managing, which is fine because it stands still.
- **Replaces** easing back as a player walks away from the kitchen, and the 6.5 m closest height as what decides the view in the kitchen. The kitchen's front is assumed to face the dining room at the south; a restaurant laid out another way will need that in its floor plan data.

**Details (proposed; flagged for review, tunable on `CustomerIndicatorHud`):**
- **A plain dot for anyone else out of view,** walking in or queueing.
- **Faces:** 4 stars and up is happy, 2.5 and up neutral, anything else or walking out unhappy.
- **Icons are drawn in code** until the art phase, rather than real emoji.
- **A group's markers spread out along the edge,** one each.

---

## 2026-11-06 — The waiter, the bell, and who's for hire where

**User's calls:**
- **Plates wait on any counter, and players ring a bell (X) to send one out.** It works before a waiter is hired too. Replaces my proposed dedicated pass counter.
- **A waiter serves only the diner whose order matches,** one plate at a time, serving before clearing. Wages are €45-70 a day for now.
- **The pile of dirty plates unlocks the waiter.** The cleaner looks after surfaces and plates; with a pile and a waiter, the cleaner focuses on washing.
- **Waiters and cleaners are only for hire from restaurant two.** Restaurant one only has helpers.
- **Waiters and cleaners wait and roam in the dining room,** not the kitchen.

**Details (proposed; flagged for review, tunable in `StaffConfig`):**
- **`_restaurantNumber` says which restaurant this is,** set to 1, until the campaign provides it in Phase 5. Set it to 2 to try cleaners and waiters.
- **The dishwasher is now called the cleaner** on screen. They don't clean surfaces yet: that comes with cleaning (design doc §15.8).
- **A waiter who slips drops the plate at the counter,** breaking it and losing the dish. The plate is replaced in the morning, like others.
- **If a diner leaves while the plate's on its way,** it goes back on a free counter and rings again.
- **Waiters walk at 3 m/s,** other staff at 2.
- **Staff blocked by someone standing still go another way round, or wait until there's space (user's call).** They never push through a player.
- **With a waiter, the cleaner no longer clears tables,** even against the wall.

**Not built:** the bell's sound, shown only as an "Order up!" label for now. The pile as an unlock is recorded but has nothing to check yet, because the pile can't be bought or sold; it matters once it can (Phase 5). A rung plate that can't go to anyone just stays rung.

---

## 2026-11-06 — Slower cooking, one bar under the pan, and how crust is judged

**User's calls:** a bar guides players through the cooking stages, teaching that more heat means more crust and faster cooking, and less heat a blander, slower steak. The ideal is high heat for crust, flip once, then low until done. A warning comes before burning. Cooking runs a little slower than the clock, at 90% of its pace: full speed burnt too fast, and half and 70% felt too long (user's call). Crust is judged by the better side, and any side close to burnt loses points. Replaces averaging the two sides.

**Details (proposed; tunable in `TuningConfig`):**
- **One bar at a time,** as two gauges were too much: the crust gauge until the first flip, then the stages of doneness, with a needle following the steak's middle. It reads the hottest the middle has been, as doneness does.
- **The warning:** the bar pulses red from browning 1.5 (burnt is 2) on the side in the pan. Flipping clears it.
- **The slower speed is for food and pans only:** the clock, customers and staff run as before. Resting and cooling take longer in real time too.

**Measured for a medium order at half speed, scale by 5/7 for 70%:** full heat to the flip (about 4 s), then 40%, takes about 18 s with full crust marks. Full heat throughout takes 15 s, but crust falls to 0.20. A steady 70% scores as well as searing and takes about as long, so searing isn't the only good way; it's worth watching in play.

**After play (user's call: too slow, and medium went straight to burnt, so well done was impossible):** heat moves through a steak faster (surface to core conductance 0.9 to 1.2) and it browns 15% slower. Medium comes about 3 s sooner, and at full heat well done now comes before burnt.

**Not built:** smoke as a burn warning.

---

## 2026-11-06 — Steak has five stages

**User's call:** a steak goes raw, rare, medium, well done, burnt. Replaces the seven stages, which also had medium-rare and medium-well.

**Details (proposed; tunable on `ING_Steak`):** rare from 45 °C, medium from 55 °C, well done from 65 °C, with orders aiming for 50, 60 and 70 °C. Orders come 30% rare, 50% medium, 20% well done, in the same spirit as the old mix.

---

## 2026-11-06 — Confirmed: mornings, office saves, the first style, keeping a name

**User's calls (the first four were flagged as proposals in the entry below):**
- **Yesterday's mess is cleaned during Prep,** and how messy it is depends on how the service went.
- **The boss's collection is worked out as Review starts and shown on the dashboard.** Warnings and repossessions happen in person the next morning.
- **Everything done in the office saves immediately,** including changing a player's appearance.
- **Only finished days count towards a restaurant's 7-day fee.**
- **The first restaurant is a grill with a fixed service style.** Players choose only its name, and can change the style later from the tablet. The first real style choice comes with the second restaurant.
- **Keeping a restaurant's name keeps its old reviews; a new name starts with none.**
- **Combinations are judged preliminarily like this:** service style sets most expectations, each cuisine adds a few of its own, and only combinations that earn it get custom review lines.
- **Customer photos, if built, stay inside the game,** with no export. The photo system itself is still a proposal in `ideas.md`.
- **Unusual combinations are worth trying in play:** they attract fewer regular customers but more influencers, so an odd pairing is a gamble and not simply harder. To judge once influencers exist.

---

## 2026-11-06 — Review is a read-only scene; saving, style at purchase, decoration choices

**User's calls:**
- **Review is a read-only scene,** a dashboard players browse: the service, customers, dishes, money lost and earned, overall progress, and more. It starts as soon as the day ends and the last customer leaves. Replaces the Review card over a walkable kitchen.
- **The game saves as Review starts, and the moment a restaurant is bought in the office.** Leaving a restaurant at any other point in the day loses that day's progress, and players must be clearly told so. Replaces the two saves proposed earlier today.
- **Review and the tablet station give the decoration hints:** "great steak, but I expected candles" appears in Review, and on the tablet's history during Prep.
- **Decoration includes floor patterns, wall colours and windows.**
- **Cuisine and service style are two choices, and any combination is allowed.** Reviews and tips judge how well it works, so fine-dining pizza is possible.
- **Style and name are chosen when buying a restaurant.** Players pick a new name or keep the previous owner's.
- **Recipes cost money to unlock.** Players browse them freely and decide which to buy.
- **A customer who orders something out of stock** loses patience as usual.

**Details (proposed while writing this up; flagged for review):**
- **Anything else done in the office also saves immediately,** such as borrowing from the boss, so it can't be undone by quitting.
- **Only finished days count towards the 7 days before fees fall due.**
- **Because Review is read-only, the boss's collection is worked out as it starts and shown on the dashboard. Anything he does in person, such as a warning or repossession, happens the next morning.**
- **Service mess is cleaned the next morning,** because Review starts as soon as the last customer leaves. Dirt is saved with the restaurant.

**Reason:** A read-only Review makes one clear save point, right after the day is decided, so no result can be undone by quitting during it.

---

## 2026-11-06 — The cookbook, hidden decoration taste, changing style, saving at Review

**User's calls:**
- **The cookbook sets the menu.** It opens from the management menu. A restaurant's style decides which ingredients the market sells and which recipes the cookbook offers. Players choose which recipes to unlock, which configures the menu, and that tells them which ingredients to keep in stock. Replaces "buying a new ingredient unlocks recipes".
- **Each recipe shows** its complexity, the appliances and ingredients it depends on, its approximate cost and the value it generates.
- **Recipes carried over from an earlier campaign can be used at any restaurant,** ignoring the style's limits, which gives a new chef a real head start. The cookbook is the same thing as the recipe book.
- **Players discover the cookbook on the first day:** after buying the plate pile, the boss asks for the dish he wants, so players see why they next need steaks and onions.
- **Decoration should match the style, and players aren't told what that means.** Fine dining expects bigger tables, lamps, plants, art, solemn colours and more staff; a burger place values functionality such as bar tables, bins and game stations, and more eclectic decoration.
- **Style can be changed at any time from the tablet station,** at a real cost, such as selling furniture that doesn't suit the new style.
- **The game saves at the Review phase and only then,** before the next day starts. Replaces continuous saving.
- **"Vibe" isn't a separate system:** a restaurant simply starts with its previous owner's style and decoration.

**Details (proposed while writing this up; flagged for review):**
- **Two saves within Review:** one on entering it, so a bad service can't be undone by quitting during the Review, and one on leaving it, so a purchase made in the office between days counts. Trips to the office happen at this point.
- **Collection and the boss's escalation happen in Review,** so they're saved straight away.
- **Quitting at any other time replays that day from the morning.** This means players can quit mid-service to retry a bad one. That seems an acceptable price on a couch game.
- **Changing style has no fee of its own.** The cost is half-price furniture sales, new decoration, and reviews suffering until the place matches.
- **The boss's dish is free to unlock.** Later recipes may cost money (open, see `ideas.md`).

---

## 2026-11-06 — The campaign, the boss and the first day

**User's calls (full picture in `game-design.md` §15):**

*Story and failure*
- **The boss** funds the player's restaurants to build a gastro-empire, believing reviews, social media and word of mouth make restaurants more profitable than ever. He takes a cut. His physical description isn't part of the joke.
- **Failing to pay escalates visibly:** the boss visits and warns you, his bodyguards repossess appliances on screen, he takes a restaurant, and finally you're erased. Money and restaurants go, and the recipe book stays in his office for the next chef. This replaces the "never all progress" half of the no-resets rule.
- **The ending:** with every restaurant on the city miniature unlocked, you take over the empire. The bodyguards follow player 1 and stand guard outside the door in restaurants. This is when players really make money and can hire and decorate more heavily.

*Saves and time*
- **One campaign at a time, with no save slots.** Progress on all restaurants saves continuously. The recipe book lasts across campaigns. Replaces "each restaurant is a persistent save slot".
- **A restaurant only runs while you're in it.** Others sit idle, and players choose between focusing on one and jumping around.
- **Fees are due every 7 days played in that restaurant.** Leave on day 3 and nothing is due until players return and play 4 more days there.
- **Collection day also sets the week's goal.**

*Food and recipes*
- **The first restaurant is a grill**, serving onion steaks.
- **No test kitchen in the office.** A new ingredient and its mechanics are introduced when it's first bought.
- **Style decides what vendors sell. Buying a new ingredient unlocks one or more recipes,** and customers can order them from then on. Harder recipes pay more.
- **Vendors are outside and stay reachable during service,** so running out means leaving the kitchen to restock. Prep is where players plan around the approximate number of customers expected.

*The first day*
- **Dry start:** players look round the restaurant and clean up, with very light mechanics.
- **The boss phones** and asks them to get ready for him and his friends: enough plates and onion steaks.
- **The restaurant has no plates at all.** The plate pile is an appliance, bought in management mode (Select), and it's the first purchase.
- **Street vendors near the restaurant** sell the food.
- **The boss arrives alone,** because his friends made a cheap excuse. Only he sits and eats while the bodyguards stand behind him. When he's done he invites them to sit, which brings two more orders, and both bodyguards leave a review.
- **Tasks appear one by one in the top right corner,** each with its own control hints.

*Cleaning, devices and decoration*
- **Dirt is on floors, tables and counters,** cleaned the same way: approach it, hold X and move the stick to match a pattern. The chef uses a mop or a cloth depending on the surface.
- **Boss calls come through a personal mobile,** shown in the bottom right corner.
- **A restaurant laptop, phone or tablet station is a separate, bought appliance,** introduced around day 2. It opens a shared-screen listing, like a Google listing, and that's where reviews are explained in more depth. It also offers quick-restock delivery and switching restaurants without visiting the office, and may gain other perks.
- **Appliances are bought from management mode,** with no need to go anywhere.
- **Decoration affects reviews and customer profile:** the food can be delicious while the place doesn't feel original, clean, or in keeping with its style.
- **Vendors and scenery should suit the location** chosen in the office. The camera zooms out when players split up, and vendors stay close.
- **Co-op and repeat play:** any player can complete any task, and returning players can skip the tutorial.

**Details (proposed while writing this up; flagged for review):**
- **An idle restaurant is frozen:** food doesn't age, and nobody is paid or changes morale. Nothing in it moves until players return.
- **The bought plate pile comes with its plates,** 6 as now.
- **Washing up first appears after the boss's meal,** since no dirty plates exist before then.

**Replaces:** starting with plates, and with no tables (two 4-seat tables are now inherited); the plate pile not being for sale; the free daily delivery, once vendors exist; the review app as a phone app (§7.3).

**Still open:** see "Questions from the campaign design" in `ideas.md`.

**Reason:** Gives the game a reason to exist beyond each service, and teaches its systems through a story, one light step at a time.

---

## 2026-11-04 — Hints only ever show Xbox buttons

**Decision (user's call):** nothing players see names keyboard controls, whatever they're playing with. Hints are always Xbox buttons. The keyboard still works, mainly for testing.

---

## 2026-11-05 — The floor plan, a camera that follows the action, and tables (Phase 4, milestone 3)

**User's calls:**
- **A real restaurant floor plan:** customers come into the dining room from the street, not past the kitchen, and a door lets people move between the rooms.
- **A customer sits and shows their order even at a dirty table;** players clear it before they can put the dish down.
- **Customers and players collide,** just as hires do.
- **The camera follows the action,** zooming out as players or customers spread apart and in as they gather. The restaurant can grow as much as players want, so a fixed camera won't do. Replaces the fixed camera decided for Phases 0–3 and moves this up from size tiers.

**Details (proposed while building; flagged for review):**
- **The building is the first restaurant, a small steakhouse, 8 × 9 m (user's call):** a kitchen 5 m deep and a dining room 4 m deep to its south. A low wall runs between them, so the camera sees over it. The door is 1.2 m wide, west of the hatch, and swings open for whoever's near. It never blocks anyone. The sink and dirty pile start on the east wall, to leave about 2 m of the dividing wall free for tables.
- **Tables are bought with none at the start,** and only go in the dining room; kitchen furniture stays in the kitchen. Within a room, layouts are still free. Tables bought in Plan or Prep count from that day's service.
- **No pass and no stools (user's call):** the wall between the rooms closes up, and the restaurant starts with three bar tables against its dining side, served across it. This frees that row of the kitchen. It replaces the counter service of 2026-11-02.
- **The dishwasher clears any table against the wall,** from the kitchen side. Tables away from it are for players, and later the waiter.
- **A customer with no way to their seat gives up:** a lost cover, no penalty, and the seat goes to the next. Someone leaving always gets out, and someone blocked by people for 2 seconds walks through, so a bad layout costs money but can't freeze a service.
- **Pushing a seated customer moves them,** and they walk straight back.
- **The camera** keeps its 55 degree tilt and leaves 27% of the screen clear above the floor at the top, as the back wall stands up into that (user's call: more room above the wall). It doesn't show much beyond the building's walls, and centres on it once it all fits. It pans quickly and zooms slowly (user's call: subtle and effortless). Staff don't count towards the framing, as they stay in the kitchen. Tunable on `FramingCamera`.

**Later the same milestone (2026-11-05 and 06).**

*User's calls:*
- **The street door moves like furniture** and counts towards the camera's framing, so someone walking in doesn't make it zoom out. It only goes on the dining room's outer walls; anywhere else it turns red. It's framed during Service only, so a chef working alone in Plan or Prep still gets a close view.
- **Furniture against the wall between the rooms works from both sides,** like a hatch, whether it's a table or an appliance.
- **Two table types for now:** a bar table seats 1 and is narrower, and its chair costs nothing. A regular table seats up to 4, and players choose how many chairs it has.
- **Customers come in groups of 1 to 4,** following the tables. With at least one table for 4, expect groups of up to 4.

*Details (proposed and accepted as a plan; tunable in `FurnitureCatalog`):*
- **Prices:** bar table €30, regular table €50 with one chair, each further chair €10, refunded at half. RB and LB add and remove chairs while hovering over a table.
- **One chair per side,** and only where there's room in the building, so a table against a wall seats at most 3. Your biggest groups always need the long walk.
- **Each table votes on group size:** a stool or bar table votes for one person; a table for four splits its vote across 1, 2, 3 and 4.
- **A group takes the smallest free table that fits,** never shares, and stools are for people alone. The longest-waiting group that fits gets the next free table.
- **A plate goes to the diner it matches,** else the first waiting. Each starts eating when served and pays for their own dish, and they all leave together after the last has finished.
- **A group shares one patience meter.** If it runs out, everyone unserved walks out, and anyone eating pays for what they've had.

*Details (proposed while building; flagged for review):*
- **A pushed customer goes along with it** and returns 0.8 s after contact ends, as hires do. Seated customers can be pushed off their seat.
- **Routes keep clear of chairs** where there's room, so a diner doesn't walk through a friend already sitting. Someone who makes no real progress for a second squeezes through people for a moment, and walks through furniture only if pinned, so a crowded corner can't freeze a service.
- **Staff don't yet work across the wall;** only chefs do.

**Known limits:** chairs and queue spots aren't protected, so a table can be placed over one; the customer then walks through it and sits inside it. Walking speed may make distance matter too little (see the milestone's playtest).

---

## 2026-11-04 — Buying and selling furniture (Phase 4, milestone 2)

**User's calls:** you start with **4 counters and 1 hob**. Everything else about the milestone was proposed and accepted as a plan.

**Details (tunable in `FurnitureCatalog`):**
- **Y opens a shop in management mode,** during Plan and Prep: counter €30, hob €80, shelf €60, fridge €120. These are first guesses, not yet checked against a full day's earnings.
- **You pay when it goes down,** so B backs out for free. If two players are placing at once and the money runs out, the second gets "not enough money". Unlike wages, a purchase can't cause debt.
- **X sells for half the price,** free starting furniture included, so a starting counter is worth €15.
- **Only empty pieces sell,** a hob only with its pan home, and not while it's carried or a helper is headed to it. The pass, sink, piles, board, racks and bin aren't for sale.
- **The last hob, fridge and shelf can't be sold.** This is the exception to learning by playing: with no hob and no money the restaurant couldn't recover, and deliveries need a fridge and a shelf to arrive in. Selling a fridge or shelf hands its delivery to the one that's left.
- **A bought hob comes with its own pan,** and a sold one takes its pan with it.

**Not built:** bought furniture and layouts last from day to day but not after quitting, until saves in Phase 5.

---

## 2026-11-04 — Layout editing and management mode (Phase 4, milestone 1)

**User's call:** during Plan and mise en place, players can switch to a cursor view with the Select button.

**Details (proposed while building; flagged for review):**
- **Keyboard:** Tab, though hints only ever name Xbox buttons. **While managing:** the chef waits; A picks up and places, B puts what's held back where it was, and Select returns to the chef, the same button that enters (user's call). You can't ready up while managing.
- **No grid (user's call):** furniture goes exactly where the player puts it, snapping when within 0.15 m of lining up with a neighbour (edges touching or centres level), a wall, or where it came from. Replaces the 0.5 m grid first built.
- **RB and LB rotate by 45 degrees (user's call),** easing round in about 0.1 s, though the new angle counts straight away (user's call: smooth it now). Keyboard: C and Z. Turned stations take up their own shape, can't poke through walls, and snap to neighbours turned the same way.
- **Furniture under a cursor lights up,** just as when a chef walks up to it (user's call). It keeps its own colour and only gets brighter: tinting it towards the player's colour was confusing. This replaces the player-colour tint from Phase 0 for chefs too.
- **Anything can be moved,** and what's on a station moves with it. Two players can't hold the same station.
- **Placements are refused** outside the piece's own room, on another station, or on a person. The reason shows at the cursor.
- **A layout is never refused for being bad (user's call):** a blocked hob, a cut-off counter or a walled-in chef are all allowed, and players find out by walking it. This replaces the earlier rules that work stations keep a clear front and that nothing gets cut off. Learning by playing beats being prevented. A helper with no way to a station stands and waits rather than working from a distance.
- **What's carried turns red where it can't go (user's call),** and the floor frame only shows while choosing what to pick up.
- **What's carried stays on the floor (user's call):** lifted, the angled camera drew it away from where it would land. Being carried shows through brightness, or red.
- **Service stops rearranging:** management mode isn't available during Service or Review.

---

## 2026-11-04 — One waiter, no runner; a limited pile of dirty dishes

**Decisions (user's call):**
- **The waiter does both jobs:** takes food to the table and clears the table afterwards. There's no separate runner. Replaces the roadmap's and design doc's waiter and runner.
- **Dirty dishes get a pile with a maximum,** just like clean plates. The pile holds 6, the clean pile holds 6 and the sink holds 2, as three separate things. Plates go from the pass to the dirty pile, to the sink, to the clean pile, in hands or on a tray.
- **Management mode:** during mise en place, players can switch to the cursor view with the Select button on the Xbox controller.

**Note:** the kitchen owns 6 plates, so the dirty pile can't actually fill up until there are more plates. The sink's limit of 2 is what bites for now.

**Reason:** One clear hire instead of two similar ones, and a full pile makes falling behind on washing up visible.

---

## 2026-11-04 — Morale (Phase 3)

**Decisions (proposed while building; flagged for review, tunable in `StaffConfig`):**
- **Everyone starts at 60%,** where it neither helps nor hurts. Morale carries from day to day, while fatigue resets overnight.
- **It changes at the end of each day:** a good rating lifts it (up to +10% at 5 stars, down to -10% at 1 star, 3 stars neutral), walk-outs lower it (up to -15% if everyone walked out), and ending a day past 50% fatigue wears on it (up to -20%).
- **Its effects:** speed changes by 3% per 10 points away from neutral, in both directions. Mistakes get likelier only below neutral, because being happy doesn't add skill.
- **Below 10%, they quit** at the end of the day, in the Review. Wages for that day are still paid.
- **Players see words, not numbers:** new, happy, doing fine, unhappy, fed up, on the Plan card and in the Review. The debug overlay shows the numbers.

**Reason:** Makes staff feel like people, and ties how a service goes to how the team works the next day. Pay and how you treat them (pushing them around, say) don't affect it yet.

---

## 2026-11-04 — People push each other; onions burn visibly

**Pushing (user's call):** chefs and staff can push each other. Someone idle gets moved along by whoever walks into them, and two walking into each other with equal force don't move. A helper pushed away from their work stops, walks back when there's space, and carries on.

**After play:** bodies have weight: an idle body soaks up 80% of a push (`_resistance` on `PushResolver`), so it moves at a fifth of walking pace (first 40%, too light in play). A pushed helper doesn't fight back while pressed, so you can keep pushing them as far as you like; they head back 0.8 s after nobody's touching them. Helpers slide along furniture when pushed into it, as chefs do.

**Details (proposed while building):** the stronger push wins by the difference. Someone pinned against furniture can't give way, so the pusher stops. The food stays on the board while a helper is pushed off it, and a chef can take it in the meantime. A chef pushed while washing keeps washing for now.

**Onions burn (user's call):** food left on the heat after it's cooked burns, onions included. Diced onion browns faster (1/14000, was 1/30000). It wasn't showing a burn at all, because the view showed the side facing up and onions don't get flipped; loose food now shows its darkest side. Food tags say "Burnt" for any burnt food.

**Measured:** at full heat, diced onion is cooked at 4 real seconds and burnt at about 6.5; at 70% heat, cooked at about 6 and burnt at about 23.

---

## 2026-11-04 — The prep assistant (Phase 3)

**Agreed:** the assistant keeps the containers players set out stocked. A container with some food in it says what it's for; when it runs low, they bring more from storage by tray, cutting at the board if what's stored is still whole. Empty containers are left alone: only players decide what a container is for. They never touch the hob or the steak.

**After playtest (user's calls):** assistants don't restock storage; they only keep containers filled, going by the first ingredient players store. A tray only comes out to move several portions and goes back afterwards; food to cut is carried by hand. If they end up holding food with nowhere to put it, whole food goes back to storage and food already cut goes in the bin.

**Details (proposed while building; flagged for review, tunable in `StaffConfig`):**
- **Refills below half full,** emptiest first, up to 4 portions a trip and never past full.
- **With no tray free, they carry one portion at a time by hand.**
- **Cuts one item in 4 real seconds** at average speed, without the dial. Skill and tiredness set how even the cut is. A mistake drops a portion, which is lost.
- **A busy board:** they don't fetch food to cut while it's in use, and if a chef gets there first they wait 5 real seconds, then put the food back. A chef can take food off the board mid-cut.
- **Candidates:** every day offers at least one person per role. Four assistant types, €40–80 a day.
- **Leftovers** go to a matching container first. Whole food with no room in storage also goes in the bin.
- **Staff walk around furniture,** by pathfinding, and stand on the free side of a station nearest the middle of the room. 
- **Staff take up space (user's call):** they arrive at separate spots, and give way to each other and to chefs, keeping right to pass. If someone's standing on their spot when they start a job, they stop close by and carry on from there.

---

## 2026-11-04 — Any ingredient can go in any storage

**Decision (user's call):** food isn't tied to a fridge or shelf. Refrigerated food can go on the shelf and anything can go in the fridge, and the cost is freshness: meat on a shelf spoils much faster. Replaces the fixed slot for each ingredient.

**Details (proposed while building):** each unit still has four slots, one ingredient per slot at a time, free again when empty. A portion goes with the same ingredient if there's room, or else into a free slot. Slots hold up to 6 (user's call), so each unit holds 24. Deliveries fill the same way (18 steaks to the fridge, 8 onions to the shelf), and tomatoes and diced onion no longer get a reserved place.

**Measured by the existing model:** a night on the shelf costs a steak about 86% freshness, against about 25% in the fridge. Stored food below 10% gets thrown out, so a steak left on the shelf won't survive much more than a night.

---

## 2026-11-04 — Containers, trays and restocking through shopping

**The user's design:**
- **Restocking happens through shopping.** Players start with a fridge and a shelf, and purchases go straight there. This replaces the storeroom and crates built earlier the same day. Shopping itself comes later; until then, a free delivery arrives at the fridge and shelf each morning.
- **The cutting board only cuts.** The finished cut stays on it and blocks the next one until someone moves it; the board's four slots are gone.
- **Containers** fill a counter and hold up to 6 portions of one ingredient at a time (first 10, changed after play). They keep food fresher than a tray, but not as well as a fridge. Counters become precious, and players plan their kitchen around them.
- **Trays** fill a counter when put down and hold 4 things in any mix: ingredients, or empty, filled or dirty plates. Food loose on one ages faster. They can serve customers (RT chooses what to give) and move ingredients, including restocking a fridge.
- **Usual flow:** with a tray, go to the fridge or shelf, then put ingredients in containers for quick access during service or mise en place.

**Details proposed while building (flagged for review, in `EquipmentTier`):**
- **6 containers and 4 trays,** on racks, carried to any counter by hand until layout editing in Phase 4.
- **Ageing:** food in a container ages at half the normal rate and on a tray at 1.5×. A fridge still beats a container because of its cold.
- **Containers give one portion at a time,** oldest first, to hands, plates and pans. A tray loads as many as fit. A container can only be picked up when empty.
- **RT with a tray** opens a four-button menu of what's on it. Not at storage, where RT loads the tray from the slot menu, or at a hob, where RT is the heat. A with a tray tries the last thing put on.
- **Daily delivery:** 18 steaks and 8 onions, in fridge slots of 20 + 20 and a shelf slot of 12 onions. Food stored starts at 0, then the first delivery arrives on day 1.
- **Food in a container doesn't count as "left out" overnight.**

**Returning things:** clean plates, empty trays and containers go back to their piles, and ingredients back to storage. A tray returns everything on it that fits, wherever it is on the tray. Cut food can be put away too.

**More counters for testing:** 8 free counters instead of 3 (a longer island and three along the walls). The final number is a design question, since counters are meant to be precious.

**Not built:** shopping and choosing what to buy, so deliveries are still fixed, and leftovers can pile up.

---

## 2026-11-04 — Storeroom, crates and the night (Phase 3; storeroom and crates since replaced)

**Decisions (proposed while building; flagged for review):**
- **Storeroom on the east wall,** across the kitchen from the fridge and shelf, kept cold. The trip is the cost of not prepping. It holds 40 steaks and 20 onions.
- **A free delivery each morning,** as Prep starts: 18 steaks and 8 onions, up to capacity. The Prep card says what arrived. Tomatoes aren't delivered, since nobody orders them.
- **The line starts light on day 1** (6 steaks, 4 onions, 2 tomatoes) and no longer refills by itself. This replaces the temporary morning top-up.
- **Crates:** the storeroom's menu hands out a crate of up to 6 of the oldest portions in that slot. Interact empties it into the fridge or shelf, and what doesn't fit stays in the crate. An emptied crate disappears rather than needing to be carried back.
- **Closing runs as the service ends:** 18 in-game hours pass, hobs turn off, and food ages at the temperature around it. Stored food below 10% freshness is thrown out. Food left out stays where it is, aged at room temperature. The Review lists both, which is the Close line agreed in the plan.

**Known effect, to judge in play:** a night in the cold costs a steak about 25% freshness, and storage hands out the oldest first. That alone takes a day-old steak from a perfect score to about 98%, which costs most of the top tip on the steep curve. So overstocking hurts, but with fixed deliveries the player can't yet order less. Buying comes in Phase 5.

---

## 2026-11-04 — Wash game: less inertia, interface above the chef

**Playtest:** the mechanic feels good, but the point had too much inertia and the interface was intrusive.

**Decisions:** the interface is smaller and see-through, and sits above the washing chef. The point keeps moving far less after you let go (damping 5, was 2.5), runs away more gently (instability 4, was 5) and gets smaller nudges (1.4, was 1.8). In the simulation, the coast after a full push drops from about 1.6 circle radii of travel to about 0.4.

**Then, after more play (too easy):** damping 4.2, instability 4.5, nudge 1.6, between the first and second tuning.

**Trade-off:** ordinary over-correcting used to be heavily punished and now mostly isn't; only slamming the stick around with slow reactions is. This softens "small movements shift it drastically" in the earlier entry. Doing nothing still never cleans a plate.

---

## 2026-11-04 — Plates can wait on the pass; washing by hand is a balancing game

**User's direction:** a blocked stool feels good. A plate on a free stool can be taken back, and goes to whoever sits down next, fitting their order or not. Washing becomes a stick game: keep a point in the centre of a circle, small movements shift it a lot, and not reacting never cleans a plate.

**Details (proposed while building; flagged for review, tunable in `TuningConfig`):**
- **Plates wait indefinitely** on a free counter, cooling, including overnight. This replaces clearing them after a delay. They can also be set out during Prep.
- **The point behaves like balancing a stick on your palm:** it accelerates away from the centre the further off it is, with small random nudges. A plate takes 2.5 s at full speed; full speed inside 25% of the circle, fading to nothing at 55%. Each plate starts with the point off-centre at 70%.
- **The nudge is kept below what the instability can overcome,** so a point left alone can't drift back into range. A test guards this.
- **One chef at the sink at a time,** with empty hands. Use starts and stops, and the chef stays put. The dishwasher can wash alongside, sharing progress, and isn't subject to the game.

**Measured with simulated players, not real ones:** doing nothing cleans 0 plates; a steady hand with a 0.15 s reaction takes under 4 s per plate; over-correcting is several times slower.

---

## 2026-11-04 — Hiring and the dishwasher (Phase 3)

**Decisions (proposed while building; flagged for review, all tunable in `StaffConfig`):**
- **Hiring happens on the Plan card:** A, X and Y hire the three people listed, and B lets your dishwasher go. Any player can press them. Candidates change daily and are described in words.
- **Starting money €100; wages €30–70 a day,** paid when the service ends. You can't hire what you can't afford, but a day's wages can still push the balance negative.
- **One person per role,** and only the dishwasher exists so far.
- **The dishwasher follows the player's rules:** one plate at a time, from pass to sink to stack, and a player can beat them to a plate. Order of work: clean plates to the stack, then clearing stools, then washing. A plate takes 2.5 real seconds at average speed.
- **Fatigue and mistakes:** each plate adds fatigue, which slows them down and makes mistakes likelier. It resets overnight. A mistake breaks a plate.
- **Broken plates are replaced free each morning** until they can be bought in Phase 5.
- **Chefs can't pick things up or use stations during Plan and Review,** so the hiring buttons don't also do things in the kitchen. They can still walk around.
- **Staff have no collision** yet, so they walk through chefs, and they don't path around counters; their routes along the pass happen to be clear.

**Reason:** Makes a hire a real decision, with a visible payoff: someone else clears the stools and does the washing up.

---

## 2026-11-04 — Plates and washing up (Phase 3)

**Order changed (user's call):** plates and hiring come before the storeroom, to reach the first hire sooner.

**Decisions (proposed while building; flagged for review):**
- **The kitchen owns 6 plates** (in `EquipmentTier`), and the stack can run out.
- **A used plate stays dirty on the pass until someone clears it, and blocks that stool.** A customer can sit there, but can't be served until it's cleared.
- **The sink:** drop dirty plates in, press Use 4 times per plate (in `TuningConfig`), then carry clean plates back to the stack. Everything is carried by hand, one plate at a time.
- **Plates left dirty stay dirty overnight.**

**Reason:** Gives washing up a real cost in time, so that hiring help pays off.

---

## 2026-11-03 — Phase 3 shape and the daily loop (milestone 1)

**Agreed with the Phase 3 plan (`plans/phase-3.md`):** the waiter and runner wait for tables in Phase 4; Prep has no time limit and ends when all players ready up; Close is summarised; staff have fixed roles; restocking is physical, with a storeroom and crates of up to 6; hiring is a Plan menu with candidates described in words. Progress lasts for the session until Phase 5.

**Decisions while building milestone 1 (flagged for review):**
- **Every phase but Service ends when all players ready up** with Menu (keyboard: Esc); pressing again takes it back. This replaces the Phase 2 restart, so `PlayerRejoin` and the scene reload are gone.
- **Close has no phase of its own yet;** its waste line arrives with stock in milestone 2.
- **The simulation pauses during Plan and Review,** so food on a hob doesn't burn while players read. Players can still walk around.
- **Prep's card sits near the top of the screen,** because players work through that phase.
- **Each day has its own customers,** from the seed plus the day number, so a given day replays the same while tuning.
- **Balance starts at 0** until hiring needs starting money.
- **Temporary:** line storage tops up by itself each morning, until the storeroom replaces this in milestone 2. Food left on counters and in pans stays there.

**Reason:** Turns one service into days that follow each other, so the later milestones have a loop to plug into.

**`game-design.md` §4 is now partly out of date:** it lists Close as a phase and leaves open whether Prep has a time limit and whether Close is played.

---

## 2026-11-03 — Slightly easier crust; picky customers after Phase 2

**Decisions:**
- **Crust scoring relaxed a little** (values proposed, now in `TuningConfig`): no crust at an average browning of 0.1 or below (was 0.2), full marks from 0.6 (was 0.7). The "pale" comment now needs an average below 0.35.
- **Per-customer pickiness waits until after Phase 2** (user's call; see `ideas.md`).

**Reason:** Playtest: the "pale" comment came up often. By calculation, flipping on green and then giving the second side about a real second shouldn't draw it even under the old rules, so if it keeps happening, look for early flips, or for a bug, before relaxing further.

---

## 2026-11-03 — One gauge, for the first flip only

**Decision (user's call; band proposed, tunable in `TuningConfig`):** a steak gets a single gauge, under the pan. It appears as soon as the steak touches the pan, its needle moves left to right, and it disappears at the first flip. After that, players judge the doneness themselves, reading the tag when close to the hob. Better signifiers come at a later stage. A second gauge for doneness was proposed and rejected as too much.

**How it reads:** the needle follows the actual browning of the side facing down, not a timer, so a cool pan makes it creep and a hot one makes it race. The green band is browning 0.7–1.1, and the scale ends at burnt. It lights up by growing slightly while in green. Other food gets no gauge (`NeedsFlip`, steak only).

**Measured (preheated pan at full heat):** green opens about 2 real seconds in and lasts about 1.2 s. Flipping anywhere in green still allows a best score of 98.8% or more for every order on the menu. The first side barely depends on the order; the second side and the heat decide the doneness: roughly 2–3.25 s for rare and medium-rare, 3.5–4.75 s for medium, and 8–9.5 s at 80% heat for well done.

**Reason:** Playtest: it wasn't discoverable how to get a steak right, and pale steaks felt unfair.

---

## 2026-11-03 — The service loop (Phase 2, milestone 5)

**Decisions (proposed while building; flagged for review):**
- **The kitchen is live before opening:** the cooking simulation runs, so players can preheat and prep, but no customers come until someone presses **Menu (keyboard: Esc)**. There's no time limit on this. It stands in for the Prep phase until Phase 3, and free prep time makes the first rush easier.
- **Last orders 30 in-game minutes before the end** (22:30, tunable): the door closes to new customers, and those inside can still be served.
- **Closing time (23:00):** the service ends once everyone has left. Anyone still waiting keeps losing patience and walks out, with the penalty. With the patience dial at 0, patience starts draining at closing time, so a service can always finish.
- **The summary** shows money (meals, tips, walk-outs), the rating over all reviews including walk-outs, how many were served and as ordered, walked out and turned away, the best review line and the most common complaint.
- **Restart reloads the scene,** with the same button, and players come back on their own devices without joining again. A small static (`PlayerRejoin`) carries this across the reload, as a deliberate exception to "no singletons". Stock, money and everything else start fresh; persistence comes in Phase 5.
- **Pause does nothing else yet:** the button is only used to open and restart.

**Reason:** Completes the first vertical slice: open, cook through a service, see how it went, go again.

---

## 2026-11-03 — Steep tips; walk-outs cost money

**Decisions (the user's direction; numbers proposed, tunable in `ServiceConfig`):**
- **Tips reward perfection,** based on the menu price: perfect → 100% of the price, 99% → 50%, 95% → 20%, 90% → 10%, 80% or below → nothing, blended between. Replaces the earlier "up to 25%, from a 60% score".
- **Walk-outs cost half their order's menu price** (€12 for a steak, €14 with onions), including those who give up in the queue. Money can go negative. Customers turned away at a full door cost nothing.
- **The wait only reduces the tip when patience drops below 50%,** falling to 40% of the tip at zero. Otherwise some waiting is unavoidable and 100% could never be reached.

**Perfect means 99.5% or above** (user's call): the top stop of the curve is at 99.5%, because nothing reaches exactly 100% with the real assets.

**Measured (open question):** the best scores found with the real assets, searching heat, time per side and rest but with a freshly stocked steak: medium-rare 99.78%, medium 99.64%, rare 99.12%, well done 99.01%. With the 99.5% stop, the best tips are 100% of the price for medium-rare and medium, and about 62% and 51% for rare and well done, so the harder orders still tip less. Revisit after playtesting; a fix would change how crust is scored. Tests hold each doneness at 99% or above.

**Reason:** Playtest feedback: tips should matter much more, and letting a customer walk out should hurt.

---

## 2026-11-03 — Payment, tips and reviews (Phase 2, milestone 4)

**Decisions (proposed while building; flagged for review):**
- **Prices:** steak €24, onions €4 (the currency is a field in `ServiceConfig`). Customers pay only for what reached the plate.
- **Payment** = price × (50% + 50% × dish score), rounded to whole units. A bad dish still earns something.
- **Tip** = payment × up to 25%. It rises from nothing at a dish score of 60% to the full amount at 100%, then shrinks with the wait, down to 40% of itself if served with no patience left. Quality earns the tip; speed protects it.
- **Customers pay and review when they finish eating.** Walk-outs pay nothing and leave a 1-star review. Customers turned away at a full door leave no review.
- **One review line per customer, about the worst thing:** no main, burnt, wrong doneness, no side, the weakest part of the dish, then a long wait; otherwise praise (from 4.5 stars) or "decent". Lines are editable in `ServiceConfig`.
- **On screen:** a money counter at the top centre, with each payment flashing beside it, and a 4-second card where the customer sat, showing stars, payment and the review line.
- **Money is income only for now.** Ingredient costs, rent and wages come in Phase 5.

**Reason:** Turns the dish score and patience into money and words, so a good dish is rewarded and a mistake can be understood.

**Departs from `game-design.md` §7.1**, which recommends no numbers during service: the cards show stars and money as an instant reward. The dish's score breakdown stays out of them. Revisit after playtesting.

---

## 2026-11-03 — Patience and walk-outs (Phase 2, milestone 3)

**Decisions (proposed while building; flagged for review):**
- **A seated customer waits 35 in-game minutes** (about 35 real seconds) for their food before walking out. Patience **drains linearly** from when they sit down until they're served; walking to the stool doesn't count.
- **Queueing by the door drains patience at half the rate.** One meter carries over from the queue to the stool, so a long queue means less patience at the counter.
- **Walking out** frees the stool for the next in the queue and counts as a lost cover. The log separates walk-outs from customers turned away at a full door.
- **Realism dial `Customer patience`** (in `RealismSettings`) multiplies the wait: 2 = twice as patient, 0 = never walk out. It applies live.
- **On screen:** a patience meter under each ticket (green → amber → red), a meter alone over queueing customers, and "walked out!" in red over a customer leaving angry.
- Patience left when served is recorded for tips in milestone 4; it doesn't change the dish score.

**Reason:** Adds the time pressure milestone 3 is for, while keeping the steak's 6–8 s cook plus rest comfortably inside one customer's wait.

---

## 2026-11-02 — Services last 5 hours; satisfaction is the dish score for now

**Decisions:**
- **A service lasts 5 in-game hours** (about 5 real minutes; was 6). The arrival pace stays the same. The rush stays at 35–60% of the service, so the quiet tail after it is about an hour shorter.
- **Satisfaction** for each served customer is, for now, **their dish's score against their order** (stars). The Service log shows the average, how many got what they ordered, and the last four dishes with what was off. Patience and the wait join it in milestones 3–4.

**Reason:** Playtest: the pace was right, but the end of the service dragged.

---

## 2026-11-02 — Orders, tickets and pacing (Phase 2, milestone 2)

**Decisions (proposed while building after the milestone 1 playtest; flagged for review):**
- **Slower service, more stock:** a customer about every **25** in-game minutes (was 12), with a rush at **2×** (was 2.5×), which makes about 18 covers instead of about 30. The fridge holds **2 × 9 steaks** (was 6) and the shelf **8 onions** (was 6). Running out is still possible on purpose; restocking comes with Prep in Phase 3.
- **Every customer orders when they arrive:** steak at a doneness, weighted **Rare 2 · Medium-rare 4 · Medium 3 · Well done 1** (no medium-well on the menu), and **onions with 40%** of orders. Tunable in `ServiceConfig`. Orders use their own random generator, so changing the menu doesn't move the arrivals.
- **Each doneness level has an order target** inside its band (steak: 48 / 54.5 / 60 / 65.5 / 71 °C core). The ordered level's target replaces the house standard when the dish is scored.
- **Scoring with an order:** the steak counts **1** and the side **0.5**. Anything ordered but missing counts as a zero at its weight, so a perfect steak without the onions it came with scores about two thirds.
- **Ticket above each seated customer** ("Steak · Medium-rare + Onions") until they're served.
- **Doneness tag on the food** each player carries or faces (in a pan, on a plate, on the counter): the level's name, hanging below the food so it doesn't cover the station's prompt.
- **One HUD colour per doneness**, shared by tickets and tags (in `HudStyle`), close to the steak's own band colours.
- **The pass prompt checks the plate:** "serve: as ordered", "serve: wanted Medium", "serve: no onions". You can still serve a wrong plate on purpose.

**Reason:** In playtest, demand outran stock, and nothing told the cook what to aim for. Orders make doneness the core decision of each dish, and the shared colours make it a quick check.

---

## 2026-11-02 — Customers and seating details (Phase 2, milestone 1)

**Decisions (proposed while building; flagged for review):**
- **Until orders exist (milestone 2), any plate with food** served on the pass in front of a seated customer counts: they eat it and leave. The quality card still shows.
- **The service starts automatically with the scene** for now; Start, the clock and the summary come in milestone 5.
- **One stool per pass counter** (three); customers come in through a **door at the east end** of the dining strip. With all stools taken they wait in a **queue of up to 3** by the door, the longest-waiting taking the next free stool; beyond that they **walk away (a lost cover)**.
- **Starting numbers (tunable in `ServiceConfig`):** a customer about every 12 in-game minutes (±35%), a rush from 35% to 60% of the service at 2.5× the rate, 15 in-game minutes to eat, walking at 1.6 m/s.
- **Fixed random seed**, so a service plays the same way each time while tuning.
- **Customers have no physics**; they never get stuck on chefs.

---

## 2026-11-02 — Phase 2 shape: counter service, steak to order, ~6-minute services

**Decisions:**
- **Counter service at the pass** for the first service: customers sit on stools along the dining side and are served on the pass in front of them; no front-of-house work until staff (Phase 3).
- **Menu:** steak to order (rare / medium-rare / medium / well done), sometimes with sautéed onions. **Orders set the doneness target**, replacing the medium-rare house standard.
- **A service lasts ~6 real minutes** (~6 in-game hours at 60x), with waves and a rush. Resolves the open question in `game-design.md` §4 (6–8 min recommended).
- **Wrong dish:** eaten unhappily and scored low; no send-backs.

**Reason:** Keeps the first vertical slice about cooking, timing and serving temperature, using everything Phase 1 built. Plan in `plans/phase-2.md`.

---

## 2026-11-02 — RT opens slot menus; cutting is a radial timing dial

**Decisions (yours, plus details flagged for review):**
- **Slot menus open on RT** (keyboard E), not by approaching. Facing storage shows only "RT open" (and "A put back" when holding food that belongs there); while RT is held, the radial menu shows the four slots and A/B/X/Y take from them. **The prep counter works the same for its slots**; putting on / taking from the board (A) and cutting (X) need no RT.
- **Cutting is a dial, replacing the beat:** a needle swings across a **half arc (170°, tunable) like a pendulum, far right to far left and back** (Early/Late follow the direction of travel); X is pressed as it crosses the ingredient's sweet spot. Centre = Good, edges = Early/Late, outside = Miss (still cuts, less evenly). **Each ingredient has its own sweet spot** (position along the sweep, width, speed); onion: 110° from the right end, ±14° Good, ±28° Early/Late, 0.7 s per sweep, **5 cuts**. (First built as a full circle, then a one-way sweep; changed to a pendulum the same day.)
- **The needle's speed reacts to each cut:** Good speeds it up progressively (×1.15), Early/Late slow it subtly (×0.95), a Miss slows it significantly (×0.75), within 0.5×–2× of the base speed. Tunable in `TuningConfig`.
- The dial is uGUI: a dark half-arc fan with an amber Early/Late band and a green Good core on its rim and a white needle. **Every cut is made obvious:** a marker where you pressed, the fan flashes in the grade's colour (pop on Good, shake on Miss), the grade (bigger for a Miss) with "faster"/"slower", a dot per cut coloured by its grade, and a cut-quality bar that drops when you're penalised.

**Supersedes:** "follow a beat" and the 8-chop onion (milestone 6′).

---

## 2026-11-02 — HUD direction

**Decisions:** a mix of **in-world information on stations** (heat ring, bin counts, doneness gauge) and **flat button badges in Xbox colours**; prompts are **badge + short verb**; doneness reads as a **gauge with a target band and a needle**; built with **uGUI + TextMeshPro**. Plan in `plans/hud-pass.md`.

**Reason:** Least clutter over a shared screen and readable from the overhead camera, without waiting for the art phase.

---

## 2026-11-02 — Knife, mise en place counter, rhythm chopping, resting (milestone 6′)

**Decisions (yours, plus building details flagged for review):**
- **The knife lives in its own tool slot**, separate from the hands: a chef can hold an ingredient and the knife at once. It's taken from and returned to a **knife rack** with A; **A always deals with the ingredient in hand first**, the knife second.
- **The mise en place counter replaces the board-with-rail idea:** a cutting board in the middle and **four slots** for prepared ingredients in the face-button diamond. A slot takes the first ingredient that arrives and is freed when empty; each holds 4 portions (equipment tier).
- **A** puts a whole ingredient on the board (or a prepared one back into its slot); with empty hands, **A takes from the board first, then from slot A**; **B / X / Y** take from their slots.
- **Holding the knife, X chops** whatever is on the board, **following a beat**: the first chop sets it, each chop is graded Good / Early / Late / Miss (±0.07 s good, ±0.14 s early/late), and a long pause restarts the beat with one miss. **Feedback:** a pulsing beat light, a progress bar of cuts and the last grade in colour.
- **An onion takes 8 chops at a 0.28 s beat** (≈2 s) and becomes a portion of **diced onion**, which goes into a slot (or stays on the board if none can take it). Diced onion is its own ingredient: it softens (80 °C) and cooks (92 °C core) in ~3–4 in-game minutes at full heat and browns slowly; scored on doneness (92 °C target), crust, heat and **cut evenness** (the rhythm score).
- **Steak rest** is scored: in-game seconds out of the pan until served, full marks at 180 s (3 real seconds). Slicing steak waits for orders in Phase 2.
- Two new score parts, **cut** and **rest**, each weighted 0.15.

**Supersedes:** the 3-slot rail on the board (`game-design.md` §6.2) and question 1 of the board design.

---

## 2026-11-02 — Pans are picked up with their food

**Decision:** With empty hands, A at a pan takes **the pan with its food** (no lifting food out on its own). At a plate, A again moves the food onto the plate and the empty pan stays in hand; the hot pan then goes back on a hob (or any free counter). A hob's prompt shows "take pan" / "put pan".

**Reason:** Your call after playtesting: there was no way to carry a pan that had food in it. Plating now reads as a real-kitchen move: take the pan, plate, put the pan back.

**Supersedes:** "empty hands take food out of a pan first" (milestone 2a).

---

## 2026-11-02 — The pass, serving temperature and dish scoring (milestone 5′)

**Decisions (proposed while building; flagged for review):**
- **Putting a plate with food on the pass serves it.** It's scored on the spot, a quality card shows above the pass, and the plate is collected after 4 s (tunable). Nothing can be taken back or added to a served plate.
- **Score per item, averaged over the dish:**
  - **doneness:** peak core temperature against the target; full marks within the window, falling to 0 over 8 °C beyond it;
  - **crust:** average browning of both sides; pale → 0, about 0.7–1.4 → full, then falling towards burnt; burnt → 0;
  - **serving temperature:** the food's average temperature on arrival; ≥ 52 °C full marks, ≤ 38 °C nothing;
  - **freshness**, as is.
  - Weights 0.4 / 0.25 / 0.25 / 0.1; parts that don't apply (a raw tomato has no doneness) are left out. Shown as 0–5 stars in halves.
- **Doneness target is a placeholder house standard, medium-rare (54.5 °C core),** until customer orders choose it in Phase 2.
- **The cooking precision dial sets the doneness window:** ±5 °C at its most generous, ±1.5 °C at its strictest (±3.25 °C at the default 0.5).
- **Steam** rises from hot food off the heat and fades as it cools, so a waiting dish visibly loses its heat.

**Reason:** Serving temperature is one of the game's differentiators; scoring it at the pass makes plating and walking promptly matter. The card stands in for customers until Phase 2, and lets us tune the maths first.

---

## 2026-11-02 — Storage units (milestone 4′)

**Decisions (built as agreed, plus details flagged for review):**
- A storage unit has **four slots, one per face button**, with bins on the unit in the same diamond (Y back, A front, X left, B right, as seen from the camera).
- **Empty hands: a face button takes one portion** from that slot (oldest first). **Full hands: A puts the food back** into the slot for its ingredient if there's room. Anything else gets the "can't" shake.
- **Stock is real, simulated food:** a **fridge keeps it at 4 °C**, a **shelf at kitchen temperature**, so fridge food stays fresh longer. Leftovers put back keep their state (temperature, doneness).
- **Starting setup (placeholder):** a fridge with steak ×6 and a shelf with tomato ×6 and onion ×6 on the west wall, replacing the crates. Stock refills when Play starts.
- **B and Y joined the Gameplay controls** (keyboard D and W) so storage can use all four slots; at other stations they do nothing yet (the "can't" shake).
- **Button prompts:** a small box in the player's colour above the station they face shows what each button does, with glyphs for their device (A/B/X/Y and LT/RT, or X/D/A/W and Q/E). Greybox UI for now.

---

## 2026-11-02 — In-world hob controls, one piece per pan, doneness chip, revised Phase 1 order

**Decisions:**
- **Hob controls in the kitchen view:** while facing a hob, the **triggers** turn the heat smoothly (keyboard **Q** down / **E** up) and **Use** (X; keyboard A) **flips** the food in the pan.
- **One piece of food per pan.** Simpler to read and control; Use always knows what to flip.
- **Doneness chip:** while a player faces a hob, a small chip floats above the pan in the colour of the food's middle (doneness) band. No numbers.
- **Revised Phase 1 milestones:** 3′ in-world hob controls (and remove the picture-in-picture code) → 4′ storage (4-slot fridge/shelf with the A/B/X/Y prompt) → 5′ pass with serving temperature and a quality card → 6′ board and onion (rhythm chopping, rail, sautéing; resting and slicing the steak). See `plans/phase-1.md`.

**Reason:** Your choices after dropping the close-up: one press per action, readable from the shared camera, with storage and serving temperature brought forward as the differentiators.

---

## 2026-11-02 — Drop the picture-in-picture close-up

**Decision:** No close-up panel. Cooking is done in the kitchen view, directly at the station. The game's differentiators are instead:
1. **Storage with capacity** (fridges and shelves that players buy, place and stock; see `ideas.md`), the main one.
2. **Serving temperature:** how fast a dish gets from the pass to the table matters, because food cools.
3. **Food keeps cooking** off the heat (carryover, resting), as the simulation already does.
4. **Hobs and grills with adjustable power**, not on/off.

**Reason:** A panel per player makes co-op busier and harder to manage on a shared screen. The four points above keep "cooking is a craft" through timing, heat and planning rather than through minigames.

**Supersedes:** the close-up station view (`CLAUDE.md`, `game-design.md` §3.2, Phase 1 milestones 3–5) and the close-up details below. The milestone 3 framework code (commit 5e7a1c0) is to be removed or reduced once the in-world replacement controls are agreed.

---

## 2026-11-02 — Close-up framework details (milestone 3)

**Decisions (proposed by me while building; flagged for review):**
- **Keyboard triggers:** Q = left trigger (heat down), E = right trigger (heat up), either side of W.
- **Heat is continuous:** the hob dial moves smoothly with the triggers (off to full in ~1.25 s at full trigger). Use at a hob now opens its close-up; the temporary off/low/medium/high steps are gone.
- **Flip moved into the close-up now (A)**, with the stick choosing which piece. The temporary "put it back to flip" rule is removed. Basting, resting and slicing stay in milestone 4.
- **Leaving is B only** (keyboard D / Esc). "Pull the stick away from the station" from the transition proposal doesn't work while the stick aims inside the close-up.
- **No kitchen dim yet.** "Dim your side of the screen" assumed fixed side panels; with the panel near the station there's no clear "side", and dimming would hide the partner. The panel has a border in the player's colour instead. Revisit after playtesting.
- **Edge ping:** the panel border pulses orange while food in any *other* pan is close to burning.
- **Second player at a busy station** gets the "can't" shake on Use.

**Reason:** Needed to make the framework playable; each is a tuning value or easy to change.

---

## 2026-11-02 — Phase 1: steak and onion controls, fridge replaces crates

**Decisions:**
- **Steak close-up controls:** triggers turn the heat down / up, **A** flips, **hold X** bastes, **B** leaves. Keyboard equivalents for the triggers are still to choose (the face buttons follow the W/A/D/X mirror).
- **Onion close-up controls:** the **stick** moves the knife along the onion, **A** chops, **Y** rotates 90° to turn slices into dice. Even spacing gives better quality; rhythm is rewarded but you can't fail.
- **The 4-slot fridge** with the button prompt and limited stock **replaces the crates in Phase 1** (milestone 6). Stock refills each time Play starts; restocking during Prep comes in Phase 3.

**Reason:** Recommended in the Phase 1 plan and accepted.

---

## 2026-11-02 — One time scale for everything: 1 real second = 1 in-game minute

**Decision:** The whole game runs on a single clock at **60x**: one in-game minute passes in one real second, so a 12–15 hour shift plays in 12–15 minutes. **Cooking follows the same scale:** a steak that takes ~8–10 minutes in reality cooks in ~8–10 seconds, resting 5 minutes takes 5 seconds, a 30-minute caramelised onion takes 30 seconds. More than ~15 seconds for a steak is too long.

**How it's built:** the simulation uses real-world units (°C, in-game seconds) and realistic-ish constants; gameplay feeds it `realDeltaTime × TimeScale`. The scale is one tuning value (default 60).

**Consequences:**
- Techniques are short and punchy. The steak's skill is a few well-timed decisions (when to flip, when to pull, when to baste), not a long minigame.
- Windows are tight at 60x, so the "forgiving by default" decision and the cooking precision dial matter more: precision sets how wide the doneness windows are.
- Long passive processes (stocks, braises, slow onions) become 30 s–few-minute tasks, which suits juggling them during service.

**Reason:** Your call: compressed skill games, consistent time. I raised the risk that 8–10 s leaves little room for sear/flip/baste. We'll judge it by feel in milestone 4; the scale is tunable if needed.

**Supersedes:** question 1 in the Phase 1 plan (I had proposed a separate, slower cooking speed).

---

## 2026-11-02 — Phase 1: pans and plates, reading doneness, close-up panel placement

**Decisions:**
- **Pans and plates are carryable containers.** A pan normally sits on a hob but can be carried to a plate; plates come from a plate stack.
- **Doneness is read mostly by eye:** sear colour, smoke and (later) sizzle. Exact numbers only in the debug overlay; a probe thermometer may come later as an easy-mode option.
- **The close-up panel sits close to the station being used** (rather than fixed in a screen corner), so it stays connected to the station it grew out of. When the other player moves behind it, the panel **fades to partly transparent** so both players can still see what's underneath. To be judged by feel; a fixed bottom-corner panel is the fallback.

**Reason:** Pans and plates make plating a physical step, like a real kitchen. Reading by eye keeps cooking a craft (pillar 1). Keeping the panel near its station makes the transition read naturally, and fading keeps the partner visible.

---

## 2026-11-02 — Greybox kitchen layout (Phase 0)

**Decision:** The greybox kitchen is 10 × 8 m, split into zones that follow the food: **storage** (west wall) → **prep** (back wall, left) → **cooking** (back wall, right) → **pass** (front, facing a dining strip along the south wall). A two-counter **island** in the middle is the hand-off point, and a **bin** sits on the east wall. Hob, pass and bin are plain counters until Phase 1. Each zone has its own greybox colour so it reads from the overhead camera.

**Reason:** Storage to hobs is about 8 m (~1.6 s at 5 m/s), long enough that carrying matters and short enough not to drag. The food path crosses the middle, so the two players naturally meet at the island. That's where coordination should happen. This is a starting point to playtest, not a final layout. Playtested and accepted the same day.

**Constraint for all code from now on:** players move stations around during Plan (Phase 4), so nothing may assume fixed station positions or a fixed set of stations. Today `KitchenCompositionRoot` collects stations once at scene start, and the builder places them; both need replacing in Phase 4 with stations that register and unregister as they're placed, and layouts loaded from the save. Targeting already reads positions every frame.

---

## 2026-11-02 — Keyboard: arrows to move, face buttons as a W/A/D/X diamond

**Decision:** The keyboard is mainly for testing; the controller is the main way to play. Keyboard bindings mirror the Xbox face buttons as a diamond around S:

| Xbox | Keyboard | Gameplay meaning |
| --- | --- | --- |
| A (bottom) | **X** | Interact, Join, UI Submit |
| B (right) | **D** | Close-up exit, Build/UI cancel |
| X (left) | **A** | Use |
| Y (top) | **W** | (free; storage slot later) |
| Left stick | **Arrow keys** | Move, UI navigate |
| Start | **Esc** | Pause (Esc also cancels/exits) |

Enter also joins and submits. WASD no longer moves.

**Reason:** One mental model for both devices: "press A" always means the same key, which matters once storage uses all four face buttons. Supersedes the E / F keys in "Interact and Use are separate buttons" and the WASD movement in "Player 1 on keyboard". Rebinding in Controls is still planned.

---

## 2026-11-02 — Phase 0 interaction rules

**Decision:** For Phase 0 (accepted after playtesting pick up and place):
- Interact is contextual: empty hands pick up, full hands put down.
- Crates don't take items back; food goes down on counters.
- No swapping: full hands at a full counter does nothing (and the counter gives a small "can't" shake).
- Items can't be put on the floor.
- The targeted station is tinted towards the targeting player's colour.

**Reason:** Simple and readable to start with. Swapping is the rule most worth revisiting once there's more to juggle. The crates are placeholders anyway (see the storage idea in `ideas.md`).

---

## 2026-11-02 — Unity 6.3 LTS (6000.3.27f2)

**Decision:** The project uses Unity 6.3 LTS, 6000.3.27f2, created from Unity's 3D URP template. We stay on LTS releases and only move to the next LTS once it's out and stable, as a deliberate step.

**Reason:** `CLAUDE.md` asks for Unity 6 LTS. Unity Hub recommends 6.6, but that's a "Supported" release with a much shorter support window; LTS gets two years of fixes, which suits a long project.

---

## 2026-11-02 — Simulation has no UnityEngine references; designer data lives in `Game.Data`

**Decision:** `Game.Simulation` is compiled with no `UnityEngine` references at all. ScriptableObject definitions (`TuningConfig`, `IngredientDefinition`, etc.) live in a thin `Game.Data` assembly that references Simulation and converts its assets into plain C# settings objects passed into the simulation. Dependencies: Presentation → Gameplay → Data → Simulation.

**Reason:** Keeps the simulation pure, deterministic and fast to test, and the compiler enforces it. `CLAUDE.md` asked for "as little `UnityEngine` as possible"; this makes it none. The cost is one extra assembly and a small mapping step per definition.

---

## 2026-11-02 — Player 1 on keyboard, player 2 on controller; controls are rebindable in game

**Decision:** The default setup is player 1 on keyboard and mouse and player 2 on an Xbox controller. Any player can still join on a controller instead. There is no split-keyboard (two players on one keyboard) mode. A **Controls** screen in the in-game settings lets players rebind every action for keyboard and gamepad, with a reset-to-defaults option. Rebinds are stored in a small settings file, separate from restaurant saves.

**Reason:** Matches how the game will actually be played on a MacBook. Rebinding helps accessibility, and players can see what every button does. The Input System supports interactive rebinding and JSON binding overrides, so the cost is mostly UI.

---

## 2026-11-02 — Movement is snappy, no dash for now

**Decision:** Player movement reaches full speed in about 0.08 s and turns fast, Overcooked-style. All values live in `TuningConfig`. No dash in Phase 0; revisit after Phase 2 if walking feels slow during service.

**Reason:** Inputs must feel instant (`CLAUDE.md`). A dash is easy to add later but changes kitchen pacing and layout, so we judge it once service exists.

---

## 2026-11-02 — Interact and Use are separate buttons

**Decision:** **Interact** (gamepad A; keyboard X since the keyboard remap below) is a contextual pick up / place. **Use** (gamepad X; keyboard A) uses a station or enters its close-up. Use is bound from Phase 0 but does nothing until Phase 1.

**Reason:** Keeping the two apart stops a player who wants to put something down from accidentally entering a close-up, and Phase 1 slots in without remapping.

---

## 2026-11-02 — Board rail: full rail leaves the cut on the board

**Decision:** If every rail container is full (or none matches) when a cut finishes, the cut product stays on the board surface. The board can't start a new cut until it's cleared.

**Reason:** The pressure is visible from the overhead camera and fair, and nothing moves into the player's hands without them asking. The other options either surprised the player (auto-pickup) or felt like the game blocking them (refusing to start a cut).

---

## 2026-11-02 — Board rail: one ingredient and one cut per container

**Decision:** Each rail container holds exactly one ingredient in one cut (diced onion and sliced onion need separate containers).

**Reason:** Easy to read from overhead, easy to score, and it avoids cross-contamination edge cases. Mixes (e.g. a soffritto) can be revisited with Spanish home cooking in Phase 6 if a cuisine needs them.

---

## 2026-11-02 — Board rail: 3 slots to start, more via equipment tiers

**Decision:** The board station has a rail of small containers for cut product. The starting rail has 3 slots; equipment tiers add more (rough guide 3 → 4 → 6, to tune). The slot count comes from data (`StationDefinition` / `EquipmentTier`), not code.

**Reason:** Three slots is tight enough that prep choices matter in the greybox, and upgrades give a visible payoff later. Accepts the board-rail idea from `ideas.md`.

---

## 2026-11-02 — Close-up techniques are forgiving; precision raises quality

**Decision:** By default, sloppy input still completes a technique but lowers its quality (uneven dice, weaker sear). Techniques are hard to fail outright. The minigame complexity dial can make them stricter.

**Reason:** Keeps "cooking is a craft" (precision is rewarded) without punishing new players with ruined food from a slightly off stick input, in line with the forgiving-input rule in `CLAUDE.md`.

---

## 2026-11-02 — One player per station close-up

**Decision:** Only one player can be in a station's close-up at a time. A second player who tries to enter sees the station marked busy and is gently blocked.

**Reason:** Simple to build and easy to read. It also makes layout matter: more boards and hobs mean more parallel work. A shared surface (one cuts, the other plates) would be a fun co-op moment but complicates input and rendering a lot; we can revisit it later.

---

## 2026-11-02 — Kitchen view dims slightly on the close-up player's side

**Decision:** While a player is in close-up, the kitchen view gets a subtle dim or vignette on that player's side of the screen. The rest of the kitchen stays fully visible. The strength is a tuning value.

**Reason:** Makes it clear where the player's focus is, while keeping pings, the partner and the rest of the kitchen readable, since the world keeps running.

---

## 2026-11-02 — Fixed kitchen camera now; framing camera only if size tiers need it

**Decision:** Phases 0–3 use a fixed overhead camera on the greybox kitchen. A camera that frames both players and zooms is deferred to Phase 4, and built only if larger size tiers don't fit a fixed view.

**Reason:** A fixed camera is the most readable and the least work, and a small kitchen doesn't need framing. Framing can feel floaty, so we add it only once we know it's needed.

---

## 2026-11-02 — Local co-op is 1–2 players; 4 players is a stretch goal

**Decision:** The game targets 1–2 local players. Support for 3–4 players is a stretch goal, not a requirement for any roadmap phase.

**Reason:**
- The close-up station view (picture-in-picture) can be large with two players (roughly a third of the screen per player) and stays readable. With four players, panels would be too small for precise stick and combo techniques.
- The 60 fps target on a MacBook is easier with two close-up cameras rendering to RenderTextures instead of four.
- Co-op gets tighter with two players, with natural roles (e.g. line cook vs. pass/front of house).
- Larger restaurants have to lean on hired staff, which makes the staff system matter more.

**Cost:** We lose the 3–4 player couch-party audience that PlateUp and Overcooked serve. Revisit once the 2-player game is fun.

**Implications:** Code shouldn't hard-code a limit of two where that's cheap to avoid (e.g. `PlayerInputManager` max players comes from config), but UI layout, camera framing and performance budgets are designed and tested for two.

## 2026-11-08 — The main menu's night city is modelled in the kitchen's style

**Decision:** The main menu backdrop is a modelled low night city (`SM_City.fbx`, built by `blender/build_pilot_models.py`) in the kitchen's toon look: bevelled blocks with cornices, framed lit windows with sills, shopfronts with striped awnings, roof water towers, lantern lampposts, rounded cars with head- and taillights, and round-crowned trees. `MainMenuBuilder` swaps the models onto its greybox pieces and keeps the primitives as a fallback when the FBX is missing. The menu UI and camera are unchanged.

**Reason:** The user asked for the menu city to follow the kitchen's 3D toon style (chunky models, toon ramp, soft ink), matching their mockup: a skyline under stars, warm windows, and glowing city lights in the lower corners that show around the UI panels.
