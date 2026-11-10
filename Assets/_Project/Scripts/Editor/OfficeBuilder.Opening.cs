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
        // The user's diagram (2026-11-09): in at the far end of the avenue at eye height, gliding
        // up the middle of the road (clear of both traffic lanes) through its hustle, to stop in the
        // road with the tower straight ahead at the avenue's end and the menu over it. The last pose
        // is the stop. Heights are above the street (OfficeBuilder.Street.cs, 120 m below the floor).
        private static readonly CameraPose[] FlightPoses =
        {
            StreetPose(0.6f, 2.0f, -296f, 0.5f, 2.6f, -256f),
            StreetPose(0.4f, 2.4f, -262f, 0.2f, 4.0f, -215f),
            StreetPose(0.2f, 3.6f, -232f, 0f, 14f, -150f),
            // The stop: a little above the crowd, looking up the avenue to the top of the tower.
            StreetPose(0f, 6f, -205f, 0f, 34.7f, -105f),
        };

        // After the menu: on up the avenue and over the tower's street, then up its glass, floor by
        // floor, to the top, where the camera tips over onto the floor's wide shot (the landing is
        // added by OpeningSequence: the follow camera's own view).
        private static readonly CameraPose[] ClimbPoses =
        {
            StreetPose(0f, 9f, -130f, 0f, 60f, -14f),
            StreetPose(0f, 14f, -50f, 0f, 70f, -14f),
            StreetPose(0f, 30f, -30f, 0f, 38f, TowerFace),
            StreetPose(0f, 80f, -29f, 0f, 84f, TowerFace),
            StreetPose(0f, 110f, -29f, 0f, 116f, TowerFace),
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

            // Tap Menu on the floor: the pause menu (decision 2026-11-10). No day loop here, so it stops time itself.
            PauseMenuBuilder.Add(style, coop);
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
