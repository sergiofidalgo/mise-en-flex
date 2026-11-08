using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The dining room's furniture models (decision 2026-11-08, the art overhaul): wooden tables, a
    /// panelled bar, chairs that face their table, bar stools, and framed pictures for the walls.
    /// Without the model, everything keeps its greybox shape.
    /// </summary>
    public static class DiningArt
    {
        private const string Model = "SM_Dining";

        private static readonly Color TableWood = new Color(0.6f, 0.4f, 0.24f);
        private static readonly Color TableGrain = new Color(0.52f, 0.34f, 0.2f);
        private static readonly Color BarWood = new Color(0.5f, 0.32f, 0.19f);
        private static readonly Color BarGrain = new Color(0.43f, 0.27f, 0.16f);
        private static readonly Color BarTopWood = new Color(0.72f, 0.52f, 0.32f);
        private static readonly Color BarTopGrain = new Color(0.64f, 0.45f, 0.27f);
        private static readonly Color ChairWood = new Color(0.44f, 0.28f, 0.17f);
        private static readonly Color FrameWood = new Color(0.36f, 0.22f, 0.13f);

        /// <summary>
        /// A table's or the bar's body as its model; returns the bar's top, for the station's
        /// highlight, or null.
        /// </summary>
        public static Renderer DressTable(Transform table, bool bar)
        {
            var body = table.Find("Body");
            if (body == null || !PilotArt.TryUseMesh(body.gameObject, Model, bar ? "BarBody" : "TableBody"))
                return null;

            body.GetComponent<Renderer>().sharedMaterial = bar
                ? StyleTest.EnsureToonMaterial("BarWood", BarWood, BarGrain, grainScale: 14f)
                : StyleTest.EnsureToonMaterial("TableWood", TableWood, TableGrain, grainScale: 14f);
            if (!bar)
                return null;

            var top = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, "Top", body,
                StyleTest.EnsureToonMaterial("BarTop", BarTopWood, BarTopGrain, grainScale: 18f), keepCollider: false);
            if (PilotArt.TryUseMesh(top, Model, "BarTop"))
                return top.GetComponent<Renderer>();
            Object.DestroyImmediate(top);
            return null;
        }

        /// <summary>
        /// A seat's chair as a chair with a back facing its table (the table lies the opposite way
        /// from <paramref name="fromTable"/>), or a round stool at the bar.
        /// </summary>
        public static void DressChair(GameObject chair, bool stool, Vector3 fromTable)
        {
            if (!PilotArt.TryUseMesh(chair, Model, stool ? "Stool" : "DiningChair"))
                return;
            chair.GetComponent<Renderer>().sharedMaterial = StyleTest.EnsureToonMaterial("ChairWood", ChairWood, TableGrain, grainScale: 20f);
            if (!stool)
                chair.transform.localRotation = Quaternion.LookRotation(-fromTable);
        }

        /// <summary>A framed picture hung on a wall, facing <paramref name="facing"/>, with its back at <paramref name="position"/>.</summary>
        public static void HangPicture(Transform parent, string name, Vector3 position, Vector3 facing, Color canvas)
        {
            var frame = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent,
                StyleTest.EnsureToonMaterial("PictureFrame", FrameWood), keepCollider: false);
            if (!PilotArt.TryUseMesh(frame, Model, "PictureFrame"))
            {
                Object.DestroyImmediate(frame);
                return;
            }
            // The model faces -z in Unity (+Y in Blender, towards the camera).
            frame.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-facing));

            var picture = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, "Canvas", frame.transform,
                StyleTest.EnsureToonMaterial($"Canvas_{name}", canvas), keepCollider: false);
            if (!PilotArt.TryUseMesh(picture, Model, "PictureCanvas"))
                Object.DestroyImmediate(picture);
        }
    }
}
