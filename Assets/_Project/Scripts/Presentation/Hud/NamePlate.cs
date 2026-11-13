using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The name plate (the user's pick, 2026-11-13): the office's player cards and the money card.
    /// A dark card with a chunky ink outline, the same ink as the scene's toon outline, a band of
    /// the owner's colour along its bottom, and a round medallion (a chef's face, a coin) breaking
    /// out over its top edge. Sizes come from HudStyle (Name plates). Coordinates are canvas
    /// pixels from the top-left of <c>parent</c>, y down, as UiKit draws.
    /// </summary>
    public static class NamePlate
    {
        /// <summary>
        /// The plate itself: the ink outline, the fill and the colour band, <paramref name="width"/>
        /// by <paramref name="height"/> at (<paramref name="x"/>, <paramref name="y"/>).
        /// </summary>
        public static void Draw(HudStyle style, RectTransform parent, Color band, float x, float y, float width, float height)
        {
            var ink = style.PlateInk;
            var stroke = style.PlateStroke;
            var radius = style.PlateRadius;
            var inner = Mathf.Max(0f, radius - stroke / 2f);
            UiKit.StrokedBox(parent, "Plate", style.CardBackground, ink, stroke, x, y, width, height, radius);
            // The band: a rounded box at the bottom of the fill, its top covered by a rounded strip
            // of the fill, so only a band following the card's own rounded corners shows.
            var half = stroke / 2f;
            var left = x + half;
            var fillWidth = width - stroke;
            var bottom = y + height - half;
            var bandHeight = style.PlateBand;
            var rounded = Mathf.Max(bandHeight, inner) + bandHeight;
            UiKit.RoundBox(parent, "Band", band, left, bottom - rounded, fillWidth, rounded, inner);
            UiKit.RoundBox(parent, "BandCover", style.CardBackground, left, bottom - rounded, fillWidth, rounded - bandHeight, Mathf.Min(inner, (rounded - bandHeight) / 2f));
        }

        /// <summary>
        /// A round medallion centred on (<paramref name="cx"/>, <paramref name="cy"/>): an ink ring,
        /// a ring of <paramref name="ring"/>, and inside a circular mask holding whatever is added
        /// to the returned rect (a portrait, a coin). <paramref name="diameter"/> is the inside.
        /// </summary>
        public static RectTransform Medallion(HudStyle style, RectTransform parent, Color ring, float cx, float cy, float diameter)
        {
            var ink = style.PlateStroke;
            var ringWidth = style.PlateRing;
            var outer = diameter + 2f * (ringWidth + ink);
            UiKit.Ellipse(parent, "MedallionInk", style.PlateInk, cx - outer / 2f, cy - outer / 2f, outer, outer);
            var coloured = diameter + 2f * ringWidth;
            UiKit.Ellipse(parent, "MedallionRing", ring, cx - coloured / 2f, cy - coloured / 2f, coloured, coloured);
            var inside = UiKit.Ellipse(parent, "Medallion", style.CardBackground, cx - diameter / 2f, cy - diameter / 2f, diameter, diameter);
            inside.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            return inside.rectTransform;
        }

        /// <summary>How far the medallion of a plate reaches above the plate's top edge, ink included.</summary>
        public static float Reach(HudStyle style, float diameter, float centreBelowTop) =>
            Mathf.Max(0f, diameter / 2f + style.PlateRing + style.PlateStroke - centreBelowTop);
    }
}
