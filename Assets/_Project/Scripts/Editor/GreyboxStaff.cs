using Game.Gameplay;
using Game.Gameplay.Staff;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Builds what staff need: the greybox staff prefab and the director that runs them.</summary>
    public static class GreyboxStaff
    {
        private const string AgentPrefabPath = "Assets/_Project/Prefabs/PF_Staff.prefab";

        private static readonly Vector3 SpawnPosition = new Vector3(2.8f, 0f, -0.9f);
        private static readonly Vector3 BodyScale = new Vector3(0.62f, 0.82f, 0.62f);
        private static readonly Color BodyColor = new Color(0.30f, 0.62f, 0.55f);
        private static readonly Vector3 CarryPosition = new Vector3(0f, 1.0f, 0.42f);

        public static void Build(KitchenCompositionRoot root)
        {
            var zone = new GameObject("Zone_Staff");
            var spawn = new GameObject("StaffSpawn").transform;
            spawn.SetParent(zone.transform, false);
            spawn.localPosition = SpawnPosition;

            var director = new SerializedObject(zone.AddComponent<StaffDirector>());
            director.FindProperty("_root").objectReferenceValue = root;
            director.FindProperty("_agentPrefab").objectReferenceValue = BuildPrefab().GetComponent<StaffAgent>();
            director.FindProperty("_spawnPoint").objectReferenceValue = spawn;
            director.ApplyModifiedPropertiesWithoutUndo();

            var menu = new SerializedObject(zone.AddComponent<HiringMenu>());
            menu.FindProperty("_root").objectReferenceValue = root;
            menu.FindProperty("_dayLoop").objectReferenceValue = root.GetComponent<DayLoop>();
            menu.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildPrefab()
        {
            var root = new GameObject("PF_Staff");
            var agent = root.AddComponent<StaffAgent>();
            var body = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Capsule, "Body", root.transform, GreyboxAssets.EnsureMaterial("Staff", BodyColor), keepCollider: false);
            body.transform.localScale = BodyScale;
            body.transform.localPosition = new Vector3(0f, BodyScale.y, 0f); // capsule is 2 units tall
            var nose = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "Nose", root.transform, GreyboxAssets.EnsureMaterial("Nose", new Color(0.2f, 0.2f, 0.22f)), keepCollider: false);
            nose.transform.localScale = new Vector3(0.16f, 0.16f, 0.24f);
            nose.transform.localPosition = new Vector3(0f, BodyScale.y * 1.45f, BodyScale.z * 0.5f);

            var anchor = new GameObject("CarryAnchor").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = CarryPosition;
            var so = new SerializedObject(agent);
            so.FindProperty("_carryAnchor").objectReferenceValue = anchor;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, AgentPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
