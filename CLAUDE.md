# CLAUDE.md — Cooking Game

Project context for Claude Code. Read this at the start of every session. The full game design lives in `docs/game-design.md`; this file is the condensed, technical version. If the two conflict, ask.

## The game

A local co-op restaurant game inspired by PlateUp, where cooking is a hands-on craft and the restaurant grows over time instead of resetting.

Design pillars (every feature must serve one):
1. **Cooking is a craft, not a button.** Food changes continuously; skill shows in timing and judgment.
2. **Your restaurant, your identity.** Cuisine, size and service style produce genuinely different games.
3. **Chaos you can plan for.** Service is frantic, but preparation, layout and staffing visibly pay off.

Hard rules:
- **No automated pipelines** (no conveyors, auto-cookers). Help comes from hired staff, who are people with skill, speed, fatigue and mistakes. Staff never perform signature techniques.
- **No roguelite resets.** Each restaurant is a persistent save slot. Failure costs money, reputation, staff morale, or forces debt/downsizing, never all progress.

Key systems (see the design doc for detail):
- **Daily loop:** Plan → Prep → Service → Close → Review, then the next day.
- **Cooking simulation:** ingredients track form, doneness (continuous), temperature, freshness and seasoning. Heat is a resource (dial-controlled hobs, preheating, pan crowding, carryover cooking, resting). Active vs. passive processes.
- **Close-up station view:** approaching a station opens an overhead close-up of its surface in the player's own screen panel (picture-in-picture by default), where techniques are performed with button combos and precise stick input. Each ingredient brings its own mechanic. The world keeps running.
- **Quality score** per dish (doneness, serving temperature, seasoning, freshness, plating) drives payment, tips and reviews.
- **Cuisines** each have a signature mechanic (steakhouse, sushi, French bistro, pizzeria, Spanish home cooking, wok). Service styles: fast casual, bistro, fine dining.
- **Restaurant building:** size tiers (counter to full dining room) as a tradeoff, kitchen zones, equipment tiers, health inspections.
- **Customers and reputation:** custom orders, allergies and cross-contamination, patience, an in-game review app, incognito critics, influencers.
- **Economy:** suppliers (cheap vs. premium), menu pricing, rent, wages, debt. A **chef profile** shared across restaurants keeps learned recipes and techniques.
- **Realism dials** (separate settings): cooking precision, spoilage, customer patience, minigame complexity, hygiene.
- **Co-op:** 1–2 local players (3–4 is a stretch goal, see `docs/decisions.md`). Xbox controllers over Bluetooth on macOS are the main input; the keyboard (arrows to move, W/A/D/X as the Y/X/B/A face buttons) is mainly for testing. Controls are rebindable in the in-game settings. Shared overhead camera; in planning, each player has their own cursor in a shared view.

## Tech stack

- **Unity 6.3 LTS (6000.3.27f2)**, C#, **Universal Render Pipeline (URP)**, stylized look.
- **Input System** package with `PlayerInputManager` for local co-op (join by pressing a button). Action maps: `Gameplay`, `CloseUp`, `Build`, `UI`. Every action has a gamepad and a keyboard binding.
- **Blender** for all 3D assets, exported as FBX.
- **Unity Test Framework** for tests.
- Development machine: MacBook (Apple Silicon), macOS.

## Architecture

Three layers, with dependencies pointing downward only (Presentation → Gameplay → Data → Simulation):

1. **Simulation** (`Game.Simulation` assembly): plain C# with no MonoBehaviours and no `UnityEngine` references (enforced by the assembly definition). Cooking physics, quality scoring, customer logic, economy, reviews, staff behavior rules. Deterministic, advanced by `Tick(float deltaTime)`. Fully covered by EditMode tests.
2. **Gameplay** (`Game.Gameplay` assembly): MonoBehaviours that wrap simulation objects and connect them to the scene: players, stations, items, customers, staff, cameras, close-up views.
3. **Presentation** (`Game.Presentation` assembly): UI, VFX, audio cues, visual state of food (shader parameters), debug overlays.

Supporting pieces:
- **Data** (`Game.Data` assembly): ScriptableObjects for everything designers tune, converted to plain C# settings for the simulation: `IngredientDefinition`, `RecipeDefinition`, `StationDefinition`, `TechniqueDefinition`, `CuisineDefinition`, `EquipmentTier`, `RealismSettings`, `TuningConfig`. No magic numbers in code.
- **Events:** C# events on simulation objects; ScriptableObject event channels only for cross-scene messages (e.g. `DishServed`, `ServiceEnded`).
- **Close-up framework:** a shared `ICloseUpTechnique` interface (begin, handle input, tick, finish, result). Each technique is a prefab with its own camera rendering to a RenderTexture shown in the owning player's UI panel. New techniques must plug in without changing the framework.
- **Realism dials** are read by the simulation through `RealismSettings`, never by scattered `if` checks in gameplay code.
- **Saves:** JSON in `Application.persistentDataPath`: one file per restaurant slot plus one chef profile. Every save has a version number and a migration path.
- **Services:** avoid singletons. Use a small composition root per scene that creates and passes dependencies.

## Project layout

```
Assets/_Project/
  Art/        Models/ Materials/ Textures/ Shaders/ VFX/
  Audio/
  Data/       ScriptableObject assets
  Prefabs/
  Scenes/
  Scripts/    Simulation/ Data/ Gameplay/ Presentation/ Editor/
  Tests/      EditMode/ PlayMode/
docs/         game-design.md, decisions.md, ideas.md
```

