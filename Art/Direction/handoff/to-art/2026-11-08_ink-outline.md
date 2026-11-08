# Ink outlines: width and colour differ from the art bible

*From the code side, 2026-11-08.*

## The question

The art bible's Rendering section says **"Outlines 2 px at 1080p, color Ink" (`#3A2414`)**. After playing the first version, the user asked for softer ink and approved this:

- **Width:** 1 px at 1080p, never more than 2 px. At 1.5 px, scaled by screen height, it came out at about 3 px on the MacBook's Retina display.
- **Colour:** a darker shade of whatever is underneath (scene colour × 0.45), with a quarter of the house brown mixed in, at 50% strength. It is no longer one fixed ink colour at full strength.
- **Where:** mostly silhouettes. Creases (cabinet doors, counter lips, plank grooves) mostly don't ink.

Their reaction to the strong version was that it "completely removes the fun". Should the art bible's Rendering line change to match? Or should the code go back to the bible's values once this has been tried in play?

A smaller point: the code's outline brown is `#332117` (`HouseStyle.OutlineColor`), which is not quite the bible's Ink `#3A2414`. Which should win?

## Still open

The old inverted-hull outlines (thin, fixed brown, about half a pixel at the kitchen shot) are still on as well. With the softer ink alone, the chef is barely outlined: white on white gives no line. Keeping a hull outline on the chef only is one option, and the user is being asked.

## Files

- `Assets/_Project/Art/Shaders/SH_InkOutline.shader`: the screen-space ink, with all values as material properties.
- `Assets/_Project/Art/Materials/MAT_Post_InkOutline.mat`: the values in use.
- `Assets/_Project/Scripts/Editor/RenderLook.cs`: sets the material and the pipeline on every build.
- `Assets/_Project/Art/Shaders/ToonCommon.hlsl`: the 3-band ramp, matching the bible.
- `docs/decisions.md`: entries "Screen-space ink, three-band ramp, banded SSAO" and "Softer ink".

## Screenshots (1920×1080, game camera)

- `2026-11-08_ink-outline.png`: the softer ink, with the old hull outlines still on (what's in the game now).
- `2026-11-08_ink-outline_no-hulls.png`: the softer ink alone.
