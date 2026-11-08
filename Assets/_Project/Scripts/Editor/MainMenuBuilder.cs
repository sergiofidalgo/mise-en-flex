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
    /// kitchen's toon style. With the city models (SM_City, from the Blender script) the buildings get
    /// cornices, framed windows, shopfronts with awnings and roof water towers, and the street gets
    /// lantern lamps, cartoon cars and trees; without them, simple shapes stand in. The city is
    /// seeded, so every rebuild is the same. Menu: Tools/Game/Build Main Menu (Build Greybox Kitchen
    /// runs it too).
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

        private const string CityModel = "SM_City";
        private static bool Modelled => PilotArt.FindMesh(CityModel, "CityBlock") != null;

        // Trim and street furniture, warm against the night blues.
        private static readonly Color WindowTrim = new Color(0.8f, 0.74f, 0.64f);
        private static readonly Color ShopTrim = new Color(0.34f, 0.21f, 0.14f);
        private static readonly Color LampIron = new Color(0.12f, 0.16f, 0.14f);
        private static readonly Color[] AwningColors =
        {
            new Color(0.78f, 0.22f, 0.18f), new Color(0.2f, 0.48f, 0.36f), new Color(0.22f, 0.32f, 0.58f), new Color(0.9f, 0.6f, 0.2f),
        };
        private static readonly Color AwningCream = new Color(0.93f, 0.88f, 0.78f);

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
            BuildForeground(city);
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

            for (var x = -27f; x <= 27f; x += 13.5f)
                Lamp(street, new Vector3(x, 0f, 1.6f), 9f, 3f);

            Color[] paints = { new Color(0.62f, 0.20f, 0.18f), new Color(0.22f, 0.36f, 0.48f), new Color(0.78f, 0.70f, 0.58f), new Color(0.30f, 0.42f, 0.32f) };
            for (var i = 0; i < 5; i++)
            {
                var car = new GameObject("Car").transform;
                car.SetParent(street, false);
                car.localPosition = new Vector3((i < 2 ? -34f : 14f) + (i % 3) * 11f + (float)random.NextDouble() * 3f, 0f, i % 2 == 0 ? -3.8f : -8.2f);
                var paint = Toon($"NightCar{i % paints.Length}", paints[i % paints.Length]);
                var glass = Unlit("NightCarGlass", new Color(0.18f, 0.22f, 0.34f));
                if (Modelled)
                {
                    // The two lanes drive opposite ways.
                    car.localRotation = Quaternion.Euler(0f, i % 2 == 0 ? 0f : 180f, 0f);
                    Model(car, "Body", "CityCarBody", paint, Vector3.zero);
                    Model(car, "Glass", "CityCarGlass", glass, Vector3.zero);
                    Model(car, "Wheels", "CityCarWheels", Toon("CityTyre", new Color(0.08f, 0.08f, 0.09f)), Vector3.zero);
                    Model(car, "Headlights", "CityCarHeadlights", Unlit("CityHeadlight", new Color(1f, 0.95f, 0.75f)), Vector3.zero);
                    Model(car, "Taillights", "CityCarTaillights", Unlit("CityTaillight", new Color(0.95f, 0.22f, 0.16f)), Vector3.zero);
                    continue;
                }
                Shape(car, PrimitiveType.Cube, "Body", paint, new Vector3(0f, 0.65f, 0f), new Vector3(4.2f, 0.9f, 1.9f));
                Shape(car, PrimitiveType.Cube, "Cabin", paint, new Vector3(-0.3f, 1.4f, 0f), new Vector3(2.2f, 0.7f, 1.7f));
                Shape(car, PrimitiveType.Cube, "Glass", glass, new Vector3(-0.3f, 1.42f, 0f), new Vector3(2.25f, 0.5f, 1.72f));
            }
        }

        // A street lamp at `at`, with a warm point light; the lantern model when there is one.
        private static void Lamp(Transform parent, Vector3 at, float range, float intensity)
        {
            var lamp = new GameObject("Lamp").transform;
            lamp.SetParent(parent, false);
            lamp.localPosition = at;
            var glow = Unlit("NightLampGlow", WindowLit);
            var lightHeight = 4.2f;
            if (Modelled)
            {
                Model(lamp, "Post", "CityLampPost", Toon("CityLampIron", LampIron), Vector3.zero);
                Model(lamp, "Glass", "CityLampGlass", glow, Vector3.zero);
                lightHeight = 4.3f;
            }
            else
            {
                Shape(lamp, PrimitiveType.Cylinder, "Post", Toon("NightLampPost", new Color(0.12f, 0.10f, 0.10f)), new Vector3(0f, 2.2f, 0f), new Vector3(0.18f, 2.2f, 0.18f));
                Shape(lamp, PrimitiveType.Sphere, "Bulb", glow, new Vector3(0f, 4.5f, 0f), Vector3.one * 0.55f);
            }
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(lamp, false);
            light.transform.localPosition = new Vector3(0f, lightHeight, 0f);
            light.type = LightType.Point;
            light.color = WindowLit;
            light.range = range;
            light.intensity = intensity;
        }

        // Nearest the camera, along the bottom of the screen below the menu's panels: trees along
        // the near pavement, and glowing lamps in the lower corners (the user's mockup).
        private static void BuildForeground(Transform city)
        {
            if (!Modelled)
                return;
            var near = new GameObject("Foreground").transform;
            near.SetParent(city, false);
            var trunk = Toon("CityTreeTrunk", new Color(0.32f, 0.22f, 0.15f));
            var crown = Toon("CityTreeCrown", new Color(0.2f, 0.36f, 0.26f));
            void Tree(Vector3 at, float size)
            {
                var tree = new GameObject("Tree").transform;
                tree.SetParent(near, false);
                tree.localPosition = at;
                tree.localScale = Vector3.one * size;
                Model(tree, "Trunk", "CityTreeTrunk", trunk, Vector3.zero);
                Model(tree, "Crown", "CityTreeCrown", crown, Vector3.zero);
            }
            for (var x = -40.5f; x <= 40.5f; x += 13.5f)
                Tree(new Vector3(x, 0f, -12.6f), 1f);
            foreach (var side in new[] { -1f, 1f })
            {
                Lamp(near, new Vector3(side * 7.8f, 0f, -27f), 10f, 3.5f);
                Tree(new Vector3(side * 12f, 0f, -21f), 1.15f);
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
                    var wall = Toon($"NightBuilding{System.Array.IndexOf(BuildingColors, colour)}", colour);
                    var tower = random.NextDouble() < 0.3;
                    if (Modelled)
                    {
                        Model(building, "Body", "CityBlock", wall, Vector3.zero, new Vector3(width, height, depth));
                        Model(building, "Cornice", "CityCornice", roof, new Vector3(0f, height, 0f), new Vector3(width + 0.5f, 1f, depth + 0.5f));
                        if (tower)
                            WaterTower(building, new Vector3(width * 0.2f, height + 0.35f, depth * 0.1f));
                    }
                    else
                    {
                        Shape(building, PrimitiveType.Cube, "Body", wall, new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth));
                        Shape(building, PrimitiveType.Cube, "Cap", roof, new Vector3(0f, height + 0.15f, 0f), new Vector3(width + 0.4f, 0.3f, depth + 0.4f));
                        if (tower)
                            Shape(building, PrimitiveType.Cylinder, "Tank", roof, new Vector3(width * 0.2f, height + 1.2f, 0f), new Vector3(1.6f, 1f, 1.6f));
                    }

                    // The front row's ground floors are shops, with an awning over a glowing window.
                    var firstFloor = 2f;
                    if (Modelled && row == 0 && width > 6.5f && random.NextDouble() < 0.75)
                    {
                        Shopfront(building, width, depth, random);
                        firstFloor = 4.2f;
                    }

                    // Windows on the front, the side the camera sees; about four in ten lit. The
                    // nearer rows get frames and sills.
                    if (row < 3)
                    {
                        for (var wy = firstFloor; wy < height - 1f; wy += 2.4f)
                        for (var wx = -width / 2f + 1.2f; wx < width / 2f - 0.8f; wx += 2f)
                        {
                            var at = new Vector3(wx + 0.4f, wy, -depth / 2f - 0.02f);
                            Shape(building, PrimitiveType.Quad, "Window", random.NextDouble() < 0.4 ? lit : dark,
                                at, new Vector3(0.9f, 1.2f, 1f), Quaternion.identity);
                            if (Modelled && row < 2)
                            {
                                var face = new Vector3(at.x, at.y, -depth / 2f);
                                Model(building, "Frame", "CityWindowFrame", Toon("CityWindowTrim", WindowTrim), face);
                                Model(building, "Sill", "CityWindowSill", Toon("CityWindowTrim", WindowTrim), face);
                            }
                        }
                    }
                    x += width + 0.6f + (float)random.NextDouble() * 1.5f;
                }
            }
        }

        // A water tower on a roof: legs, a wooden tank with bands, a cone roof.
        private static void WaterTower(Transform building, Vector3 at)
        {
            var tower = new GameObject("WaterTower").transform;
            tower.SetParent(building, false);
            tower.localPosition = at;
            var iron = Toon("CityTowerIron", new Color(0.15f, 0.13f, 0.16f));
            Model(tower, "Legs", "CityTowerLegs", iron, Vector3.zero);
            Model(tower, "Tank", "CityTowerTank", Toon("CityTowerWood", new Color(0.45f, 0.3f, 0.2f)), Vector3.zero);
            Model(tower, "Roof", "CityTowerRoof", iron, Vector3.zero);
        }

        // A shopfront along a front-row building's ground floor: glowing windows in dark frames, and
        // a striped awning over them in one of a few colours.
        private static void Shopfront(Transform building, float width, float depth, System.Random random)
        {
            var front = -depth / 2f;
            var trim = Toon("CityShopTrim", ShopTrim);
            var glow = Unlit("NightShopGlow", new Color(1f, 0.86f, 0.55f));
            var shops = Mathf.Max(1, Mathf.FloorToInt((width - 1f) / 3.1f));
            var spacing = width / shops;
            for (var i = 0; i < shops; i++)
            {
                var x = -width / 2f + spacing * (i + 0.5f);
                Shape(building, PrimitiveType.Quad, "ShopWindow", glow, new Vector3(x, 1.55f, front - 0.02f), new Vector3(2.4f, 1.8f, 1f), Quaternion.identity);
                Model(building, "ShopFrame", "CityShopFrame", trim, new Vector3(x, 1.55f, front));
            }

            var colour = random.Next(AwningColors.Length);
            var stripe = Toon($"CityAwning{colour}", AwningColors[colour]);
            var cream = Toon("CityAwningCream", AwningCream);
            var modules = Mathf.FloorToInt(width - 0.6f);
            for (var i = 0; i < modules; i++)
            {
                var at = new Vector3(-modules / 2f + i + 0.5f, 2.95f, front);
                Model(building, "Awning", "CityAwningA", stripe, at);
                Model(building, "Awning", "CityAwningB", cream, at);
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

        // A part of the city model, placed; null without the model.
        private static GameObject Model(Transform parent, string name, string mesh, Material material, Vector3 position,
            Vector3? scale = null, Quaternion? rotation = null)
        {
            var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent, material, keepCollider: false);
            if (!PilotArt.TryUseMesh(part, CityModel, mesh))
            {
                Object.DestroyImmediate(part);
                return null;
            }
            part.transform.localPosition = position;
            part.transform.localRotation = rotation ?? Quaternion.identity;
            part.transform.localScale = scale ?? Vector3.one;
            part.isStatic = true;
            return part;
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
