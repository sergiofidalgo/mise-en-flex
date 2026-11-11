using System.IO;
using System.Linq;
using Game.Presentation;
using Game.Presentation.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// Tools/Game/Bake Customer Portraits (the user's call, 2026-11-11): photographs each look of the
    /// customer prefab, head and shoulders, on a clear background, and gives the pictures to the HUD
    /// style, so a review card shows the person who came instead of a drawn face. Baked once rather
    /// than rendered live, so a dashboard full of cards costs nothing. Run it again after the
    /// customer looks change (Tools/Game/Build Greybox Kitchen rebuilds them). Renders in a preview
    /// scene, so the open scene is left alone.
    /// </summary>
    public static class CustomerPortraits
    {
        private const string CustomerPrefabPath = "Assets/_Project/Prefabs/PF_Customer.prefab";
        private const string StylePath = "Assets/_Project/Data/Settings/HudStyle.asset";
        private const string Folder = "Assets/_Project/Art/UI/Portraits";

        // Drawn at about twice the biggest size a card shows them, for Retina screens.
        private const int Size = 256;
        private const float FieldOfView = 24f;
        // A three-quarter view reads as a person more than a passport photo does.
        private const float TurnDegrees = 22f;
        private const float LookDownDegrees = 6f;
        // From the hat's top to the shoulders, as multiples of the head's height.
        private const float AboveHead = 0.75f;
        private const float BelowHead = 0.45f;
        private const float Margin = 0.94f; // under 1: the circle crops the corners anyway

        [MenuItem("Tools/Game/Bake Customer Portraits")]
        public static void Bake()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[CustomerPortraits] Missing {CustomerPrefabPath}; run Tools/Game/Build Greybox Kitchen first.");
                return;
            }
            Directory.CreateDirectory(Folder);

            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; // the pipeline's MSAA
            try
            {
                var person = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                person.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var picker = person.GetComponent<CharacterLookPicker>();
                var looks = person.transform.Cast<Transform>().Where(t => t.name.StartsWith("Look_")).ToArray();

                // The kitchen's warm sun from the front, and a soft fill so the far cheek isn't black.
                Light(scene, "Key", Quaternion.Euler(35f, 200f, 0f), 1.15f, new Color(1f, 0.96f, 0.9f));
                Light(scene, "Fill", Quaternion.Euler(15f, 140f, 0f), 0.45f, new Color(0.85f, 0.9f, 1f));
                var camera = Camera(scene);

                var paths = new string[looks.Length];
                for (var i = 0; i < looks.Length; i++)
                {
                    picker.Show(i);
                    var head = looks[i].GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name.StartsWith("CharHead"));
                    if (head == null)
                    {
                        Debug.LogError($"[CustomerPortraits] {looks[i].name} has no CharHead; skipped.");
                        continue;
                    }
                    Frame(camera, looks[i], head.bounds);
                    paths[i] = $"{Folder}/TEX_Portrait_Customer_{i}_{looks[i].name.Substring("Look_".Length)}.png";
                    Render(camera, target, paths[i]);
                }

                AssetDatabase.Refresh();
                var sprites = paths.Select(p => p == null ? null : MenuArt.Sprite(Path.GetFileNameWithoutExtension(p), Folder)).ToArray();
                var style = AssetDatabase.LoadAssetAtPath<HudStyle>(StylePath);
                var so = new SerializedObject(style);
                var list = so.FindProperty("_customerPortraits");
                list.arraySize = sprites.Length;
                for (var i = 0; i < sprites.Length; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(style);
                AssetDatabase.SaveAssets();
                Debug.Log($"[CustomerPortraits] Baked {sprites.Count(s => s != null)} portraits into {Folder} and the HUD style.");
            }
            finally
            {
                target.Release();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Light(Scene scene, string name, Quaternion rotation, float intensity, Color colour)
        {
            var light = new GameObject(name).AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            light.type = LightType.Directional;
            light.transform.rotation = rotation;
            light.intensity = intensity;
            light.color = colour;
            light.shadows = LightShadows.None;
        }

        private static Camera Camera(Scene scene)
        {
            var camera = new GameObject("PortraitCamera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.scene = scene;
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            // As the game draws people: the ink outline and the grading come with post-processing,
            // which fills the background, so the clear background is recovered by Render below.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            return camera;
        }

        // Head and shoulders, the hat included: the head's centre a little above the middle.
        private static void Frame(Camera camera, Transform look, Bounds head)
        {
            var headHeight = head.size.y;
            var top = head.max.y + headHeight * AboveHead;
            // A tall hat stretches the frame up to its top, but not past a head's height above.
            var tallest = look.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy)
                .Select(r => r.bounds.max.y).DefaultIfEmpty(top).Max();
            top = Mathf.Clamp(tallest + headHeight * 0.08f, head.max.y + headHeight * 0.1f, head.max.y + headHeight * 0.6f);
            var bottom = head.min.y - headHeight * BelowHead;
            var centre = new Vector3(head.center.x, (top + bottom) / 2f, head.center.z);
            var distance = (top - bottom) / 2f / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * Margin;

            // Customers face +Z: the camera stands in front, turned a little to one side.
            var rotation = Quaternion.Euler(LookDownDegrees, 180f + TurnDegrees, 0f);
            camera.transform.SetPositionAndRotation(centre - rotation * Vector3.forward * distance, rotation);
        }

        // Rendered on black and on white: where the two agree the person covers the pixel, and how
        // far they differ (against how far the backgrounds differ) is how see-through it is. That
        // keeps the outline's soft edge without guessing a key colour.
        private static void Render(Camera camera, RenderTexture target, string path)
        {
            var onBlack = Shoot(camera, target, Color.black);
            var onWhite = Shoot(camera, target, Color.white);
            // The backgrounds as they came out, after grading, from a corner.
            var black = onBlack[0];
            var white = onWhite[0];
            var spread = Mathf.Max(1f, (white.r - black.r + white.g - black.g + white.b - black.b) / 3f);
            var pixels = new Color32[onBlack.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var b = onBlack[i];
                var w = onWhite[i];
                var seen = ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f / spread;
                var alpha = Mathf.Clamp01(1f - seen);
                if (alpha < 0.01f)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }
                // The colour without the black background mixed in.
                byte Unmix(byte value, byte back) => (byte)Mathf.Clamp(Mathf.RoundToInt((value - back * (1f - alpha)) / alpha), 0, 255);
                pixels[i] = new Color32(Unmix(b.r, black.r), Unmix(b.g, black.g), Unmix(b.b, black.b), (byte)Mathf.RoundToInt(alpha * 255f));
            }
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            image.SetPixels32(pixels);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        private static Color32[] Shoot(Camera camera, RenderTexture target, Color background)
        {
            camera.backgroundColor = background;
            camera.targetTexture = target;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
                camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            var pixels = image.GetPixels32();
            Object.DestroyImmediate(image);
            return pixels;
        }
    }
}
