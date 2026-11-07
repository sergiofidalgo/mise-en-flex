using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Keeps HUD pieces fully on screen. Cards follow points in the world, and a station near the
    /// top edge (or a long card, like the shop list) would push them off it: they slide back in
    /// instead, so every line stays readable wherever the camera puts the thing they belong to.
    /// </summary>
    public static class HudBounds
    {
        private static readonly Vector3[] InnerCorners = new Vector3[4];
        private static readonly Vector3[] OuterCorners = new Vector3[4];

        /// <summary>Slides <paramref name="rect"/> so all of it is inside <paramref name="canvas"/>, <paramref name="margin"/> canvas units from each edge.</summary>
        public static void KeepInside(RectTransform rect, RectTransform canvas, float margin)
        {
            rect.GetWorldCorners(InnerCorners);
            canvas.GetWorldCorners(OuterCorners);
            var shift = Shift(Bounds(InnerCorners), Bounds(OuterCorners), margin * canvas.lossyScale.x);
            if (shift != Vector2.zero)
                rect.position += (Vector3)shift;
        }

        /// <summary>
        /// How far to move <paramref name="inner"/> to sit inside <paramref name="outer"/> shrunk by
        /// <paramref name="margin"/>. Something too big to fit keeps its top and left edges on screen:
        /// that's where a card's first line and first word are.
        /// </summary>
        public static Vector2 Shift(Rect inner, Rect outer, float margin)
        {
            var x = Axis(inner.xMin, inner.xMax, outer.xMin + margin, outer.xMax - margin, keepLow: true);
            var y = Axis(inner.yMin, inner.yMax, outer.yMin + margin, outer.yMax - margin, keepLow: false);
            return new Vector2(x, y);
        }

        private static float Axis(float min, float max, float low, float high, bool keepLow)
        {
            if (max - min > high - low)
                return keepLow ? low - min : high - max;
            if (min < low)
                return low - min;
            if (max > high)
                return high - max;
            return 0f;
        }

        private static Rect Bounds(Vector3[] corners) =>
            Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
}
