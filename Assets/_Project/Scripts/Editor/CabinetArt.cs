using System.Collections.Generic;
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
        // A pale, quiet wood with fine, faint grain, so plates, food and the warmer bamboo cutting
        // board stand out on it (decision 2026-11-08).
        private static readonly Color Top = new Color(0.87f, 0.77f, 0.6f);
        private static readonly Color TopGrain = new Color(0.82f, 0.7f, 0.52f);
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
                Add("Top", "CabinetTop", StyleTest.EnsureToonMaterial("CabinetTop", Top, TopGrain, grainScale: 24f));
            Add("Fronts", "CabinetFronts", StyleTest.EnsureToonMaterial("CabinetFronts", Fronts, FrontsGrain, grainScale: 14f));
            Add("Knobs", "CabinetKnobs", StyleTest.EnsureToonMaterial("CabinetKnobs", Brass));
            Add("Base", "CabinetKick", StyleTest.EnsureToonMaterial("CabinetBase", Base));
            return parts;

            void Add(string name, string mesh, Material material)
            {
                var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, body, material, keepCollider: false);
                if (PilotArt.TryUseMesh(part, Model, mesh))
                    parts.Add(part.GetComponent<Renderer>());
                else
                    Object.DestroyImmediate(part);
            }
        }
    }
}
