using System.Collections.Generic;
using System.IO;
using Game.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Renders a picture of each ingredient from its own prefab, on a transparent background, for
    /// menus. The kitchen build renders only missing pictures, so they don't change on every build;
    /// the menu item renders them all again after a model changes.
    /// </summary>
    public static class IngredientIconRenderer
    {
        public const string IconFolder = "Assets/_Project/Art/Textures/Icons";
        private const string CatalogPath = "Assets/_Project/Data/UI/IngredientIcons.asset";
        private const string ItemPrefabFolder = "Assets/_Project/Prefabs/Items";
        private const int Size = 128;
        private const float Pitch = 40f;         // looking down at the food, a little flatter than the game camera
        private const float FieldOfView = 25f;
        private const float Margin = 1.15f;      // room around the food

        public static IngredientIcons Catalog() => GreyboxAssets.EnsureAsset<IngredientIcons>(CatalogPath);

        [MenuItem("Tools/Game/Art/Render Ingredient Icons")]
        private static void RenderAllFromMenu() => Render(force: true);

        public static void Render(bool force)
        {
            GreyboxAssets.EnsureFolder(IconFolder);
            var scene = EditorSceneManager.NewPreviewScene();
            var rendered = new List<string>();
            try
            {
                var light = new GameObject("Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                EditorSceneManager.MoveGameObjectToScene(light.gameObject, scene);

                var camera = new GameObject("Camera").AddComponent<Camera>();
                camera.scene = scene;
                camera.fieldOfView = FieldOfView;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                EditorSceneManager.MoveGameObjectToScene(camera.gameObject, scene);

                var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                foreach (var type in GreyboxIngredients.Types)
                {
                    var path = IconPath(type.Id);
                    if (!force && File.Exists(path))
                        continue;

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemPrefabFolder}/PF_Item_{type.Id}.prefab");
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[IngredientIconRenderer] No prefab for {type.Id}; build the kitchen first.");
                        continue;
                    }

                    var food = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    foreach (var particles in food.GetComponentsInChildren<ParticleSystem>())
                        particles.gameObject.SetActive(false);
                    Frame(camera, food);
                    File.WriteAllBytes(path, RenderTo(camera, target).EncodeToPNG());
                    Object.DestroyImmediate(food);
                    rendered.Add(path);
                }
                target.Release();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            foreach (var path in rendered)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            FillCatalog();
        }

        public static string IconPath(string typeId) => $"{IconFolder}/ICON_{typeId}.png";

        private static void FillCatalog()
        {
            var catalog = Catalog();
            var entries = new List<IngredientIcons.Entry>();
            foreach (var type in GreyboxIngredients.Types)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath(type.Id));
                if (sprite != null)
                    entries.Add(new IngredientIcons.Entry(type.Id, sprite));
            }
            catalog.Entries = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        // Fits the food's bounds in view, from in front and above.
        private static void Frame(Camera camera, GameObject food)
        {
            var bounds = new Bounds(food.transform.position, Vector3.zero);
            foreach (var renderer in food.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);

            var rotation = Quaternion.Euler(Pitch, 0f, 0f);
            var distance = bounds.extents.magnitude * Margin / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);
            camera.nearClipPlane = distance * 0.1f;
            camera.farClipPlane = distance * 4f;
        }

        private static Texture2D RenderTo(Camera camera, RenderTexture target)
        {
            camera.targetTexture = target;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
                camera.Render();
            camera.targetTexture = null;

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            return image;
        }
    }
}
