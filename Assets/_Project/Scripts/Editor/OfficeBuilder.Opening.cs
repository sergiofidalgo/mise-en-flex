using Game.Data;
using Game.Presentation.Hud;
using Game.Presentation.Menu;
using Game.Presentation.Office;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Editor
{
    /// <summary>
    /// The opening in the office scene (docs/opening-flow.md; decision 2026-11-09, one scene): the
    /// main menu, hidden until the camera stops on the tower, the camera's path from the street up
    /// through the city, and the HUD for the fades, the save notice, "A Skip" and the join slots.
    /// </summary>
    public static partial class OfficeBuilder
    {
        // The user's notes (2026-11-10): in at street level close to the tower, from the avenue's
        // mouth, through the crowd to stop right in front of the main entrance, looking at the
        // restaurant, its queue and its marquee; the menu comes up there. The last pose is the stop.
        // Heights are above the street (OfficeBuilder.Street.cs, 120 m below the floor).
        private static readonly CameraPose[] FlightPoses =
        {
            StreetPose(4.5f, 1.7f, -47f, 1.5f, 2.4f, TowerFace),
            StreetPose(2.2f, 1.75f, -38f, 0.6f, 3.0f, TowerFace),
            // The stop: on the far pavement, the doors, the queue and the marquee ahead.
            StreetPose(0.4f, 1.8f, -29.6f, 0.4f, 4.4f, TowerFace),
        };

        // After the chef has stepped into the lift: up beside it, a few metres off the glass, floor
        // by floor, to the top, where the camera tips over onto the floor's wide shot (the landing is
        // added by OpeningSequence: the follow camera's own view).
        private static readonly CameraPose[] ClimbPoses =
        {
            StreetPose(0f, 3.5f, -25.5f, 0f, 4.5f, TowerFace),
            StreetPose(0f, 40f, -25f, 0f, 41f, TowerFace),
            StreetPose(0f, 85f, -25f, 0f, 86f, TowerFace),
            StreetPose(0f, 112f, -24f, 0f, 116f, TowerFace),
        };

        private static CameraPose StreetPose(float x, float height, float z, float tx, float targetHeight, float tz) =>
            new CameraPose(new Vector3(x, -CityDepth + height, z), new Vector3(tx, -CityDepth + targetHeight, tz));

        private static void AddOpening(Camera camera, OfficeCamera follow, PlayerInputManager manager, CoopSettings coop)
        {
            var style = AssetDatabase.LoadAssetAtPath<HudStyle>(SettingsFolder + "/HudStyle.asset");

            var menu = new GameObject("MainMenu").AddComponent<MainMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("_startHidden").boolValue = true;
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_coop").objectReferenceValue = coop;
            so.FindProperty("_realism").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RealismSettings>(SettingsFolder + "/RealismSettings.asset");
            so.FindProperty("_service").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ServiceConfig>(SettingsFolder + "/ServiceConfig.asset");
            so.FindProperty("_medium").objectReferenceValue = MenuArt.Font("Inter-Medium");
            so.FindProperty("_semiBold").objectReferenceValue = MenuArt.Font("Inter-SemiBold");
            so.FindProperty("_bold").objectReferenceValue = MenuArt.Font("Inter-Bold");
            so.FindProperty("_serifItalic").objectReferenceValue = MenuArt.Font("Lora-Italic");
            so.FindProperty("_sign").objectReferenceValue = MenuArt.Sprite("TEX_Menu_Sign");
            so.FindProperty("_forkcastLogo").objectReferenceValue = MenuArt.Sprite("TEX_Menu_ForkcastLogo");
            so.FindProperty("_searchIcon").objectReferenceValue = MenuArt.Sprite("TEX_Menu_IconSearch");
            so.FindProperty("_bellIcon").objectReferenceValue = MenuArt.Sprite("TEX_Menu_IconBell");
            so.FindProperty("_polaroidFirst").objectReferenceValue = MenuArt.Sprite("TEX_Menu_PolaroidFirstUp");
            so.FindProperty("_polaroidNext").objectReferenceValue = MenuArt.Sprite("TEX_Menu_PolaroidNextUp");
            so.FindProperty("_slotDashed").objectReferenceValue = MenuArt.Sprite("TEX_Menu_SlotDashed");
            so.FindProperty("_slotPlus").objectReferenceValue = MenuArt.Sprite("TEX_Menu_SlotPlus");
            so.FindProperty("_avatarEmpty").objectReferenceValue = MenuArt.Sprite("TEX_Menu_AvatarEmpty");
            so.FindProperty("_cookEmpty").objectReferenceValue = MenuArt.Sprite("TEX_Menu_CookEmpty");
            so.ApplyModifiedPropertiesWithoutUndo();

            var opening = new GameObject("Opening");
            var hud = opening.AddComponent<OpeningHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("_style").objectReferenceValue = style;
            hudSo.FindProperty("_coop").objectReferenceValue = coop;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            var sequence = new SerializedObject(opening.AddComponent<OpeningSequence>());
            sequence.FindProperty("_camera").objectReferenceValue = camera;
            sequence.FindProperty("_follow").objectReferenceValue = follow;
            sequence.FindProperty("_menu").objectReferenceValue = menu;
            sequence.FindProperty("_players").objectReferenceValue = manager;
            sequence.FindProperty("_hud").objectReferenceValue = hud;
            sequence.FindProperty("_elevator").objectReferenceValue = Object.FindFirstObjectByType<OpeningElevator>();
            sequence.FindProperty("_chef").objectReferenceValue = Object.FindFirstObjectByType<OpeningChef>();
            sequence.FindProperty("_streetLevel").objectReferenceValue = GameObject.Find("City/Street");
            Poses(sequence.FindProperty("_flight"), FlightPoses);
            Poses(sequence.FindProperty("_climb"), ClimbPoses);
            sequence.ApplyModifiedPropertiesWithoutUndo();

            // The miniature table's hint (built with the back room).
            var table = Object.FindFirstObjectByType<Game.Gameplay.Office.MiniatureTable>();
            var tableHud = new SerializedObject(opening.AddComponent<MiniatureHud>());
            tableHud.FindProperty("_style").objectReferenceValue = style;
            tableHud.FindProperty("_table").objectReferenceValue = table;
            tableHud.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Poses(SerializedProperty list, CameraPose[] poses)
        {
            list.arraySize = poses.Length;
            for (var i = 0; i < poses.Length; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Position").vector3Value = poses[i].Position;
                item.FindPropertyRelative("Target").vector3Value = poses[i].Target;
            }
        }
    }
}
