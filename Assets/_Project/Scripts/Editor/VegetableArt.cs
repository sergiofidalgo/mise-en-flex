using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Whole vegetables as cartoony models (decision 2026-11-08): the onion's coppery bulb with its
    /// stripes, tuft and roots. The body is the ingredient's surface (its colour is the raw colour
    /// the ingredient view starts from); the other parts sit under it, so they turn and vary with
    /// it. Origin at the base.
    /// </summary>
    public static class VegetableArt
    {
        private static readonly Dictionary<string, (string Part, string Mesh, Color Color)[]> Parts =
            new Dictionary<string, (string, string, Color)[]>
            {
                ["Onion"] = new[]
                {
                    ("Body", "OnionBody", new Color(0.83f, 0.46f, 0.22f)),
                    ("Lines", "OnionLines", new Color(0.6f, 0.29f, 0.13f)),
                    ("Tuft", "OnionTuft", new Color(0.8f, 0.68f, 0.45f)),
                    ("Root", "OnionRoot", new Color(0.42f, 0.31f, 0.2f)),
                },
            };

        /// <summary>The vegetable's model as <paramref name="name"/> under <paramref name="parent"/>; its body is the returned object.</summary>
        public static GameObject Create(string id, string name, Transform parent)
        {
            var model = $"SM_{id}";
            var parts = Parts.TryGetValue(id, out var known) ? known : new[] { ("Body", $"{id}Body", Color.white) };
            GameObject body = null;
            foreach (var (part, mesh, color) in parts)
            {
                var material = StyleTest.EnsureToonMaterial($"{id}{part}", color);
                var go = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, body == null ? name : part,
                    body == null ? parent : body.transform, material, keepCollider: false);
                if (!PilotArt.TryUseMesh(go, model, mesh))
                {
                    Object.DestroyImmediate(go);
                    continue;
                }
                if (body == null)
                    body = go;
            }
            return body;
        }
    }
}
