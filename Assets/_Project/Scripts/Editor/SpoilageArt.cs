using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Mould for spoiled food (decision 2026-11-11): a few fuzzy pale-green spots grown over the top
    /// of a piece of food, hidden until the food's view shows them. Sized from the food's own shape,
    /// so one recipe fits a steak, an onion or a pile of diced onion. Greybox until the art pass.
    /// </summary>
    public static class SpoilageArt
    {
        // Two kinds of spot, alternating: grey-white fuzz and green bloom, each with a darker heart.
        private static readonly Color[] Fuzz = { new Color(0.86f, 0.88f, 0.82f), new Color(0.52f, 0.66f, 0.44f) };
        private static readonly Color[] Heart = { new Color(0.55f, 0.62f, 0.5f), new Color(0.32f, 0.46f, 0.3f) };
        private const int Spots = 9;
        // Golden angle, so the spots spread evenly over the top without a pattern.
        private const float Turn = 2.3999632f;

        /// <summary>
        /// Adds the spots under <paramref name="parent"/>, over the top of <paramref name="food"/>'s
        /// renderers where they stand now, and hides them.
        /// </summary>
        public static GameObject AddMould(Transform parent, IEnumerable<Renderer> food, string name = "Mould")
        {
            var bounds = new Bounds();
            var any = false;
            foreach (var renderer in food)
            {
                if (renderer == null)
                    continue;
                if (any)
                    bounds.Encapsulate(renderer.bounds);
                else
                    bounds = renderer.bounds;
                any = true;
            }

            var mould = new GameObject(name);
            mould.transform.SetParent(parent, false);
            var fuzz = new[] { StyleTest.EnsureToonMaterial("MouldFuzz", Fuzz[0]), StyleTest.EnsureToonMaterial("MouldBloom", Fuzz[1]) };
            var heart = new[] { StyleTest.EnsureToonMaterial("MouldHeart", Heart[0]), StyleTest.EnsureToonMaterial("MouldBloomHeart", Heart[1]) };
            var size = Mathf.Min(bounds.size.x, bounds.size.z);
            for (var i = 0; i < Spots && any; i++)
            {
                // Spread over the top, within 70% of the way to the edge, sitting on a dome
                // the food's height: flat food gets flat-lying spots, round food a cap of them.
                var r = 0.75f * Mathf.Sqrt((i + 0.5f) / Spots);
                var u = r * Mathf.Cos(i * Turn);
                var v = r * Mathf.Sin(i * Turn);
                var lift = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u - v * v));
                var at = new Vector3(bounds.center.x + u * bounds.extents.x, bounds.center.y + lift * bounds.extents.y, bounds.center.z + v * bounds.extents.z);
                var spot = size * (0.1f + 0.045f * (i % 3));
                var kind = i % 2;

                var blob = GreyboxAssets.CreatePrimitive(PrimitiveType.Sphere, "Spot", mould.transform, fuzz[kind], keepCollider: false);
                blob.transform.position = at;
                blob.transform.localScale = new Vector3(spot, spot * 0.45f, spot * 0.85f);
                blob.transform.rotation = Quaternion.Euler(0f, i * 47f, 0f);
                var middle = GreyboxAssets.CreatePrimitive(PrimitiveType.Sphere, "Heart", blob.transform, heart[kind], keepCollider: false);
                middle.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                middle.transform.localScale = Vector3.one * 0.5f;
            }
            mould.SetActive(false);
            return mould;
        }
    }
}
