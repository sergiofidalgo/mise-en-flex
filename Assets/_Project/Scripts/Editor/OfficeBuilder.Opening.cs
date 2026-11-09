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
        // From the street at the tower's foot (OfficeBuilder.Street.cs) up past the rooftops; the
        // last pose is the stop, with the tower on the left third and its lit floor in frame.
        private static readonly CameraPose[] FlightPoses =
        {
            new CameraPose(new Vector3(42f, -118.4f, -23.5f), new Vector3(0f, -118f, -23.5f)),
            new CameraPose(new Vector3(22f, -116.8f, -25f), new Vector3(-10f, -116f, -21f)),
            // Rising out of the street, looking up the tower's glass into the sky...
            new CameraPose(new Vector3(8f, -110f, -26f), new Vector3(-2f, -60f, -12f)),
            // ...then back over the shops' roofs, into the open corridor (OpeningCorridor), the city opening up.
            new CameraPose(new Vector3(34f, -80f, -80f), new Vector3(8f, -35f, -6f)),
            new CameraPose(new Vector3(48f, -42f, -118f), new Vector3(14f, -14f, 2f)),
        };

        // Up the glass towards the floor, before the turn onto the follow camera's wide shot.
        private static readonly CameraPose[] ClimbPoses =
        {
            new CameraPose(new Vector3(20f, -30f, -60f), new Vector3(0f, -10f, -12f)),
            new CameraPose(new Vector3(6f, -6f, -30f), new Vector3(0f, 0f, -8f)),
        };

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
