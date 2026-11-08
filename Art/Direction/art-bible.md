# Art bible

These are starting values sampled by eye from the screenshots. The Art Director project refines them and keeps this file updated; dated changes go in `CHANGELOG.md`.

**Pillars:** warm, cozy, readable under chaos. Every station must be identifiable by silhouette at couch distance.

## Palette

| Role | Hex | Use |
| --- | --- | --- |
| Ink | `#3A2414` | Outlines, text on light panels |
| Wood dark | `#5C3A21` | Panel fill, walls, frames |
| Wood mid | `#8B5A33` | Cabinets, dining floor |
| Cream | `#EADFC6` | Countertops, light panels |
| Accent orange | `#E8873A` | Selection, panel borders, chef apron trim |
| Kitchen floor | `#4A4540` | Darkened tile for contrast |
| Good / Xbox A | `#6AAE4A` | Success, serve |
| Info / Xbox X | `#3B72D9` | Neutral actions |
| Warn / Xbox Y | `#E0B341` | Timers running low |
| Bad / Xbox B | `#D9493B` | Burnt, failed, angry |

## Rendering

Outlines 2 px at 1080p, color Ink; toon ramp with 3 bands; one shared palette texture for props (UV islands on swatches) so recoloring is one file.

## Modeling

Chunky and slightly oversized props (stations ~1.15× real proportions); bevels on all hard edges; ~300–1,500 tris per prop; no thin parts players can't read from above.

## UI kit

Rounded rectangles, 12 px radius; Wood dark fill, 3 px Accent orange border; 1 px inner highlight on top edge. Button glyphs always official Xbox colors. Status colors always paired with an icon or shape (colorblind-safe).

## Typography

A rounded display face for titles and numbers (e.g. Fredoka) and a sturdy UI face for body (e.g. Nunito), both free under the SIL Open Font License. Minimum 28 px at 1080p for anything read during play.

## Icons

64 px base grid, 4 px Ink outline, two-tone fill + one highlight, slight top-left light. Food icons match their 3D model's colors.
