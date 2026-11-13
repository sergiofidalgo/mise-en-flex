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

        /// <summary>The same for the saved scene, without the Editor open: run it with -executeMethod.</summary>
        public static void AddToSavedScene()
        {
            // Open the scene before loading the bank: opening one unloads assets nothing references yet.
            var scene = EditorSceneManager.OpenScene(GreyboxKitchenBuilder.ScenePath, OpenSceneMode.Single);
            AddTo(Object.FindFirstObjectByType<KitchenCompositionRoot>(), Camera.main);
            if (!EditorSceneManager.SaveScene(scene))
                throw new System.InvalidOperationException($"Couldn't save {GreyboxKitchenBuilder.ScenePath}.");
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

            MakeSeamless(SizzleLoopName);
            so.FindProperty("_sizzleLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipFolder}/{SizzleLoopName}.wav");
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

            var sizzle = audio.GetComponent<SizzleAudio>();
            if (sizzle == null)
                sizzle = audio.gameObject.AddComponent<SizzleAudio>();
            var sizzleSo = new SerializedObject(sizzle);
            sizzleSo.FindProperty("_root").objectReferenceValue = root;
            sizzleSo.FindProperty("_audio").objectReferenceValue = audio;
            sizzleSo.ApplyModifiedPropertiesWithoutUndo();

            var alarm = audio.GetComponent<SmokeAlarm>();
            if (alarm == null)
                alarm = audio.gameObject.AddComponent<SmokeAlarm>();
            var alarmSo = new SerializedObject(alarm);
            alarmSo.FindProperty("_root").objectReferenceValue = root;
            alarmSo.FindProperty("_audio").objectReferenceValue = audio;
            alarmSo.ApplyModifiedPropertiesWithoutUndo();

            var chefs = audio.GetComponent<PlayerSounds>();
            if (chefs == null)
                chefs = audio.gameObject.AddComponent<PlayerSounds>();
            var chefsSo = new SerializedObject(chefs);
            chefsSo.FindProperty("_audio").objectReferenceValue = audio;
            chefsSo.ApplyModifiedPropertiesWithoutUndo();

            foreach (var door in Object.FindObjectsByType<SwingDoorView>(FindObjectsSortMode.None))
            {
                var doorSo = new SerializedObject(door);
                doorSo.FindProperty("_audio").objectReferenceValue = audio;
                doorSo.ApplyModifiedPropertiesWithoutUndo();
            }
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
