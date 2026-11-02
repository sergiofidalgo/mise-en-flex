using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Where a close-up panel goes on screen (decision 2026-11-02: near the station being used).
    /// Pure maths, in screen pixels with the origin at the bottom left.
    /// </summary>
    public static class CloseUpPanelLayout
    {
        /// <summary>
        /// Places a square panel of <paramref name="size"/> next to <paramref name="station"/>, pushed
        /// towards the screen centre so it doesn't cover the station, then clamped inside the screen.
        /// </summary>
        public static Rect Place(Vector2 station, Vector2 screen, float size, float margin)
        {
            var centre = screen / 2f;
            var towardsCentre = centre - station;
            var direction = towardsCentre.sqrMagnitude > 1f ? towardsCentre.normalized : Vector2.up;

            // Distance from the station to the panel's centre so the panel's nearest edge sits
            // `margin` away from it. For a square seen along `direction`, the half-extent along
            // that direction is half the size divided by the larger of |cos| and |sin|.
            var halfExtent = size / 2f / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
            var panelCentre = station + direction * (halfExtent + margin);

            var min = panelCentre - Vector2.one * (size / 2f);
            min.x = Mathf.Clamp(min.x, margin, Mathf.Max(margin, screen.x - margin - size));
            min.y = Mathf.Clamp(min.y, margin, Mathf.Max(margin, screen.y - margin - size));
            return new Rect(min, Vector2.one * size);
        }
    }
}
