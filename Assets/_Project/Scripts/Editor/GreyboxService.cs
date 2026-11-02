using Game.Gameplay;
using Game.Gameplay.Customers;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds counter service (decision 2026-11-02): a stool in front of each pass counter, a door
    /// at the east end of the dining strip, queue spots by the door, the greybox customer prefab
    /// and the customer director that runs them.
    /// </summary>
    public static class GreyboxService
    {
        private const string CustomerPrefabPath = "Assets/_Project/Prefabs/PF_Customer.prefab";

        // Dining strip runs along the south wall in front of the pass (pass front edge at z = -2.9).
        private const float SeatZ = -3.35f;
        private static readonly Vector3 DoorPosition = new Vector3(4.55f, 0f, -3.4f);
        private static readonly float[] QueueX = { 4.0f, 3.4f, 2.8f };

        private static readonly Vector3 StoolScale = new Vector3(0.36f, 0.22f, 0.36f); // cylinder: 0.44 m tall
        private static readonly Color StoolColor = new Color(0.35f, 0.25f, 0.18f);
        private static readonly Vector3 DoorScale = new Vector3(0.06f, 1.2f, 0.9f);
        private static readonly Color DoorColor = new Color(0.20f, 0.14f, 0.10f);

        // Customers: slightly shorter and slimmer than chefs, a muted colour so they read apart.
        private static readonly Vector3 CustomerScale = new Vector3(0.6f, 0.8f, 0.6f);
        private static readonly Color CustomerColor = new Color(0.55f, 0.60f, 0.70f);

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

            var door = new GameObject("Door").transform;
            door.SetParent(zone, false);
            door.localPosition = DoorPosition;
            door.localRotation = Quaternion.Euler(0f, -90f, 0f); // customers walk in facing west
            var doorPanel = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "DoorPanel", zone, GreyboxAssets.EnsureMaterial("Door", DoorColor), keepCollider: false);
            doorPanel.transform.localScale = DoorScale;
            doorPanel.transform.localPosition = new Vector3(4.9f - DoorScale.x / 2f, DoorScale.y / 2f, DoorPosition.z);

            var queue = new Transform[QueueX.Length];
            for (var i = 0; i < QueueX.Length; i++)
            {
                queue[i] = new GameObject($"QueueSpot_{i + 1}").transform;
                queue[i].SetParent(zone, false);
                queue[i].localPosition = new Vector3(QueueX[i], 0f, -3.55f);
            }

            var director = zone.gameObject.AddComponent<CustomerDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("_root").objectReferenceValue = root;
            so.FindProperty("_customerPrefab").objectReferenceValue = BuildCustomerPrefab().GetComponent<CustomerAgent>();
            so.FindProperty("_door").objectReferenceValue = door;
            SetArray(so.FindProperty("_seats"), seats);
            SetArray(so.FindProperty("_queueSpots"), queue);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildCustomerPrefab()
        {
            var root = new GameObject("PF_Customer");
            root.AddComponent<CustomerAgent>();
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
