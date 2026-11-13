using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Sets up the kitchen's sound: fills the sound bank from the clips in the SFX folder and adds
    /// what plays them to the scene. Safe to run again: tuning in the bank is kept.
    /// </summary>
    public static class GreyboxAudio
    {
        public const string BankPath = "Assets/_Project/Data/Settings/SoundBank.asset";
        private const string ClipFolder = "Assets/_Project/Audio/SFX";
        private const string SizzleLoopName = "SFX_Sizzle_Loop";
        private const string FireLoopName = "SFX_Fire_Loop";
        private const string ScenesFolder = "Assets/_Project/Scenes";

        /// <summary>Adds sound to the kitchen scene that's open, without rebuilding the rest of it.</summary>
        [MenuItem("Tools/Game/Add Sound To Kitchen")]
        public static void AddToOpenScene()
        {
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var camera = Camera.main;
            if (root == null || camera == null)
            {
                Debug.LogError("Open the kitchen scene first: it needs its CompositionRoot and Main Camera.");
                return;
            }

            AddTo(root, camera);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log("Kitchen sound added. Save the scene (Cmd+S), then press Play.");
        }

        /// <summary>
        /// The same for every saved kitchen scene and the office, without the Editor open: run it with
        /// -executeMethod. A scene that doesn't exist yet gets its sound from its builder when it's built.
        /// </summary>
        public static void AddToSavedScene()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Scene Kitchen_", new[] { ScenesFolder }))
            {
                // Open each scene before loading the bank: opening one unloads assets nothing references yet.
                var scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid), OpenSceneMode.Single);
                var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
                if (root == null)
                    continue;
                AddTo(root, Camera.main);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new System.InvalidOperationException($"Couldn't save {scene.path}.");
            }

            var office = $"{ScenesFolder}/Office_Greybox.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(office) == null)
                return;
            var officeScene = EditorSceneManager.OpenScene(office, OpenSceneMode.Single);
            AddToOffice();
            if (!EditorSceneManager.SaveScene(officeScene))
                throw new System.InvalidOperationException($"Couldn't save {office}.");
        }

        /// <summary>Fills in the bank's clips, and makes sure the loop plays without a gap.</summary>
        [MenuItem("Tools/Game/Update Sound Bank")]
        public static SoundBank EnsureBank()
        {
            var bank = GreyboxAssets.EnsureAsset<SoundBank>(BankPath);
            var so = new SerializedObject(bank);
            SetClips(so, "_bell", "SFX_Bell_");
            SetClips(so, "_chopClean", "SFX_Chop_Clean_");
            SetClips(so, "_chopOff", "SFX_Chop_Off_");
            SetClips(so, "_chopMiss", "SFX_Chop_Miss_");
            SetClips(so, "_doorOpen", "SFX_Door_Open_");
            SetClips(so, "_doorClose", "SFX_Door_Close_");
            SetClips(so, "_sizzleHit", "SFX_Sizzle_Hit_");
            SetClips(so, "_fridgeOpen", "SFX_Fridge_Open_");
            SetClips(so, "_fridgeClose", "SFX_Fridge_Close_");
            SetClips(so, "_alarmBeep", "SFX_Alarm_");
            SetClips(so, "_step", "SFX_Step_");
            SetClips(so, "_clack", "SFX_Clack_");
            SetClips(so, "_till", "SFX_Till_");
            SetClips(so, "_chime", "SFX_Chime_");
            SetClips(so, "_deliveryPing", "SFX_DeliveryPing_");
            SetClips(so, "_courierHorn", "SFX_CourierHorn_");
            SetClips(so, "_uiMove", "SFX_UI_Move_");
            SetClips(so, "_uiConfirm", "SFX_UI_Confirm_");
            SetClips(so, "_uiBack", "SFX_UI_Back_");
            SetClips(so, "_deny", "SFX_Deny_");
            SetClips(so, "_pickup", "SFX_Pickup_");
            SetClips(so, "_join", "SFX_Join_");
            SetClips(so, "_heatClick", "SFX_Heat_Click_");
            SetClips(so, "_gasIgnite", "SFX_Gas_Ignite_");
            SetClips(so, "_gasOff", "SFX_Gas_Off_");
            SetClips(so, "_fireWhoosh", "SFX_Fire_Whoosh_");
            SetClips(so, "_fireOut", "SFX_Fire_Out_");
            SetClips(so, "_scrub", "SFX_Scrub_");
            SetClips(so, "_mop", "SFX_Mop_");
            SetClips(so, "_spray", "SFX_Spray_");
            SetClips(so, "_cleanDone", "SFX_Clean_Done_");
            SetClips(so, "_wash", "SFX_Wash_");
            SetClips(so, "_splash", "SFX_Splash_");
            SetClips(so, "_splat", "SFX_Splat_");
            SetClips(so, "_binThunk", "SFX_Bin_");
            SetClips(so, "_binBag", "SFX_Bag_");
            SetClips(so, "_knife", "SFX_Knife_");
            SetClips(so, "_furniture", "SFX_Furniture_");
            SetClips(so, "_serveGood", "SFX_Serve_Good_");
            SetClips(so, "_serveOk", "SFX_Serve_Ok_");
            SetClips(so, "_servePoor", "SFX_Serve_Poor_");
            SetClips(so, "_ticket", "SFX_Ticket_");
            SetClips(so, "_walkout", "SFX_Walkout_");
            SetClips(so, "_countdown", "SFX_Count_");
            SetClips(so, "_doorsOpen", "SFX_Open_");
            SetClips(so, "_serviceOver", "SFX_Close_");
            SetClips(so, "_scooterIn", "SFX_Scooter_In_");
            SetClips(so, "_scooterOut", "SFX_Scooter_Out_");
            SetClips(so, "_phone", "SFX_Phone_");
            SetClips(so, "_fanfare", "SFX_Fanfare_");

            MakeSeamless(SizzleLoopName);
            so.FindProperty("_sizzleLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipFolder}/{SizzleLoopName}.wav");
            MakeSeamless(FireLoopName);
            so.FindProperty("_fireLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipFolder}/{FireLoopName}.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return bank;
        }

        public static void AddTo(KitchenCompositionRoot root, Camera camera)
        {
            var bank = EnsureBank();

            if (Object.FindFirstObjectByType<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();

            var audio = Object.FindFirstObjectByType<KitchenAudio>();
            if (audio == null)
                audio = new GameObject("KitchenAudio").AddComponent<KitchenAudio>();
            var audioSo = new SerializedObject(audio);
            audioSo.FindProperty("_root").objectReferenceValue = root;
            audioSo.FindProperty("_sounds").objectReferenceValue = bank;
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            Attach<SizzleAudio>(audio, root);
            Attach<SmokeAlarm>(audio, root);
            Attach<PlayerSounds>(audio, null);
            Attach<HobAudio>(audio, root);
            Attach<CleaningAudio>(audio, root);
            Attach<StationAudio>(audio, root);
            Attach<ServiceAudio>(audio, root);

            foreach (var door in Object.FindObjectsByType<SwingDoorView>(FindObjectsSortMode.None))
            {
                var doorSo = new SerializedObject(door);
                doorSo.FindProperty("_audio").objectReferenceValue = audio;
                doorSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // A listener on the KitchenAudio object, pointed at it (and at the root, if it takes one).
        private static void Attach<T>(KitchenAudio audio, KitchenCompositionRoot root) where T : MonoBehaviour
        {
            var listener = audio.GetComponent<T>();
            if (listener == null)
                listener = audio.gameObject.AddComponent<T>();
            var so = new SerializedObject(listener);
            so.FindProperty("_audio").objectReferenceValue = audio;
            var rootProperty = so.FindProperty("_root");
            if (rootProperty != null)
                rootProperty.objectReferenceValue = root;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The boss's floor: its sound player, which the main and pause menus play through too.</summary>
        public static void AddToOffice()
        {
            var bank = EnsureBank();
            var audio = Object.FindFirstObjectByType<Game.Presentation.Office.OfficeAudio>();
            if (audio == null)
                audio = new GameObject("OfficeAudio").AddComponent<Game.Presentation.Office.OfficeAudio>();
            var so = new SerializedObject(audio);
            so.FindProperty("_sounds").objectReferenceValue = bank;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetClips(SerializedObject bank, string cue, string namePrefix)
        {
            var paths = AssetDatabase.FindAssets($"{namePrefix} t:AudioClip", new[] { ClipFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => System.IO.Path.GetFileName(path).StartsWith(namePrefix))
                .OrderBy(path => path)
                .ToArray();
            if (paths.Length == 0)
                Debug.LogWarning($"No clips named {namePrefix}* in {ClipFolder}.");

            var clips = bank.FindProperty(cue).FindPropertyRelative("_clips");
            clips.arraySize = paths.Length;
            for (var i = 0; i < paths.Length; i++)
                clips.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);
        }

        // Compressed clips can gain a little padding at the ends, which a loop plays as a tick.
        private static void MakeSeamless(string clipName)
        {
            if (!(AssetImporter.GetAtPath($"{ClipFolder}/{clipName}.wav") is AudioImporter importer))
            {
                Debug.LogWarning($"No clip named {clipName} in {ClipFolder}.");
                return;
            }

            var settings = importer.defaultSampleSettings;
            if (settings.compressionFormat == AudioCompressionFormat.PCM)
                return;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}
