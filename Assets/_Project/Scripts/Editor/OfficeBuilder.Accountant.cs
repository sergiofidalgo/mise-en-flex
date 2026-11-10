using Game.Data;
using Game.Gameplay.Debt;
using Game.Gameplay.Office;
using Game.Presentation.Hud;
using Game.Presentation.Office;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The boss and money trouble (Phase 6, milestone 4), kept in its own file so it merges apart
    /// from the rest of the office: borrowing at the accountant's desk (the user's call,
    /// 2026-11-10), the hall of fame's portraits of erased chefs, and the boss and his bodyguards
    /// as prefabs for their morning visits to the kitchen.
    /// </summary>
    public static partial class OfficeBuilder
    {
        private const string BossVisitorPrefabPath = "Assets/_Project/Prefabs/PF_BossVisitor.prefab";
        private const string BodyguardPrefabPath = "Assets/_Project/Prefabs/PF_Bodyguard.prefab";

        // The desk faces the visitors' chairs (south, toward the camera): a borrower stands there.
        private static void AddAccountantDesk(Transform rooms)
        {
            var room = rooms.Find("Accountant");
            var desk = room != null ? room.Find("Desk") : null;
            if (desk == null)
                return;
            var books = new GameObject("AccountantDesk").transform;
            books.SetParent(room, false);
            books.position = desk.position;
            books.rotation = Quaternion.Euler(0f, 180f, 0f); // local forward points at the visitors' chairs
            var deskSo = new SerializedObject(books.gameObject.AddComponent<AccountantDesk>());
            deskSo.FindProperty("_staffConfig").objectReferenceValue = FindAsset<StaffConfig>();
            deskSo.FindProperty("_visitorSide").vector3Value = new Vector3(0f, 0f, 1.5f);
            deskSo.ApplyModifiedPropertiesWithoutUndo();

            var hud = new SerializedObject(books.gameObject.AddComponent<AccountantHud>());
            hud.FindProperty("_style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HudStyle>(SettingsFolder + "/HudStyle.asset");
            hud.FindProperty("_desk").objectReferenceValue = books.GetComponent<AccountantDesk>();
            hud.ApplyModifiedPropertiesWithoutUndo();
        }

        // The hall of fame's portraits are erased chefs' (§15.3): each shows only once a campaign has
        // been erased, so the wall fills as players lose. Four slots: the three on the east wall
        // and one more beside them.
        private static void AddHallPortraits(Transform rooms)
        {
            var room = rooms.Find("HallOfFame");
            if (room == null)
                return;
            HangPicture(room, new Vector3(17.5f, PictureHeight, 2.4f), Vector3.left, new Color(0.44f, 0.42f, 0.4f));
            var slots = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in room)
                if (child.name.StartsWith("Picture_"))
                    slots.Add(child.gameObject);
            slots.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
            var portraits = new SerializedObject(room.gameObject.AddComponent<HallOfFamePortraits>());
            var list = portraits.FindProperty("_slots");
            list.arraySize = slots.Count;
            for (var i = 0; i < slots.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            portraits.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The boss and a bodyguard as walking prefabs for the kitchen (BossVisitDirector), dressed
        /// as they are in the office. No colliders: they walk the free floor and step round nobody.
        /// </summary>
        public static (VisitorAgent Boss, VisitorAgent Guard) BuildVisitorPrefabs()
        {
            GreyboxAssets.EnsureFolder(System.IO.Path.GetDirectoryName(BossVisitorPrefabPath));
            var holder = new GameObject("Visitors").transform;
            Boss(holder, Vector3.zero);
            Bodyguard(holder, "Bodyguard_L", Vector3.zero);
            var boss = SaveVisitor(holder.Find("Boss"), BossVisitorPrefabPath);
            var guard = SaveVisitor(holder.Find("Bodyguard_L"), BodyguardPrefabPath);
            Object.DestroyImmediate(holder.gameObject);
            return (boss, guard);
        }

        private static VisitorAgent SaveVisitor(Transform person, string path)
        {
            person.SetParent(null, true);
            person.localPosition = Vector3.zero;
            person.localRotation = Quaternion.identity;
            foreach (var collider in person.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);
            person.gameObject.AddComponent<VisitorAgent>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(person.gameObject, path);
            Object.DestroyImmediate(person.gameObject);
            return prefab.GetComponent<VisitorAgent>();
        }

        private static T FindAsset<T>() where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
                return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            return null;
        }
    }
}