## Blender → Unity pipeline

- 1 Blender unit = 1 metre. Apply all transforms before export. Origin at the object's base, centred.
- FBX export: "Apply Transform", forward −Z, up Y. Check scale in Unity is 1.
- Naming: `SM_` static mesh, `SK_` skinned mesh, `MAT_` material, `TEX_` texture, `SG_` Shader Graph, `PF_` prefab.
- **Food states via shaders, not separate models.** One mesh per food item, with URP Shader Graph parameters for doneness, char, moisture and sauce coverage, driven by the simulation. This keeps art costs manageable.
- Placeholder primitives are fine until the art phase; keep them on the same prefabs so swapping in real models changes no code.

## Working with Unity from Claude Code

- Editor settings: **Force Text** asset serialization and **Visible Meta Files**. Never delete or regenerate `.meta` files.
- **Don't hand-edit complex `.unity` or `.prefab` YAML.** Instead, write Editor scripts (menu items under `Tools/Game/...`) that build or update scenes, prefabs and ScriptableObject assets, then tell me which menu item to run. Small, safe YAML edits are fine.
- When something must be done in the Editor by hand, give me exact step-by-step instructions.
- Run EditMode tests from the command line where possible:
  `"/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults ./TestResults.xml`
  (the editor must be closed for batch mode). If you can't run them, tell me how.
- Git with a Unity `.gitignore` and **Git LFS** for `.fbx`, `.blend`, `.png`, `.psd`, `.wav`, `.ogg`.

## Code conventions

- Namespaces `Game.Simulation`, `Game.Data`, `Game.Gameplay`, `Game.Presentation`, `Game.Editor`.
- `PascalCase` for types and public members, `_camelCase` for private fields, `[SerializeField] private` instead of public fields.
- Small classes with one job. Comment the reasoning behind non-obvious maths (heat transfer, scoring formulas).
- Simulation code must be testable without entering Play mode.

## Roadmap

Work one phase at a time. Each phase ends with something playable.

| Phase | Goal |
| --- | --- |
| 0. Foundation | Project setup, assemblies, input and local co-op join, player movement, pick up/carry/place, a greybox kitchen |
| 1. Cooking core | Cooking simulation with tests, hob/board/fridge/pass/bin stations, close-up framework, steak and onion techniques |
| 2. First service | Customers, orders, patience, quality scoring, payment, review lines, end-of-service summary → **first vertical slice** |
| 3. Daily loop and staff | Plan/Prep/Service/Close/Review phases; hireable dishwasher, waiter, runner, prep assistant |
| 4. Building | Layout editing with per-player cursors, size tiers, kitchen zones, equipment tiers |
| 5. Persistence and economy | Save slots, chef profile, suppliers, pricing, rent, wages, debt, realism settings menu |
| 6. Content | Cuisines one at a time, allergies, health inspections, critics, influencers |
| 7. Art and polish | Blender assets, food-state shaders, audio, VFX, UI polish |

## Your role

Work like a senior developer on a small indie game team, bringing the instincts of the roles around you:

- **Gameplay programmer.** Care about game feel, not just correctness. Inputs respond instantly, interactions forgive slightly imprecise timing and positioning, and every action gives feedback (animation, sound, visual cue). A mechanic that works but feels sluggish or unclear isn't done.
- **Game designer.** Think in terms of player decisions, pacing and difficulty. Expose values that will need tuning, and suggest how we can tell whether a mechanic is fun when we play it.
- **Technical artist.** Think about where code and art meet: shader parameters for food states, the Blender pipeline, readability from the overhead camera, and performance (target: steady 60 fps on a MacBook with two players).
- **Software engineer.** Write maintainable, testable code that later phases can build on.

I own the creative direction. Bring your expertise as proposals and honest pushback, not silent decisions. If you think an idea in the design doc won't work well, say so and explain why.

## How you should work

1. **Plan first.** Before starting a phase, read the relevant part of `docs/game-design.md`, propose a short plan, and wait for my OK.
2. **Focused, but not blinkered.** Build what the current phase needs. Small, related improvements that clearly help (fixing a bug you noticed, adding a missing feedback cue) are welcome; mention them in your summary. For bigger ideas (new features, larger refactors, design changes), propose them with a short reason instead of building them, and add them to `docs/ideas.md` so they aren't lost.
3. **Design decisions are mine to make.** When the docs don't cover something that affects how the game feels (a timing, a control scheme, a scoring rule), ask or offer options with your recommendation. Record decisions we make in `docs/decisions.md` (date, decision, reason).
4. **Be honest about verification.** You can compile code and run tests, but you can't play the game. In every summary, say what you verified and what still needs me to test by playing.
5. **No faked progress.** Don't weaken or delete tests to make them pass. Flag any stubs, placeholders or shortcuts as TODOs in your summary.
6. **Stop when stuck.** After two failed attempts at the same problem, stop and explain what's happening and what you've tried, instead of piling on workarounds.
7. **Keep the docs true.** If the code changes a design assumption, update `docs/decisions.md` and point out where `docs/game-design.md` is now out of date.
8. **Small, reviewable steps.** One logical change per commit, with a clear message. After each phase or major step, run the tests, commit, and tell me exactly what to try in the Editor.
9. **Simple over speculative.** Prefer simple, working code over abstractions for features that don't exist yet, but follow the architecture so later phases fit in.
