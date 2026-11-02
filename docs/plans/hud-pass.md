# HUD and signifiers pass

*Approved 2026-11-02, after Phase 1. Decisions in `../decisions.md`; background in `../ideas.md` ("HUD and signifiers pass").*

**Goal:** replace the greybox UI with one clear visual language, readable from the overhead camera with two players, before Phase 2 adds customers.

**Direction:**
- **Mix of in-world and badges:** information lives on the stations where possible (heat ring around the burner, counts on bins, doneness gauge over the pan); button prompts are flat badges in Xbox colours.
- **Prompts:** button badge + short verb, e.g. (A) take, (X) flip. An "icons only" option can come later.
- **Doneness:** a gauge from raw to well done with the target band and a needle at the core.
- **Tech:** uGUI + TextMeshPro (screen-space canvas anchored to world positions; world-space where it sits on a station).

## Steps

| Step | What | Replaces | Status |
| --- | --- | --- | --- |
| H1 | TextMeshPro; HUD style asset (colours, fonts, sizes, fades); button badges (Xbox A/B/X/Y circles, keyboard keycaps, trigger pills); HUD canvas following world positions | — | Done |
| H2 | Prompt cards: badge + verb, fade in/out when facing a station; storage and prep as a badge diamond with counts | IMGUI station prompts | Done |
| H3 | Heat ring around each burner (fills cold → hot, searing zone marked); burn warning icon | Heat pips | |
| H4 | Doneness gauge over the pan with target band and needle | Doneness ball | |
| H5 | Serve card (stars pop in, a bar per score part); chop approach ring onto the beat; counts on bins; coloured ring under each chef | IMGUI quality card and rhythm display | |

Not in scope: the debug overlay (stays IMGUI), smoke and steam, sound (no assets yet), colour-blind palettes (later, but colours come from the style asset so they can be swapped).

## How we'll know it works

- A new player can cook and serve a steak without being told the buttons.
- From a glance at the kitchen you can tell which hobs are on, how hot, and how close each steak is to its target.
- With two players, prompts never cover each other or the station they're about.
