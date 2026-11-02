using System;
using System.Linq;
using Game.Data;
using Game.Gameplay.Items;
using Game.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// Builds the placeholder ingredients: an IngredientDefinition asset, a primitive prefab and
    /// a catalog entry for each. Definitions are created once and then left alone, so tuning
    /// done in the Editor survives rebuilds; prefabs and the catalog are rebuilt every time.
    /// </summary>
    public static class GreyboxIngredients
    {
        private const string DefinitionFolder = "Assets/_Project/Data/Ingredients";
        private const string CatalogPath = "Assets/_Project/Data/Items/ItemCatalog.asset";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Items";

        public sealed class IngredientType
        {
            public string Id;
            public Color Color;
            public PrimitiveType Shape;
            public Vector3 Scale;
            public Vector3 CratePosition;

            // Layered food (a slab like steak) is built as a top surface over three edge bands,
            // so its doneness can be read from the side; anything else is a single shape.
            public bool Layered;

            // Applied only when the definition asset is first created.
            public Action<SerializedObject> InitialTuning;

            /// <summary>Height of the primitive's centre above its base (spheres are 1 unit tall, cylinders 2).</summary>
            public float CentreHeight => Shape == PrimitiveType.Cylinder ? Scale.y : Scale.y / 2f;
        }

        public static readonly IngredientType[] Types =
        {
            new IngredientType
            {
                // 300 g, 2.5 cm steak: IngredientDefinition's defaults are tuned for this. Shown
                // oversized (19 x 16 x 4 cm) so it reads from the overhead camera.
                Id = "Steak", Color = new Color(0.62f, 0.12f, 0.14f), Shape = PrimitiveType.Cylinder,
                Scale = new Vector3(0.19f, 0.02f, 0.16f), CratePosition = new Vector3(-4.4f, 0f, -1.5f),
                Layered = true,
                InitialTuning = so => SetDoneness(so,
                    ("Rare", 45f), ("Medium-rare", 52f), ("Medium", 57f), ("Medium-well", 63f), ("Well done", 68f)),
            },
            new IngredientType
            {
                Id = "Tomato", Color = new Color(0.90f, 0.18f, 0.12f), Shape = PrimitiveType.Sphere,
                Scale = Vector3.one * 0.28f, CratePosition = new Vector3(-4.4f, 0f, 1.5f),
                InitialTuning = so => SetVegetable(so, massKg: 0.12f),
            },
            new IngredientType
            {
                Id = "Onion", Color = new Color(0.93f, 0.84f, 0.62f), Shape = PrimitiveType.Sphere,
                Scale = Vector3.one * 0.26f, CratePosition = new Vector3(-4.4f, 0f, 0f),
                InitialTuning = so => SetVegetable(so, massKg: 0.15f),
            },
        };

        /// <param name="kitchenware">Pans, plates and other non-ingredient items, by type id.</param>
        public static ItemCatalog BuildCatalog(params (string TypeId, GameObject Prefab)[] kitchenware)
        {
            var definitions = Types.Select(BuildDefinition).ToArray();

            var catalog = GreyboxAssets.EnsureAsset<ItemCatalog>(CatalogPath);
            var so = new SerializedObject(catalog);
            var list = so.FindProperty("_ingredients");
            list.arraySize = definitions.Length;
            for (var i = 0; i < definitions.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];

            var wares = so.FindProperty("_kitchenware");
            wares.arraySize = kitchenware.Length;
            for (var i = 0; i < kitchenware.Length; i++)
            {
                var entry = wares.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_typeId").stringValue = kitchenware[i].TypeId;
                entry.FindPropertyRelative("_prefab").objectReferenceValue = kitchenware[i].Prefab;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        /// <summary>A collider-less copy of the item's shape, e.g. as a label on top of a crate.</summary>
        public static GameObject CreateShape(IngredientType type, string name, Transform parent)
        {
            var shape = GreyboxAssets.CreatePrimitive(
                type.Shape, name, parent, GreyboxAssets.EnsureMaterial(type.Id, type.Color), keepCollider: false);
            shape.transform.localScale = type.Scale;
            return shape;
        }

        private static IngredientDefinition BuildDefinition(IngredientType type)
        {
            var path = $"{DefinitionFolder}/ING_{type.Id}.asset";
            var isNew = AssetDatabase.LoadAssetAtPath<IngredientDefinition>(path) == null;
            var definition = GreyboxAssets.EnsureAsset<IngredientDefinition>(path);

            var so = new SerializedObject(definition);
            so.FindProperty("_typeId").stringValue = type.Id;
            so.FindProperty("_prefab").objectReferenceValue = BuildPrefab(type);
            if (isNew)
                type.InitialTuning?.Invoke(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static GameObject BuildPrefab(IngredientType type)
        {
            // Root at the item's base so it sits on whatever anchor holds it.
            var root = new GameObject($"PF_Item_{type.Id}");
            var itemView = root.AddComponent<ItemView>();

            Renderer surface;
            var bands = Array.Empty<Renderer>();
            if (type.Layered)
                (surface, bands) = BuildLayers(type, root.transform);
            else
            {
                var body = CreateShape(type, "Body", root.transform);
                body.transform.localPosition = new Vector3(0f, type.CentreHeight, 0f);
                surface = body.GetComponent<Renderer>();
            }

            var view = root.AddComponent<IngredientView>();
            var so = new SerializedObject(view);
            so.FindProperty("_itemView").objectReferenceValue = itemView;
            so.FindProperty("_surface").objectReferenceValue = surface;
            var bandList = so.FindProperty("_bands");
            bandList.arraySize = bands.Length;
            for (var i = 0; i < bands.Length; i++)
                bandList.GetArrayElementAtIndex(i).objectReferenceValue = bands[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            GreyboxAssets.EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/PF_Item_{type.Id}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Three stacked bands (bottom, middle, top) make up the slab's edge, each a third of its
        // thickness, with a thin disc on top for the surface seen from above. They share one
        // material; IngredientView tints each through a property block.
        private const float SurfaceDiscThickness = 0.001f;

        private static (Renderer Surface, Renderer[] Bands) BuildLayers(IngredientType type, Transform parent)
        {
            var material = GreyboxAssets.EnsureMaterial(type.Id, type.Color);
            var thickness = type.Scale.y * 2f; // cylinder primitives are 2 units tall
            var bandHeight = thickness / 3f;
            var names = new[] { "Band_Bottom", "Band_Core", "Band_Top" };
            var bands = new Renderer[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                var band = GreyboxAssets.CreatePrimitive(PrimitiveType.Cylinder, names[i], parent, material, keepCollider: false);
                band.transform.localScale = new Vector3(type.Scale.x, bandHeight / 2f, type.Scale.z);
                band.transform.localPosition = new Vector3(0f, bandHeight * (i + 0.5f), 0f);
                bands[i] = band.GetComponent<Renderer>();
            }

            var surface = GreyboxAssets.CreatePrimitive(PrimitiveType.Cylinder, "Surface", parent, material, keepCollider: false);
            surface.transform.localScale = new Vector3(type.Scale.x, SurfaceDiscThickness / 2f, type.Scale.z);
            surface.transform.localPosition = new Vector3(0f, thickness + SurfaceDiscThickness / 2f, 0f);
            return (surface.GetComponent<Renderer>(), bands);
        }

        private static void SetDoneness(SerializedObject so, params (string Name, float MinPeakCore)[] levels)
        {
            var list = so.FindProperty("_donenessLevels");
            list.arraySize = levels.Length;
            for (var i = 0; i < levels.Length; i++)
            {
                var level = list.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("_name").stringValue = levels[i].Name;
                level.FindPropertyRelative("_minPeakCoreTemperature").floatValue = levels[i].MinPeakCore;
            }
        }

        // Whole vegetables: no doneness levels yet (cut and sautéed onions come in milestone 5),
        // stored at room temperature, slower to spoil than meat.
        private static void SetVegetable(SerializedObject so, float massKg)
        {
            so.FindProperty("_massKg").floatValue = massKg;
            so.FindProperty("_specificHeat").floatValue = 3900f;
            so.FindProperty("_startTemperature").floatValue = 20f;
            so.FindProperty("_freshnessLossPerHourAt20C").floatValue = 1f / 96f;
        }
    }
}
