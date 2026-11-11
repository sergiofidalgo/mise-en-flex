using System;
using System.IO;
using System.Linq;
using Game.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Tools/Game/Bake Shop Pictures (the user's call, 2026-11-11): photographs every piece the shop
    /// sells, in a three-quarter view from a little above, the way the kitchen camera sees it, on a
    /// clear background, and gives each to its catalog entry, so the shop card's grid shows the real thing. Furniture comes from its
    /// catalog prefab, kitchenware from its item prefab. Baked once rather than rendered live, so four
    /// players can shop at once for free. Run it again after a piece's model changes.
    /// Batch mode (editor closed):
    /// Unity -batchmode -projectPath . -executeMethod Game.Editor.ShopPictures.BakeFromCommandLine
    /// </summary>
    public static class ShopPictures
    {
        private const string CatalogPath = "Assets/_Project/Data/Settings/FurnitureCatalog.asset";
        private const string ItemPrefabFolder = "Assets/_Project/Prefabs/Items";
        public const string Folder = "Assets/_Project/Art/UI/Shop";

        // About twice the biggest size a tile shows them, for Retina screens.
        private const int Size = 256;
        private const float FieldOfView = 20f;
        // The kitchen camera looks down steeply; a little less here shows the fronts, where the
        // dials and doors are.
        private const float LookDownDegrees = 32f;
        private const float TurnDegrees = 30f;
        private const float Margin = 1.04f; // over 1: a little air around the piece

        [MenuItem("Tools/Game/Bake Shop Pictures")]
        public static void Bake()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FurnitureCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[ShopPictures] Missing {CatalogPath}.");
                return;
            }
            Directory.CreateDirectory(Folder);

            var scene = EditorSceneManager.NewPreviewScene();
            var target = PhotoStudio.Target(Size);
            var baked = 0;
            try
            {
                // The same warm key and cool fill as the portraits, so every baked picture belongs together.
                PhotoStudio.Light(scene, "Key", Quaternion.Euler(45f, 200f, 0f), 1.15f, new Color(1f, 0.96f, 0.9f));
                PhotoStudio.Light(scene, "Fill", Quaternion.Euler(20f, 130f, 0f), 0.5f, new Color(0.85f, 0.9f, 1f));
                var camera = PhotoStudio.Camera(scene, FieldOfView);

                foreach (var entry in catalog.Entries)
                {
                    var prefab = PrefabOf(entry);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[ShopPictures] No model for '{entry.Id}'; skipped.");
                        continue;
                    }
                    var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    try
                    {
                        piece.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        var bounds = Bounds(piece);
                        if (bounds.size == Vector3.zero)
                        {
                            Debug.LogWarning($"[ShopPictures] '{entry.Id}' draws nothing; skipped.");
                            continue;
                        }
                        Frame(camera, bounds);
                        PhotoStudio.Render(camera, target, PathOf(entry.Id));
                        baked++;
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(piece);
                    }
                }
                AssetDatabase.Refresh();
                // Each picture into its catalog entry, for the shop card.
                var so = new SerializedObject(catalog);
                var entries = so.FindProperty("_entries");
                for (var i = 0; i < entries.arraySize; i++)
                {
                    var element = entries.GetArrayElementAtIndex(i);
                    var path = PathOf(element.FindPropertyRelative("_id").stringValue);
                    element.FindPropertyRelative("_picture").objectReferenceValue =
                        File.Exists(path) ? MenuArt.Sprite(Path.GetFileNameWithoutExtension(path), Folder) : null;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ShopPictures] Baked {baked} pictures into {Folder}.");
            }
            finally
            {
                target.Release();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        public static void BakeFromCommandLine()
        {
            try
            {
                Bake();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Where a piece's picture lives: TEX_Shop_{id}, spaces as underscores.</summary>
        public static string PathOf(string id) => $"{Folder}/TEX_Shop_{id.Replace(' ', '_')}.png";

        private static GameObject PrefabOf(FurnitureCatalog.Entry entry) =>
            entry.Prefab != null ? entry.Prefab : entry.IsItem
                ? AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemPrefabFolder}/PF_Item_{entry.ItemTypeId}.prefab")
                : null;

        // Everything that draws, without the helpers (labels, gizmos) that are switched off.
        private static Bounds Bounds(GameObject piece)
        {
            var renderers = piece.GetComponentsInChildren<Renderer>()
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0)
                return new Bounds();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        // The whole piece in the frame: the camera backs off until its bounding sphere fits.
        private static void Frame(Camera camera, Bounds bounds)
        {
            var radius = bounds.extents.magnitude;
            var distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad) * Margin;
            // Pieces face -Z (towards the dining room and the kitchen camera): the camera stands
            // there, turned a little to one side.
            var rotation = Quaternion.Euler(LookDownDegrees, TurnDegrees, 0f);
            camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);
        }
    }
}
