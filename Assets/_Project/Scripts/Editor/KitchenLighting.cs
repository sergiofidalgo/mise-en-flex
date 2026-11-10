using Game.Presentation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Lamps for the kitchen (user's note, 2026-11-09: next to the office at night the kitchen
    /// looked flat, but it shouldn't get the office's strong light and shadow). A few broad, soft,
    /// overlapping lights that only warm and lift the scene evenly: ceiling lights over the
    /// kitchen, low pendants over the dining tables, and the grill's own glow now counting too.
    /// Kept wide and gentle on purpose, so no pools or dark corners appear and the kitchen still
    /// reads as the calm, even place to work in. Called by GreyboxKitchenBuilder.
    /// </summary>
    public static class KitchenLighting
    {
        // A dusk interior's warm white, a step less orange than the office's lamps.
        private static readonly Color Ceiling = new Color(1f, 0.88f, 0.72f);
        private static readonly Color Pendant = new Color(1f, 0.8f, 0.6f);

        // How much the kitchen's lamps count; the office's is 3. Low, so they warm rather than light.
        private const float Strength = 1.4f;

        /// <summary>
        /// Adds the kitchen's lamps where its layout says (the grill spans x −4…4, z −7…2, its dining
        /// room z −7…−3): ceiling lights over the kitchen, pendants over the dining room, sconces on
        /// the dining room's side walls.
        /// </summary>
        public static void Add(KitchenLayout layout)
        {
            var root = new GameObject("KitchenLamps").transform;
            var switchOn = new SerializedObject(root.gameObject.AddComponent<LampLighting>());
            switchOn.FindProperty("_localLights").floatValue = Strength;
            switchOn.ApplyModifiedPropertiesWithoutUndo();

            // Three wide ceiling lights over the kitchen, overlapping, so the light is even.
            foreach (var x in layout.CeilingLightXs)
                Point(root, "CeilingLight", new Vector3(x, 4f, -0.4f), Ceiling, 7.5f, 0.9f);
            // Two pendants over the dining room, a touch warmer.
            foreach (var at in layout.PendantPositions)
                Point(root, "DiningPendant", at, Pendant, 6.5f, 0.8f);
            // Sconces on the dining room's side walls: you see where the light comes from, and
            // each throws only a small, soft glow.
            var brass = StyleTest.EnsureToonMaterial("SconceBrass", new Color(0.86f, 0.62f, 0.26f));
            var glow = PilotArt.EnsureUnlitMaterial("SconceGlow", new Color(1f, 0.86f, 0.6f));
            glow.SetColor("_BaseColor", new Color(1f, 0.86f, 0.6f));
            var wall = layout.HalfWidth - 0.15f;
            foreach (var x in new[] { -wall, wall })
            foreach (var z in layout.SconceZs)
            {
                var sconce = new GameObject("Sconce").transform;
                sconce.SetParent(root, false);
                sconce.localPosition = new Vector3(x, 1.0f, z);
                var plate = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, "Plate", sconce, brass, keepCollider: false);
                plate.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                plate.transform.localScale = new Vector3(0.12f, 0.08f, 0.22f);
                var bulb = GreyboxAssets.CreatePrimitive(PrimitiveType.Sphere, "Bulb", sconce, glow, keepCollider: false);
                bulb.transform.localPosition = new Vector3(0f, 0.16f, 0f);
                bulb.transform.localScale = Vector3.one * 0.16f;
                Point(sconce, "SconceLight", new Vector3(-Mathf.Sign(x) * 0.3f, 0.5f, 0f), Pendant, 3.5f, 0.6f);
            }
        }

        private static void Point(Transform parent, string name, Vector3 at, Color color, float range, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }
    }
}
