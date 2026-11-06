using Game.Gameplay;
using Game.Gameplay.Customers;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds counter service (decision 2026-11-02): a stool in front of each pass counter, a street
    /// door into the dining room, queue spots by the door, the greybox customer prefab
    /// and the customer director that runs them.
    /// </summary>
    public static class GreyboxService
    {
        private const string CustomerPrefabPath = "Assets/_Project/Prefabs/PF_Customer.prefab";

        // Stools stand in front of the pass (pass front edge at z = -2.9).
        private const float SeatZ = -3.35f;
        // The street door is in the south wall; customers wait along the east wall until a seat's free.
        private static readonly Vector3 DoorPosition = new Vector3(2f, 0f, -6.55f);
        private const float DoorPanelBehind = -0.32f;
        private const float DoorMatAhead = 0.1f;

        // Furniture stays this far off the doorway, so people have somewhere to step in.
        private static readonly Vector2 DoorClearance = new Vector2(0.6f, 0.6f);

        // Relative to the door, whichever wall it's on.
        private static readonly Vector3[] QueueOffsets = { new Vector3(1.2f, 0f, 0.35f), new Vector3(1.2f, 0f, 1.15f), new Vector3(1.2f, 0f, 1.95f), new Vector3(2f, 0f, 0.35f), new Vector3(2f, 0f, 1.15f), new Vector3(2f, 0f, 1.95f) };

        private static readonly Vector3 StoolScale = new Vector3(0.36f, 0.22f, 0.36f); // cylinder: 0.44 m tall
        private static readonly Color StoolColor = new Color(0.35f, 0.25f, 0.18f);
        private static readonly Vector3 DoorScale = new Vector3(0.9f, 1.2f, 0.06f);
        private static readonly Color DoorColor = new Color(0.20f, 0.14f, 0.10f);
        private static readonly Vector3 DoorMatScale = new Vector3(1.2f, 0.012f, 0.7f);
        private static readonly Color DoorMatColor = new Color(0.25f, 0.42f, 0.34f);

        // Customers: slightly shorter and slimmer than chefs, a muted colour so they read apart.
        private static readonly Vector3 CustomerScale = new Vector3(0.6f, 0.8f, 0.6f);
        private static readonly Color CustomerColor = new Color(0.55f, 0.60f, 0.70f);
        private const float CustomerBodyRadius = 0.3f;

        /// <param name="passPositions">Pass counter positions in seat order.</param>
        public static void Build(Vector3[] passPositions, KitchenCompositionRoot root)
        {
            var zone = new GameObject("Zone_Dining").transform;

            var seats = new Transform[passPositions.Length];
            var stoolMaterial = GreyboxAssets.EnsureMaterial("Stool", StoolColor);
            for (var i = 0; i < passPositions.Length; i++)
            {
                seats[i] = new GameObject($"Seat_{i + 1}").transform;
                seats[i].SetParent(zone, false);
                seats[i].localPosition = new Vector3(passPositions[i].x, 0f, SeatZ);

                var stool = GreyboxAssets.CreatePrimitive(PrimitiveType.Cylinder, "Stool", seats[i], stoolMaterial, keepCollider: false);
                stool.transform.localScale = StoolScale;
                stool.transform.localPosition = new Vector3(0f, StoolScale.y, 0f);
            }

            // The street door: its root is the doorway people step through, facing into the room.
            var door = new GameObject("Street door").transform;
            door.SetParent(zone, false);
            door.localPosition = DoorPosition;

            // The wall hides the door from the camera, so a mat on the floor shows where it is.
            var panel = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, "DoorPanel", door, GreyboxAssets.EnsureMaterial("Door", DoorColor), keepCollider: false);
            panel.transform.localScale = DoorScale;
            panel.transform.localPosition = new Vector3(0f, DoorScale.y / 2f, DoorPanelBehind);
            var mat = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, "DoorMat", door, GreyboxAssets.EnsureMaterial("DoorMat", DoorMatColor), keepCollider: false);
            mat.transform.localScale = DoorMatScale;
            mat.transform.localPosition = new Vector3(0f, DoorMatScale.y / 2f, DoorMatAhead);

            var streetDoor = new SerializedObject(door.gameObject.AddComponent<Game.Gameplay.Stations.StreetDoor>());
            streetDoor.FindProperty("_halfSize").vector2Value = DoorClearance;
            streetDoor.FindProperty("_itemAnchor").objectReferenceValue = door;
            streetDoor.ApplyModifiedPropertiesWithoutUndo();

            var doorLight = new SerializedObject(door.gameObject.AddComponent<Game.Presentation.InteractableHighlightView>());
            doorLight.FindProperty("_interactable").objectReferenceValue = door.GetComponent<Game.Gameplay.Stations.StreetDoor>();
            doorLight.FindProperty("_shakeTarget").objectReferenceValue = panel.transform;
            var doorParts = doorLight.FindProperty("_renderers");
            doorParts.arraySize = 2;
            doorParts.GetArrayElementAtIndex(0).objectReferenceValue = panel.GetComponent<Renderer>();
            doorParts.GetArrayElementAtIndex(1).objectReferenceValue = mat.GetComponent<Renderer>();
            doorLight.ApplyModifiedPropertiesWithoutUndo();

            var queue = new Transform[QueueOffsets.Length];
            for (var i = 0; i < QueueOffsets.Length; i++)
            {
                queue[i] = new GameObject($"QueueSpot_{i + 1}").transform;
                queue[i].SetParent(door, false);
                queue[i].localPosition = QueueOffsets[i];
            }

            var director = zone.gameObject.AddComponent<CustomerDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("_root").objectReferenceValue = root;
            so.FindProperty("_customerPrefab").objectReferenceValue = BuildCustomerPrefab().GetComponent<CustomerAgent>();
            so.FindProperty("_door").objectReferenceValue = door;
            SetArray(so.FindProperty("_queueSpots"), queue);
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var pass in Object.FindObjectsByType<Game.Gameplay.Stations.PassStation>(FindObjectsSortMode.None))
            {
                var passSo = new SerializedObject(pass);
                passSo.FindProperty("_seat").objectReferenceValue = seats[pass.SeatIndex];
                passSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static GameObject BuildCustomerPrefab()
        {
            var root = new GameObject("PF_Customer");
            var agent = new SerializedObject(root.AddComponent<CustomerAgent>());
            agent.FindProperty("_bodyRadius").floatValue = CustomerBodyRadius;
            agent.ApplyModifiedPropertiesWithoutUndo();
            var body = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Capsule, "Body", root.transform, GreyboxAssets.EnsureMaterial("Customer", CustomerColor), keepCollider: false);
            body.transform.localScale = CustomerScale;
            body.transform.localPosition = new Vector3(0f, CustomerScale.y, 0f); // capsule is 2 units tall
            var nose = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "Nose", root.transform, GreyboxAssets.EnsureMaterial("Nose", new Color(0.2f, 0.2f, 0.22f)), keepCollider: false);
            nose.transform.localScale = new Vector3(0.16f, 0.16f, 0.24f);
            nose.transform.localPosition = new Vector3(0f, CustomerScale.y * 1.45f, CustomerScale.z * 0.5f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CustomerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SetArray(SerializedProperty property, Transform[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
