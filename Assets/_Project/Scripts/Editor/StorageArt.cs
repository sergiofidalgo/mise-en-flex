using Game.Gameplay.Stations;
using Game.Presentation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Dresses a storage unit built by <see cref="GreyboxStorage"/> with its model, when the model
    /// exists: a fridge gets its door, cold mist and dark interior; a shelf gets goods on its lower
    /// tiers. Without the models it stays greybox.
    /// </summary>
    public static class StorageArt
    {
        public const string FridgeModel = "SM_Fridge";
        public const string ShelfModel = "SM_Shelf";

        // Where the door's hinge is; must match FRIDGE_HINGE in blender/build_pilot_models.py.
        private static readonly Vector3 DoorHinge = new Vector3(-0.42f, 0.1f, -0.435f);
        private static readonly Color InteriorColor = new Color(0.16f, 0.22f, 0.27f);
        private static readonly Color GoodsColor = new Color(0.66f, 0.55f, 0.38f);

        // Cold air: a soft, slow puff that rolls out of the door and sinks.
        private static readonly Color MistColor = new Color(0.86f, 0.94f, 1f, 0.55f);
        private static readonly Vector3 MistPosition = new Vector3(0f, 0.55f, -0.5f);
        private const float MistLifetime = 1.2f;
        private const float MistSpeed = 0.3f;
        private const float MistRadius = 0.25f;
        private const float MistGravity = 0.04f;

        public static string BinMesh(bool refrigerated) => refrigerated ? "FridgePan" : "ShelfBasket";

        public static string ModelFor(bool refrigerated) => refrigerated ? FridgeModel : ShelfModel;

        public static void Dress(StorageStation station, bool refrigerated)
        {
            var body = station.transform.Find("Body").gameObject;
            if (!PilotArt.TryUseMesh(body, ModelFor(refrigerated), refrigerated ? "FridgeBody" : "ShelfBody"))
                return;

            if (refrigerated)
                DressFridge(station, body.GetComponent<Renderer>().sharedMaterial);
            else
                CreatePart(station.transform, "Goods", ShelfModel, "ShelfGoods",
                    GreyboxAssets.EnsureMaterial("ShelfGoods", GoodsColor), Vector3.zero);
        }

        private static void DressFridge(StorageStation station, Material bodyMaterial)
        {
            CreatePart(station.transform, "Interior", FridgeModel, "FridgeInterior",
                GreyboxAssets.EnsureMaterial("FridgeInterior", InteriorColor), Vector3.zero);
            var door = CreatePart(station.transform, "Door", FridgeModel, "FridgeDoor", bodyMaterial, DoorHinge);

            // The door lights up with the body when the fridge is targeted.
            var highlight = new SerializedObject(station.GetComponent<InteractableHighlightView>());
            var renderers = highlight.FindProperty("_renderers");
            renderers.arraySize++;
            renderers.GetArrayElementAtIndex(renderers.arraySize - 1).objectReferenceValue = door.GetComponent<Renderer>();
            highlight.ApplyModifiedPropertiesWithoutUndo();

            var mist = GreyboxKitchenware.CreateParticles(station.transform, "ColdMist", MistPosition.y, MistRadius,
                MistLifetime, MistSpeed, minSize: 0.12f, maxSize: 0.22f, growth: 2.2f, maxParticles: 40);
            mist.transform.localPosition = MistPosition;
            mist.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.3f, -1f)); // out of the door
            var main = mist.main;
            main.startColor = MistColor;
            main.gravityModifier = MistGravity; // cold air sinks

            var view = new SerializedObject(station.gameObject.AddComponent<FridgeDoorView>());
            view.FindProperty("_door").objectReferenceValue = door.transform;
            view.FindProperty("_mist").objectReferenceValue = mist;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePart(Transform parent, string name, string model, string mesh, Material material, Vector3 position)
        {
            var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent, material, keepCollider: false);
            PilotArt.TryUseMesh(part, model, mesh);
            part.transform.localPosition = position;
            return part;
        }
    }
}
