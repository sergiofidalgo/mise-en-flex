using Game.Presentation.Office;
using Game.Presentation.Office.Ambience;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The bodyguards' watch (the user's notes, 2026-11-09; see BodyguardWatch): each guard is put
    /// on a turning frame of his own, so his idle glance (an Oscillate on the person) and the stare
    /// don't fight over one transform, and one watcher turns both. They watch the guards' room and
    /// the rooms off it: the test kitchen and the hall of fame (the middle row of the floor).
    /// </summary>
    public static partial class OfficeBuilder
    {
        private static void AddGuards(Transform rooms)
        {
            var room = rooms.Find("GuardedDoors");
            if (room == null)
                return;
            var guards = new Transform[2];
            var idles = new Oscillate[2];
            foreach (var (name, index) in new[] { ("Bodyguard_L", 0), ("Bodyguard_R", 1) })
            {
                var person = room.Find(name);
                if (person == null)
                    continue;
                var frame = new GameObject(name + "_Watch").transform;
                frame.SetParent(room, false);
                frame.SetPositionAndRotation(person.position, person.rotation);
                person.SetParent(frame, true);
                guards[index] = frame;
                idles[index] = person.GetComponent<Oscillate>();
            }

            var watcher = new GameObject("BodyguardWatch").AddComponent<BodyguardWatch>();
            watcher.transform.SetParent(room, false);
            var watch = new SerializedObject(watcher);
            var list = watch.FindProperty("_guards");
            list.arraySize = guards.Length;
            for (var i = 0; i < guards.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = guards[i];
            var idleList = watch.FindProperty("_idles");
            idleList.arraySize = idles.Length;
            for (var i = 0; i < idles.Length; i++)
                idleList.GetArrayElementAtIndex(i).objectReferenceValue = idles[i];
            // The middle row: the test kitchen, the guards' own room, the hall of fame.
            var rects = watch.FindProperty("_watched");
            rects.arraySize = 3;
            for (var column = 0; column < 3; column++)
                rects.GetArrayElementAtIndex(column).rectValue = new Rect(
                    ColumnEdges[column], RowEdges[1], ColumnEdges[column + 1] - ColumnEdges[column], RowEdges[2] - RowEdges[1]);
            watch.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
