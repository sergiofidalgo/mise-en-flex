using System;
using System.Linq;
using Game.Data;
using Game.Gameplay.Items;
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

            // Applied only when the definition asset is first created.
            public Action<SerializedObject> InitialTuning;

            /// <summary>Height of the primitive's centre above its base (spheres are 1 unit tall, cylinders 2).</summary>
            public float CentreHeight => Shape == PrimitiveType.Cylinder ? Scale.y : Scale.y / 2f;
        }

        public static readonly IngredientType[] Types =
        {
            new IngredientType
            {
                // 300 g, 2.5 cm steak: IngredientDefinition's defaults are tuned for this.
                Id = "Steak", Color = new Color(0.62f, 0.12f, 0.14f), Shape = PrimitiveType.Cylinder,
                Scale = new Vector3(0.13f, 0.0125f, 0.11f), CratePosition = new Vector3(-4.4f, 0f, -1.5f),
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

        public static ItemCatalog BuildCatalog()
        {
            var definitions = Types.Select(BuildDefinition).ToArray();

            var catalog = GreyboxAssets.EnsureAsset<ItemCatalog>(CatalogPath);
            var so = new SerializedObject(catalog);
            var list = so.FindProperty("_ingredients");
            list.arraySize = definitions.Length;
            for (var i = 0; i < definitions.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
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
            root.AddComponent<ItemView>();
            var body = CreateShape(type, "Body", root.transform);
            body.transform.localPosition = new Vector3(0f, type.CentreHeight, 0f);

            GreyboxAssets.EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/PF_Item_{type.Id}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
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
