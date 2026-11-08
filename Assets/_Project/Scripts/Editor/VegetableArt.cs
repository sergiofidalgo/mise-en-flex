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
                // Cut onion: a heap of cream dice (decision 2026-11-08), browning as one as it cooks.
                ["DicedOnion"] = new[] { ("Body", "DicedOnionBody", new Color(0.96f, 0.9f, 0.7f)) },
            };

        // The cut onion's colour, from its entry above (a static field here would be read before it's set).
        private static Color DiceColor => Parts["DicedOnion"][0].Color;
        private static readonly Color CutFaceColor = new Color(0.98f, 0.94f, 0.8f);
        private static readonly Color CutRingsColor = new Color(0.86f, 0.76f, 0.55f);

        // Where the halves lie on the board while cutting (m, from where the item sits): to one
        // side, their length across the board, leaving the middle for the dice heap.
        private const float HalfScale = 0.65f;
        private static readonly Vector3[] HalfPlaces = { new Vector3(-0.1f, 0f, 0.105f), new Vector3(-0.1f, 0f, -0.105f) };

        /// <summary>
        /// Adds an onion's cutting progress to the board: halves, the heap's dice one by one, and bits
        /// that fly with each chop (<see cref="Game.Presentation.CutProgressView"/>). Placed where the
        /// item sits on the board. Nothing without the models.
        /// </summary>
        public static void DressCutProgress(Game.Gameplay.Stations.PrepStation prep, Transform boardAnchor)
        {
            const string model = "SM_OnionCut";
            if (PilotArt.FindMesh(model, "OnionHalfSkin") == null)
                return;
            var root = new GameObject("CutProgress").transform;
            root.SetParent(prep.transform, false);
            root.localPosition = boardAnchor.localPosition;
            root.localRotation = boardAnchor.localRotation;

            var halves = new GameObject[HalfPlaces.Length];
            for (var i = 0; i < halves.Length; i++)
            {
                var half = Part(root, $"Half_{i + 1}", model, "OnionHalfSkin", StyleTest.EnsureToonMaterial("OnionBody", Parts["Onion"][0].Color));
                half.transform.localPosition = HalfPlaces[i];
                half.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // its length across the board
                half.transform.localScale = Vector3.one * HalfScale;
                Part(half.transform, "Face", model, "OnionHalfFace", StyleTest.EnsureToonMaterial("OnionCutFace", CutFaceColor));
                Part(half.transform, "Rings", model, "OnionHalfRings", StyleTest.EnsureToonMaterial("OnionCutRings", CutRingsColor));
                halves[i] = half;
            }

            var pieces = new System.Collections.Generic.List<GameObject>();
            for (var i = 0; PilotArt.FindMesh(model, $"OnionDie_{i:00}") != null; i++)
                pieces.Add(Part(root, $"Die_{i:00}", model, $"OnionDie_{i:00}", StyleTest.EnsureToonMaterial("DicedOnionBody", DiceColor)));
            foreach (var go in halves)
                go.SetActive(false);
            foreach (var go in pieces)
                go.SetActive(false);

            // Onion bits flying off the knife: a little burst of cream crumbs that fall back down.
            var bits = GreyboxKitchenware.CreateParticles(root, "Bits", 0.04f, 0.05f, lifetime: 0.45f, speed: 1.3f,
                minSize: 0.018f, maxSize: 0.032f, growth: 1f, maxParticles: 60);
            var main = bits.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 3f;
            main.startColor = new Color(0.97f, 0.92f, 0.75f, 1f);
            var cone = bits.shape;
            cone.angle = 40f;
            var stay = bits.colorOverLifetime;
            var visible = new Gradient();
            visible.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            stay.color = visible;

            var view = new UnityEditor.SerializedObject(root.gameObject.AddComponent<Game.Presentation.CutProgressView>());
            view.FindProperty("_station").objectReferenceValue = prep;
            view.FindProperty("_boardAnchor").objectReferenceValue = boardAnchor;
            SetArray(view.FindProperty("_halves"), halves);
            SetArray(view.FindProperty("_pieces"), pieces);
            view.FindProperty("_bits").objectReferenceValue = bits;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(UnityEditor.SerializedProperty list, System.Collections.Generic.IList<GameObject> items)
        {
            list.arraySize = items.Count;
            for (var i = 0; i < items.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static GameObject Part(Transform parent, string name, string model, string mesh, Material material)
        {
            var go = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent, material, keepCollider: false);
            if (PilotArt.TryUseMesh(go, model, mesh))
                return go;
            Object.DestroyImmediate(go);
            return null;
        }

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
