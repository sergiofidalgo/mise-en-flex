using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data;
using Game.Presentation;
using Game.Presentation.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Tools/Game/Art/Render Dish Pictures (the user's call, 2026-11-11: the Restaurant tab should show
    /// the plates, not just their names). Each cookbook recipe is plated from the game's own models,
    /// cooked (a medium steak, golden onions), photographed from a three-quarter view on a clear
    /// background, and stored on the HUD style by recipe id. Steam is drawn over it by the dashboard.
    /// </summary>
    public static class DishPictures
    {
        private const string StylePath = "Assets/_Project/Data/Settings/HudStyle.asset";
        private const string CookbookPath = "Assets/_Project/Data/Settings/CookbookConfig.asset";
        private const string ItemPrefabFolder = "Assets/_Project/Prefabs/Items";
        private const string Folder = "Assets/_Project/Art/UI/Dishes";
        private const int Size = 384;            // shown at up to 190 px: twice that for Retina
        private const float FieldOfView = 24f;
        private const float LookDownDegrees = 48f;
        private const float Margin = 1.06f;

        [MenuItem("Tools/Game/Art/Render Dish Pictures")]
        public static void Render()
        {
            var cookbook = AssetDatabase.LoadAssetAtPath<CookbookConfig>(CookbookPath);
            var plate = Prefab("Plate");
            if (cookbook == null || plate == null)
            {
                Debug.LogError("[DishPictures] Needs the Cookbook Config and the plate prefab; run Tools/Game/Build Greybox Kitchen first.");
                return;
            }
            Directory.CreateDirectory(Folder);

            var scene = EditorSceneManager.NewPreviewScene();
            var target = PhotoStudio.Target(Size);
            var baked = new List<(string Id, string Path)>();
            try
            {
                // The kitchen's warm key from the front-left, and a cool fill so the far side reads.
                PhotoStudio.Light(scene, "Key", Quaternion.Euler(50f, 30f, 0f), 1.2f, new Color(1f, 0.95f, 0.86f));
                PhotoStudio.Light(scene, "Fill", Quaternion.Euler(20f, -120f, 0f), 0.45f, new Color(0.85f, 0.9f, 1f));
                var camera = PhotoStudio.Camera(scene, FieldOfView);

                foreach (var entry in cookbook.Entries)
                {
                    var root = new GameObject("Dish");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                    var plateObject = Place(plate, root.transform);
                    var plateBounds = Bounds(plateObject);
                    var radius = Mathf.Min(plateBounds.extents.x, plateBounds.extents.z);

                    var hasSide = entry.Side != null && Prefab(entry.Side) != null;
                    // The main takes most of the plate, the side tucks in beside it.
                    var main = Prefab(entry.Main);
                    if (main != null)
                        Plate(main, root.transform, plateBounds, new Vector3(hasSide ? -radius * 0.25f : 0f, 0f, hasSide ? 0.08f * radius : 0f), entry.Main, radius * (hasSide ? 1.15f : 1.45f));
                    if (hasSide)
                        Plate(Prefab(entry.Side), root.transform, plateBounds, new Vector3(radius * 0.5f, 0f, -radius * 0.22f), entry.Side, radius * 0.8f);

                    Frame(camera, Bounds(root));
                    var path = $"{Folder}/TEX_Dish_{entry.Id}.png";
                    PhotoStudio.Render(camera, target, path);
                    baked.Add((entry.Id, path));
                    Object.DestroyImmediate(root);
                }
            }
            finally
            {
                target.Release();
                EditorSceneManager.ClosePreviewScene(scene);
            }

            AssetDatabase.Refresh();
            var style = AssetDatabase.LoadAssetAtPath<HudStyle>(StylePath);
            var so = new SerializedObject(style);
            var list = so.FindProperty("_dishPictures");
            list.arraySize = baked.Count;
            for (var i = 0; i < baked.Count; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_recipeId").stringValue = baked[i].Id;
                element.FindPropertyRelative("_picture").objectReferenceValue = MenuArt.Sprite(Path.GetFileNameWithoutExtension(baked[i].Path), Folder);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DishPictures] Rendered {baked.Count} dishes into {Folder} and the HUD style.");
        }

        private static GameObject Prefab(string typeId) =>
            string.IsNullOrEmpty(typeId) ? null : AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemPrefabFolder}/PF_Item_{typeId}.prefab");

        private static GameObject Place(GameObject prefab, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            // Steam and sizzle belong to the kitchen; the dashboard draws its own steam.
            foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>(true))
                particles.gameObject.SetActive(false);
            return instance;
        }

        // Food on the plate: resting on its top, at an offset from its centre, cooked to a nice look.
        private static void Plate(GameObject prefab, Transform parent, Bounds plate, Vector3 offset, string typeId, float footprint)
        {
            var food = Place(prefab, parent);
            // The game's food is chunky for the overhead camera: scaled down to sit on the plate.
            var bounds = Bounds(food);
            var across = Mathf.Max(bounds.size.x, bounds.size.z);
            if (across > footprint)
                food.transform.localScale *= footprint / across;
            bounds = Bounds(food);
            var top = plate.max.y - plate.size.y * 0.25f; // into the plate's dip a little
            food.transform.position += new Vector3(plate.center.x - bounds.center.x, top - bounds.min.y, plate.center.z - bounds.center.z) + offset;
            food.transform.Rotate(0f, typeId == "Steak" ? 20f : -15f, 0f, Space.World);
            foreach (var view in food.GetComponentsInChildren<IngredientView>(true))
            {
                view.ApplyVariation(7);
                // A medium steak (core 57 °C), nicely seared; onions just golden. Browning is a share of burnt.
                if (typeId == "Steak")
                    view.ShowState(0.38f, 0.42f, 160f, 57f, 160f);
                else
                    view.ShowState(0.18f, 0.18f, 100f, 90f, 100f);
            }
        }

        private static Bounds Bounds(GameObject root)
        {
            // Meshes only: highlight sprites and effects would stretch the frame.
            var renderers = root.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.one * 0.1f);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        // A three-quarter view looking down, the whole plate in frame.
        private static void Frame(Camera camera, Bounds dish)
        {
            var rotation = Quaternion.Euler(LookDownDegrees, -25f, 0f);
            var radius = dish.extents.magnitude;
            var distance = radius * Margin / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(dish.center - rotation * Vector3.forward * distance, rotation);
            camera.nearClipPlane = distance * 0.1f;
            camera.farClipPlane = distance * 4f;
        }
    }
}
