using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Editor
{
    /// <summary>
    /// Builds the Phase 0 greybox content from code: co-op settings, the player prefab
    /// and the kitchen scene. Re-run it after changing this script; it overwrites the
    /// prefab and scene but keeps an existing CoopSettings asset (that's where tuning lives).
    /// </summary>
    public static class GreyboxKitchenBuilder
    {
        private const string ControlsPath = "Assets/_Project/Data/Input/GameControls.inputactions";
        private const string SettingsFolder = "Assets/_Project/Data/Settings";
        private const string CoopSettingsPath = SettingsFolder + "/CoopSettings.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/PF_Player.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/Kitchen_Greybox.unity";
        private const string JoinActionPath = "Gameplay/Join";

        // Greybox layout, in metres. Placeholder until the real kitchen layout in step 5.
        private const float FloorWidth = 10f;
        private const float FloorDepth = 8f;
        private static readonly Vector3[] SpawnPositions = { new Vector3(-1.5f, 0f, 0f), new Vector3(1.5f, 0f, 0f) };

        // Fixed overhead camera (decision 2026-11-02): high and tilted so the whole kitchen
        // and both players stay in view.
        private static readonly Vector3 CameraPosition = new Vector3(0f, 11f, -8f);
        private static readonly Vector3 CameraEuler = new Vector3(55f, 0f, 0f);
        private const float CameraFieldOfView = 45f;

        // Placeholder chef proportions: ~1.7 m tall, origin at the feet.
        private static readonly Vector3 BodyScale = new Vector3(0.7f, 0.85f, 0.7f);
        private static readonly Vector3 NoseScale = new Vector3(0.2f, 0.2f, 0.3f);

        [MenuItem("Tools/Game/Build Greybox Kitchen")]
        public static void BuildAll()
        {
            var settings = EnsureCoopSettings();
            var playerPrefab = BuildPlayerPrefab(settings);
            BuildScene(settings, playerPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GreyboxKitchenBuilder] Built {PlayerPrefabPath} and {ScenePath}.");
        }

        private static CoopSettings EnsureCoopSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CoopSettings>(CoopSettingsPath);
            if (settings != null)
                return settings;

            EnsureFolder(SettingsFolder);
            settings = ScriptableObject.CreateInstance<CoopSettings>();
            AssetDatabase.CreateAsset(settings, CoopSettingsPath);
            return settings;
        }

        private static GameObject BuildPlayerPrefab(CoopSettings settings)
        {
            var controls = LoadControls();

            var root = new GameObject("PF_Player");
            var playerInput = root.AddComponent<PlayerInput>();
            playerInput.actions = controls;
            playerInput.defaultActionMap = "Gameplay";
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

            // Visuals live under one child so the join pop scales them without moving the root.
            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(root.transform, false);

            var body = CreatePrimitiveWithoutCollider(PrimitiveType.Capsule, "Body", visuals);
            body.transform.localScale = BodyScale;
            body.transform.localPosition = new Vector3(0f, BodyScale.y, 0f); // capsule is 2 units tall

            // A nose shows facing direction from the overhead camera.
            var nose = CreatePrimitiveWithoutCollider(PrimitiveType.Cube, "Nose", visuals);
            nose.transform.localScale = NoseScale;
            nose.transform.localPosition = new Vector3(0f, BodyScale.y * 1.5f, BodyScale.z * 0.5f);

            var visual = root.AddComponent<PlayerVisual>();
            var so = new SerializedObject(visual);
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_playerInput").objectReferenceValue = playerInput;
            so.FindProperty("_popTarget").objectReferenceValue = visuals;
            var renderers = so.FindProperty("_tintedRenderers");
            renderers.arraySize = 2;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = body.GetComponent<Renderer>();
            renderers.GetArrayElementAtIndex(1).objectReferenceValue = nose.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildScene(CoopSettings settings, GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(CameraEuler));
            camera.fieldOfView = CameraFieldOfView;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(FloorWidth / 10f, 1f, FloorDepth / 10f); // plane is 10x10

            var spawnRoot = new GameObject("SpawnPoints").transform;
            var spawnPoints = SpawnPositions.Select((position, i) =>
            {
                var point = new GameObject($"Spawn_P{i + 1}").transform;
                point.SetParent(spawnRoot, false);
                point.localPosition = position;
                return point;
            }).ToArray();

            var players = new GameObject("Players");
            var manager = players.AddComponent<PlayerInputManager>();
            manager.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            manager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenJoinActionIsTriggered;
            manager.joinAction = new InputActionProperty(LoadActionReference(JoinActionPath));
            manager.playerPrefab = playerPrefab;

            var join = players.AddComponent<PlayerJoinController>();
            var so = new SerializedObject(join);
            so.FindProperty("_settings").objectReferenceValue = settings;
            var points = so.FindProperty("_spawnPoints");
            points.arraySize = spawnPoints.Length;
            for (var i = 0; i < spawnPoints.Length; i++)
                points.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(System.IO.Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static InputActionAsset LoadControls()
        {
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
            if (controls == null)
                throw new System.InvalidOperationException($"Input actions not found at {ControlsPath}.");
            return controls;
        }

        // The importer stores an InputActionReference sub-asset per action; using it keeps
        // the scene's join action pointing at the asset instead of a serialized copy.
        private static InputActionReference LoadActionReference(string mapAndAction)
        {
            var action = LoadControls().FindAction(mapAndAction, throwIfNotFound: true);
            var reference = AssetDatabase.LoadAllAssetsAtPath(ControlsPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null && r.action.id == action.id);
            if (reference == null)
                throw new System.InvalidOperationException($"No InputActionReference for {mapAndAction}.");
            return reference;
        }

        private static GameObject CreatePrimitiveWithoutCollider(PrimitiveType type, string name, Transform parent)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            Object.DestroyImmediate(primitive.GetComponent<Collider>());
            primitive.transform.SetParent(parent, false);
            return primitive;
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
