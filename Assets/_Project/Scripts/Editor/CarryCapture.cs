using System;
using System.IO;
using Game.Data;
using Game.Gameplay;
using Game.Presentation;
using Game.Simulation.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Stills of chefs carrying each kind of item, posed by their real ChefArmsView, to judge how
    /// the hands meet what they hold (the user's note, 2026-11-10: hands went through a tray). A
    /// row facing the camera and a row side on, in the built kitchen. Nothing is saved.
    /// Batch mode: -executeMethod Game.Editor.CarryCapture.CaptureFromCommandLine -captureFolder &lt;folder&gt;
    /// </summary>
    public static class CarryCapture
    {
        private const string ScenePath = "Assets/_Project/Scenes/Kitchen_Greybox.unity";
        private const string PlayerPath = "Assets/_Project/Prefabs/PF_Player.prefab";
        private const string ItemsFolder = "Assets/_Project/Prefabs/Items";
        private const string CoopSettingsPath = "Assets/_Project/Data/Settings/CoopSettings.asset";
        private const int Width = 1920;
        private const int Height = 1080;
        private static readonly string[] Items = { "Tray", "Plate", "Container", "Onion" };

        [MenuItem("Tools/Game/Art/Capture Carrying")]
        private static void CaptureFromMenu()
        {
            var folder = EditorUtility.SaveFolderPanel("Save carrying captures", "", "");
            if (string.IsNullOrEmpty(folder) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Capture(folder);
            EditorUtility.RevealInFinder(folder);
        }

        public static void CaptureFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Pass -captureFolder <folder>.");
            Capture(args[index + 1]);
        }

        private static void Capture(string folder)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var coop = AssetDatabase.LoadAssetAtPath<CoopSettings>(CoopSettingsPath);
            var island = GameObject.Find("Zone_Island");
            var middle = island != null ? island.transform.position : Vector3.zero;
            var front = new Vector3(middle.x, 0f, middle.z - 2.6f);

            // Facing the camera, then side on (facing right), one item each.
            for (var i = 0; i < Items.Length; i++)
            {
                var x = (i - 1.5f) * 1.25f;
                Chef(Items[i], i, coop, front + new Vector3(x, 0f, 0f), 180f);
                Chef(Items[i], i, coop, front + new Vector3(x, 0f, -1.4f), 90f);
            }

            var camera = new GameObject("Capture Camera").AddComponent<Camera>();
            camera.fieldOfView = 30f;
            camera.farClipPlane = 200f;
            HouseStyle.SmoothEdges(camera);
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = target;

            // From the game camera's pitch, close; then straight on at chest height.
            var centre = front + new Vector3(0f, 1f, -0.7f);
            camera.transform.SetPositionAndRotation(centre + new Vector3(0f, 6.2f, -4.35f), Quaternion.Euler(55f, 0f, 0f));
            Draw(camera, target, Path.Combine(folder, "carry_play.png"));
            camera.transform.SetPositionAndRotation(centre + new Vector3(0f, 0.35f, -9f), Quaternion.Euler(2f, 0f, 0f));
            Draw(camera, target, Path.Combine(folder, "carry_front.png"));

            target.Release();
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            Debug.Log($"[CarryCapture] Saved to {folder}");
        }

        private static void Chef(string item, int player, CoopSettings coop, Vector3 at, float facing)
        {
            var chef = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
            chef.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, facing, 0f));
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", coop.GetPlayerColor(player));
            var tinted = new SerializedObject(chef.GetComponent<PlayerVisual>()).FindProperty("_tintedRenderers");
            for (var t = 0; t < tinted.arraySize; t++)
                ((Renderer)tinted.GetArrayElementAtIndex(t).objectReferenceValue)?.SetPropertyBlock(tint);
            chef.GetComponentInChildren<ChefWardrobe>(true)?.Show(new ChefLook { Hat = "toque", Face = "crop", Skin = "skin1", Outfit = "whites" });

            var arms = chef.GetComponentInChildren<ChefArmsView>(true);
            var anchor = (Transform)new SerializedObject(arms).FindProperty("_carryAnchor").objectReferenceValue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemsFolder}/PF_Item_{item}.prefab");
            var held = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
            held.transform.localPosition = Vector3.zero;
            held.transform.localRotation = Quaternion.identity;
            arms.PreviewHolding(pan: false);
        }

        private static void Draw(Camera camera, RenderTexture target, string path)
        {
            for (var pass = 0; pass < 2; pass++) // the first frame compiles shaders
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else
                    camera.Render();
            }
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
