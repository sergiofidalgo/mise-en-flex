using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Steel sheet trays and gastronorm containers (the user's references, 2026-11-08): the tray and
    /// container items, a steel rack holding trays on runners, and a stack of nested containers.
    /// Without the models, callers keep their greybox shapes.
    /// </summary>
    public static class PanArt
    {
        private const string TrayModel = "SM_Tray";
        private const string ContainerModel = "SM_Container";
        private const string RackModel = "SM_TrayRack";

        // Must match build_tray/build_container/build_tray_rack in blender/build_pilot_models.py.
        /// <summary>Height of a pan's inside floor above its base: where food sits.</summary>
        public const float PanFloor = 0.006f;
        /// <summary>
        /// Where portions start in a container: halfway up its deep pan, as if part filled, so they
        /// show from the overhead camera rather than hiding at the bottom.
        /// </summary>
        public const float ContainerFoodLevel = 0.16f;
        private static readonly float[] TrayRackLevels = { 0.04f, 0.14f, 0.24f, 0.34f };
        // How far each container sits above the one it's nested in: its rim shows that much.
        private const float NestStep = 0.042f;
        // The idle stack shows bigger, using more of its counter (the user's note, 2026-11-08).
        private const float StackScale = 1.3f;
        // The stack isn't tidy (the user's note, 2026-11-08): each pan is nudged and turned a little,
        // as if dropped back on the pile, rather than shaped exactly into the one below.
        private const float StackNudge = 0.022f;
        private const float StackTurn = 7f;

        private static readonly Color PanSteel = new Color(0.8f, 0.82f, 0.86f);
        private static readonly Color RackSteel = new Color(0.58f, 0.6f, 0.64f);

        public static bool HasModels => PilotArt.FindMesh(TrayModel, "TrayBody") != null;

        private static Material Steel => StyleTest.EnsureToonMaterial("PanSteel", PanSteel);

        /// <summary>Makes a tray item's body the sheet tray. False without the model.</summary>
        public static bool DressTray(GameObject body) => Dress(body, TrayModel, "TrayBody");

        /// <summary>Makes a container item's body the gastronorm pan. False without the model.</summary>
        public static bool DressContainer(GameObject body) => Dress(body, ContainerModel, "ContainerBody");

        private static bool Dress(GameObject body, string model, string mesh)
        {
            if (!PilotArt.TryUseMesh(body, model, mesh))
                return false;
            body.GetComponent<Renderer>().sharedMaterial = Steel;
            return true;
        }

        /// <summary>
        /// The tray rack on a station at <paramref name="baseHeight"/>: its frame, and one tray per
        /// shelf, bottom first (the pile view takes the top one first). Empty without the models.
        /// </summary>
        public static GameObject[] TrayRack(Transform station, float baseHeight)
        {
            if (!HasModels)
                return System.Array.Empty<GameObject>();
            var frame = Part(station, "TrayRackFrame", RackModel, "TrayRackFrame", StyleTest.EnsureToonMaterial("TrayRackSteel", RackSteel));
            frame.transform.localPosition = Vector3.up * baseHeight;
            var trays = new List<GameObject>();
            for (var i = 0; i < TrayRackLevels.Length; i++)
            {
                var tray = Part(station, $"RackTray_{i + 1}", TrayModel, "TrayBody", Steel);
                tray.transform.localPosition = Vector3.up * (baseHeight + TrayRackLevels[i]);
                trays.Add(tray);
            }
            return trays.ToArray();
        }

        /// <summary>
        /// A stack of <paramref name="count"/> nested containers on a station, bottom first, each
        /// sitting loosely in the one below, nudged and turned, with its rim showing. Empty without
        /// the models.
        /// </summary>
        public static GameObject[] ContainerStack(Transform station, float baseHeight, int count)
        {
            if (!HasModels)
                return System.Array.Empty<GameObject>();
            var pans = new GameObject[count];
            for (var i = 0; i < count; i++)
            {
                pans[i] = Part(station, $"StackContainer_{i + 1}", ContainerModel, "ContainerBody", Steel);
                // The bottom one sits square; the others drift a little, the same way every build.
                var drift = i == 0 ? Vector2.zero : new Vector2(Wobble(i, 1), Wobble(i, 2)) * StackNudge;
                pans[i].transform.localPosition = new Vector3(drift.x, baseHeight / StackScale + i * NestStep, drift.y) * StackScale;
                pans[i].transform.localScale = Vector3.one * StackScale;
                pans[i].transform.localRotation = Quaternion.Euler(0f, i == 0 ? 0f : Wobble(i, 3) * StackTurn, 0f);
            }
            return pans;
        }

        // A fixed number in [-1, 1] for each pan and salt: a messy pile that's the same every build.
        private static float Wobble(int index, int salt) => Mathf.Sin(index * 12.9898f + salt * 78.233f) * 0.5f
            + Mathf.Sin(index * 4.1414f + salt * 2.7183f) * 0.5f;

        private static GameObject Part(Transform parent, string name, string model, string mesh, Material material)
        {
            var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent, material, keepCollider: false);
            if (PilotArt.TryUseMesh(part, model, mesh))
                return part;
            Object.DestroyImmediate(part);
            return null;
        }
    }
}
