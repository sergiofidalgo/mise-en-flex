using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The wooden cabinet every plain counter shares (decision 2026-11-08, the art overhaul): a
    /// light wood top over a darker wooden body with a drawer and two doors on its working sides,
    /// brass knobs and a recessed base. Without the model, the station keeps its greybox block.
    /// </summary>
    public static class CabinetArt
    {
        private const string Model = "SM_Cabinet";

        private static readonly Color Body = new Color(0.56f, 0.37f, 0.22f);
        private static readonly Color BodyGrain = new Color(0.51f, 0.33f, 0.19f);
        private static readonly Color Fronts = new Color(0.65f, 0.44f, 0.27f);
        private static readonly Color FrontsGrain = new Color(0.59f, 0.39f, 0.23f);
        // A plain, pale top with no texture (the user's note, 2026-11-08), so plates, food and the
        // bamboo cutting board stand out on it.
        private static readonly Color Top = new Color(0.86f, 0.81f, 0.73f);
        private static readonly Color Base = new Color(0.24f, 0.16f, 0.11f);
        private static readonly Color Brass = new Color(0.8f, 0.62f, 0.3f);

        /// <summary>
        /// Gives a station's body the cabinet model and adds its other parts under it, so they
        /// shake with it. Under a sink, the body stops lower and there's no wooden top: the sink's
        /// own top and basin sit there. Returns the renderers added, for the station's highlight;
        /// empty without the model.
        /// </summary>
        public static IReadOnlyList<Renderer> Dress(Transform station, bool underSink = false)
        {
            var parts = new List<Renderer>();
            var body = station.Find("Body");
            if (body == null || !PilotArt.TryUseMesh(body.gameObject, Model, underSink ? "CabinetBodyLow" : "CabinetBody"))
                return parts;

            body.GetComponent<Renderer>().sharedMaterial = StyleTest.EnsureToonMaterial("CabinetBody", Body, BodyGrain, grainScale: 14f);
            if (!underSink)
                JoinTop(station, Add("Top", "CabinetTop_0", StyleTest.EnsureToonMaterial("CabinetTop", Top)));
            Add("Fronts", "CabinetFronts", StyleTest.EnsureToonMaterial("CabinetFronts", Fronts, FrontsGrain, grainScale: 14f));
            Add("Knobs", "CabinetKnobs", StyleTest.EnsureToonMaterial("CabinetKnobs", Brass));
            Add("Base", "CabinetKick", StyleTest.EnsureToonMaterial("CabinetBase", Base));
            return parts;

            Renderer Add(string name, string mesh, Material material)
            {
                var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, body, material, keepCollider: false);
                if (!PilotArt.TryUseMesh(part, Model, mesh))
                {
                    Object.DestroyImmediate(part);
                    return null;
                }
                var renderer = part.GetComponent<Renderer>();
                parts.Add(renderer);
                return renderer;
            }
        }

        // The top joins the counters beside it into one surface (the user's note, 2026-11-08):
        // a version of it for each set of joined sides, swapped by CounterJoin.
        private static void JoinTop(Transform station, Renderer top)
        {
            if (top == null)
                return;
            var join = new SerializedObject(station.gameObject.AddComponent<Game.Gameplay.Stations.CounterJoin>());
            join.FindProperty("_top").objectReferenceValue = top.GetComponent<MeshFilter>();
            var tops = join.FindProperty("_tops");
            tops.arraySize = 16;
            for (var joined = 0; joined < 16; joined++)
                tops.GetArrayElementAtIndex(joined).objectReferenceValue = PilotArt.FindMesh(Model, $"CabinetTop_{joined}");
            join.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Joins the tops of every counter in the open scene, as play would, so the scene is saved joined.</summary>
        public static void JoinAll()
        {
            var counters = Object.FindObjectsByType<Game.Gameplay.Stations.CounterJoin>(FindObjectsSortMode.None);
            foreach (var counter in counters)
            {
                var others = new List<Game.Simulation.Movement.Footprint>();
                foreach (var other in counters)
                {
                    if (other != counter)
                        others.Add(other.Footprint);
                }
                counter.Show(Game.Simulation.Movement.CounterJoins.Joined(counter.Footprint, 0.5f, others));
            }
        }
    }
}
