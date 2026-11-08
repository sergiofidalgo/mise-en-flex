using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Presentation.Hud;
using Game.Presentation.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Builds the main menu scene (decision 2026-11-08): the menu screen over a 3D night city in the
    /// kitchen's toon style, from simple shapes until real models replace them. The city is seeded, so
    /// every rebuild is the same. Menu: Tools/Game/Build Main Menu (Build Greybox Kitchen runs it too).
    /// </summary>
    public static class MainMenuBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string KitchenScenePath = "Assets/_Project/Scenes/Kitchen_Greybox.unity";
        private const string SettingsFolder = "Assets/_Project/Data/Settings";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";
        private const int Seed = 2611;

        // A deep blue night: the sky the camera clears to, and the moonlight's tint.
        private static readonly Color NightSky = new Color(0.09f, 0.10f, 0.20f);
        private static readonly Color Moonlight = new Color(0.62f, 0.68f, 0.95f);
        private static readonly Color WindowLit = new Color(1f, 0.80f, 0.45f);
        private static readonly Color WindowDark = new Color(0.12f, 0.13f, 0.22f);

        private static readonly Color[] BuildingColors =
        {
            new Color(0.22f, 0.24f, 0.38f), new Color(0.30f, 0.25f, 0.36f), new Color(0.36f, 0.27f, 0.25f),
            new Color(0.25f, 0.31f, 0.36f), new Color(0.40f, 0.33f, 0.30f),
        };

        [MenuItem("Tools/Game/Build Main Menu")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
            moon.color = Moonlight;
            moon.intensity = 0.75f;
            moon.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.20f, 0.22f, 0.38f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.15f, 0.24f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.09f, 0.12f);

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(new Vector3(0f, 6f, -46f), Quaternion.Euler(-3f, 0f, 0f));
            camera.fieldOfView = 42f;
            camera.farClipPlane = 400f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = NightSky;
            HouseStyle.SmoothEdges(camera);

            var city = new GameObject("City").transform;
            var random = new System.Random(Seed);
            BuildStreet(city, random);
            BuildBlocks(city, random);
            BuildSky(city, random);

            var menu = new GameObject("MainMenu").AddComponent<MainMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("_style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HudStyle>(SettingsFolder + "/HudStyle.asset");
            so.FindProperty("_coop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CoopSettings>(SettingsFolder + "/CoopSettings.asset");
            so.FindProperty("_realism").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RealismSettings>(SettingsFolder + "/RealismSettings.asset");
            so.FindProperty("_service").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ServiceConfig>(SettingsFolder + "/ServiceConfig.asset");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);

            // The menu comes first in a build; the kitchen is loaded from it.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(KitchenScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log($"[MainMenuBuilder] Built {ScenePath}.");
        }

        // The near street: road, pavements, lamps and a few parked cars, in front of the camera.
        private static void BuildStreet(Transform city, System.Random random)
        {
            var street = new GameObject("Street").transform;
            street.SetParent(city, false);
            Slab(street, "Road", Toon("NightRoad", new Color(0.13f, 0.13f, 0.17f)), new Vector3(0f, -0.05f, -6f), new Vector3(140f, 0.1f, 10f));
            Slab(street, "Pavement", Toon("NightPavement", new Color(0.33f, 0.24f, 0.24f)), new Vector3(0f, 0.05f, 0.5f), new Vector3(140f, 0.2f, 3f));
            Slab(street, "PavementNear", Toon("NightPavement", new Color(0.33f, 0.24f, 0.24f)), new Vector3(0f, 0.05f, -12f), new Vector3(140f, 0.2f, 3f));
            // A plaza between the camera and the street, so the ground reaches the bottom of the frame.
            Slab(street, "Plaza", Toon("NightPlaza", new Color(0.24f, 0.20f, 0.24f)), new Vector3(0f, -0.05f, -36f), new Vector3(160f, 0.1f, 46f));
            var line = Unlit("NightRoadLine", new Color(0.85f, 0.70f, 0.30f));
            for (var x = -60f; x <= 60f; x += 6f)
                Slab(street, "Line", line, new Vector3(x, 0.01f, -6f), new Vector3(2.4f, 0.02f, 0.2f));

            var pole = Toon("NightLampPost", new Color(0.12f, 0.10f, 0.10f));
            var glow = Unlit("NightLampGlow", WindowLit);
            for (var x = -27f; x <= 27f; x += 13.5f)
            {
                var lamp = new GameObject("Lamp").transform;
                lamp.SetParent(street, false);
                lamp.localPosition = new Vector3(x, 0f, 1.6f);
                Shape(lamp, PrimitiveType.Cylinder, "Post", pole, new Vector3(0f, 2.2f, 0f), new Vector3(0.18f, 2.2f, 0.18f));
                Shape(lamp, PrimitiveType.Sphere, "Bulb", glow, new Vector3(0f, 4.5f, 0f), Vector3.one * 0.55f);
                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(lamp, false);
                light.transform.localPosition = new Vector3(0f, 4.2f, 0f);
                light.type = LightType.Point;
                light.color = WindowLit;
                light.range = 9f;
                light.intensity = 3f;
            }

            Color[] paints = { new Color(0.62f, 0.20f, 0.18f), new Color(0.22f, 0.36f, 0.48f), new Color(0.78f, 0.70f, 0.58f), new Color(0.30f, 0.42f, 0.32f) };
            for (var i = 0; i < 5; i++)
            {
                var car = new GameObject("Car").transform;
                car.SetParent(street, false);
                car.localPosition = new Vector3((i < 2 ? -34f : 14f) + (i % 3) * 11f + (float)random.NextDouble() * 3f, 0f, i % 2 == 0 ? -3.8f : -8.2f);
                var paint = Toon($"NightCar{i % paints.Length}", paints[i % paints.Length]);
                Shape(car, PrimitiveType.Cube, "Body", paint, new Vector3(0f, 0.65f, 0f), new Vector3(4.2f, 0.9f, 1.9f));
                Shape(car, PrimitiveType.Cube, "Cabin", paint, new Vector3(-0.3f, 1.4f, 0f), new Vector3(2.2f, 0.7f, 1.7f));
                Shape(car, PrimitiveType.Cube, "Glass", Unlit("NightCarGlass", new Color(0.18f, 0.22f, 0.34f)), new Vector3(-0.3f, 1.42f, 0f), new Vector3(2.25f, 0.5f, 1.72f));
            }
        }

        // Rows of buildings stepping back into the night, the nearer ones with lit windows.
        private static void BuildBlocks(Transform city, System.Random random)
        {
            var blocks = new GameObject("Blocks").transform;
            blocks.SetParent(city, false);
            var lit = Unlit("NightWindowLit", WindowLit);
            var dark = Unlit("NightWindowDark", WindowDark);
            var roof = Toon("NightRoof", new Color(0.14f, 0.13f, 0.18f));
            var rows = new[] { 4f, 16f, 30f, 48f };
            for (var row = 0; row < rows.Length; row++)
            {
                var x = -70f;
                while (x < 70f)
                {
                    var width = 6f + (float)random.NextDouble() * 8f;
                    var depth = 8f + (float)random.NextDouble() * 6f;
                    // Low in front, taller behind: a skyline under an open sky.
                    var height = 4f + row * 5f + (float)random.NextDouble() * (5f + row * 6f);
                    var colour = BuildingColors[random.Next(BuildingColors.Length)];
                    var building = new GameObject("Building").transform;
                    building.SetParent(blocks, false);
                    building.localPosition = new Vector3(x + width / 2f, 0f, rows[row] + depth / 2f);
                    Shape(building, PrimitiveType.Cube, "Body", Toon($"NightBuilding{System.Array.IndexOf(BuildingColors, colour)}", colour), new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth));
                    Shape(building, PrimitiveType.Cube, "Cap", roof, new Vector3(0f, height + 0.15f, 0f), new Vector3(width + 0.4f, 0.3f, depth + 0.4f));
                    if (random.NextDouble() < 0.3)
                        Shape(building, PrimitiveType.Cylinder, "Tank", roof, new Vector3(width * 0.2f, height + 1.2f, 0f), new Vector3(1.6f, 1f, 1.6f));

                    // Windows on the front, the side the camera sees; about four in ten lit.
                    if (row < 3)
                    {
                        for (var wy = 2f; wy < height - 1f; wy += 2.4f)
                        for (var wx = -width / 2f + 1.2f; wx < width / 2f - 0.8f; wx += 2f)
                            Shape(building, PrimitiveType.Quad, "Window", random.NextDouble() < 0.4 ? lit : dark,
                                new Vector3(wx + 0.4f, wy, -depth / 2f - 0.02f), new Vector3(0.9f, 1.2f, 1f), Quaternion.identity);
                    }
                    x += width + 0.6f + (float)random.NextDouble() * 1.5f;
                }
            }
        }

        // A moon and a scatter of stars, far behind the skyline.
        private static void BuildSky(Transform city, System.Random random)
        {
            var sky = new GameObject("Sky").transform;
            sky.SetParent(city, false);
            Shape(sky, PrimitiveType.Sphere, "Moon", Unlit("NightMoon", new Color(0.98f, 0.94f, 0.80f)), new Vector3(38f, 62f, 180f), Vector3.one * 14f);
            var star = Unlit("NightStar", new Color(0.95f, 0.95f, 1f));
            for (var i = 0; i < 90; i++)
            {
                var position = new Vector3(-170f + (float)random.NextDouble() * 340f, 40f + (float)random.NextDouble() * 110f, 200f);
                Shape(sky, PrimitiveType.Quad, "Star", star, position, Vector3.one * (0.4f + (float)random.NextDouble() * 0.6f), Quaternion.identity);
            }
        }

        private static void Slab(Transform parent, string name, Material material, Vector3 position, Vector3 size) =>
            Shape(parent, PrimitiveType.Cube, name, material, position, size);

        private static void Shape(Transform parent, PrimitiveType type, string name, Material material, Vector3 position, Vector3 scale, Quaternion? rotation = null)
        {
            var shape = GreyboxAssets.CreatePrimitive(type, name, parent, material, keepCollider: false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            if (rotation.HasValue)
                shape.transform.localRotation = rotation.Value;
            shape.isStatic = true;
        }

        private static Material Toon(string name, Color color) => StyleTest.EnsureToonMaterial(name, color);

        // Lights that glow at night (windows, lamps, the moon): unlit, so they read bright in the dark.
        private static Material Unlit(string name, Color color)
        {
            var path = $"Assets/_Project/Art/Materials/MAT_Unlit_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(UnlitShader));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
