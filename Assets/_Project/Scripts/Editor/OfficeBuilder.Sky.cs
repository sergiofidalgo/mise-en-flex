using Game.Presentation.Office.Ambience;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The night sky (user's note, 2026-11-09: "on the intro scene the sky is super important too...
    /// stars, the moon, perhaps some moving plane?"): a procedural skybox (SH_NightSky) with stars
    /// that twinkle, the moon with its halo up to the right of the tower as the avenue looks at it,
    /// slow clouds, and planes crossing high over the city, blinking.
    /// </summary>
    public static partial class OfficeBuilder
    {
        private const string SkyMaterialPath = "Assets/_Project/Art/Materials/MAT_Sky_Night.mat";

        // Up and just left of the tower's top, seen from the avenue (looking north, +z): in the open
        // sky between the avenue's buildings at the menu's stop, and over the terrace.
        private static readonly Vector3 MoonDirection = new Vector3(-0.13f, 0.56f, 0.82f);

        private static void NightSky(Camera camera)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            var shader = Shader.Find("Game/NightSky");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, SkyMaterialPath);
            }
            material.shader = shader;
            material.SetVector("_MoonDirection", MoonDirection.normalized);
            material.SetFloat("_StarDensity", 0.85f);
            material.SetFloat("_StarBrightness", 2.2f);
            EditorUtility.SetDirty(material);
            RenderSettings.skybox = material;
            camera.clearFlags = CameraClearFlags.Skybox;

            var sky = new GameObject("Sky").transform;
            // An airliner crossing high over the tower, and another further off the other way.
            Airliner(sky, new Vector3(-650f, 140f, 60f), new Vector3(650f, 140f, 60f), 55f, 0f);
            Airliner(sky, new Vector3(650f, 210f, 170f), new Vector3(-650f, 230f, 170f), 42f, 0.55f);
        }

        // A plane in silhouette with its lights: red on the left wingtip, green on the right, white
        // strobes double-flashing and a red beacon on its back. Flies its line again and again.
        private static void Airliner(Transform parent, Vector3 from, Vector3 to, float speed, float phase)
        {
            var plane = new GameObject("Airliner").transform;
            plane.SetParent(parent, false);
            var body = Toon("SkyPlane", new Color(0.2f, 0.22f, 0.3f));
            Shape(plane, PrimitiveType.Capsule, "Fuselage", body, Vector3.zero, new Vector3(3.6f, 16f, 3.6f), Quaternion.Euler(90f, 0f, 0f));
            Block(plane, "Wings", body, new Vector3(0f, -0.6f, 1f), new Vector3(34f, 0.5f, 5f));
            Block(plane, "Tailplane", body, new Vector3(0f, 0.2f, -13.5f), new Vector3(12f, 0.4f, 2.6f));
            Block(plane, "Fin", body, new Vector3(0f, 0.4f, -13.5f), new Vector3(0.5f, 6f, 3.2f));
            Lamp("NavRed", new Vector3(-17f, -0.4f, 1f), new Color(1f, 0.2f, 0.15f), 1.6f, 0f, 0f);
            Lamp("NavGreen", new Vector3(17f, -0.4f, 1f), new Color(0.3f, 1f, 0.4f), 1.6f, 0f, 0f);
            foreach (var at in new[] { new Vector3(-17f, -0.2f, 0f), new Vector3(17f, -0.2f, 0f), new Vector3(0f, 0.6f, -16f) })
            {
                Lamp("Strobe", at, Color.white, 2.2f, 1.3f, 0.06f);
                Lamp("Strobe", at, Color.white, 2.2f, 1.3f, 0.06f, 0.14f); // the second of the double flash
            }
            Lamp("Beacon", new Vector3(0f, 2.2f, 0f), new Color(1f, 0.25f, 0.2f), 1.8f, 1.1f, 0.3f);
            plane.gameObject.AddComponent<LaneMover>().Set(from, to, speed, false, 0f, phase);
            MakeDynamic(plane.gameObject);

            void Lamp(string name, Vector3 at, Color colour, float size, float period, float on, float offset = 0f)
            {
                var holder = new GameObject(name).transform;
                holder.SetParent(plane, false);
                holder.localPosition = at;
                var lamp = Shape(holder, PrimitiveType.Sphere, "Lamp", Unlit($"SkyLight{ColorUtility.ToHtmlStringRGB(colour)}", colour), Vector3.zero, Vector3.one * size);
                if (period > 0f)
                    holder.gameObject.AddComponent<Blink>().Set(lamp, period, on, offset / period);
            }
        }
    }
}
