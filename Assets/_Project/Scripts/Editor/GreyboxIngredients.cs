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
            public string DisplayName; // null = Id
            public Color Color;
            public PrimitiveType Shape;
            public Vector3 Scale;

            // Layered food (a slab like steak) is built as a top surface over three edge bands,
            // so its doneness can be read from the side; anything else is a single shape.
            public bool Layered;

            // What the pass scores for this ingredient. Applied on every build; customer orders
            // replace TargetCore with the ordered level's target (decision 2026-11-02).
            public bool ScoreDoneness;
            public bool ScoreBrowning;
            public bool NeedsFlip;
            public bool ServedHot;
            public float TargetCore = 54.5f;
            public float RestTargetSeconds;

            // Cutting at the prep counter (also applied on every build).
            public string CutInto;
            public int CutsNeeded;
            public float ChopSecondsPerSweep = 0.7f;
            public float SweetSpotPosition = 85f;
            public float SweetSpotGood = 20f;
            public float SweetSpotOk = 40f;

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
                Scale = new Vector3(0.19f, 0.02f, 0.16f),
                Layered = true, ScoreDoneness = true, ScoreBrowning = true, NeedsFlip = true, ServedHot = true,
                TargetCore = 54.5f, RestTargetSeconds = 180f,
                // Order targets sit inside each band, so an ordered level scores perfect without
                // tipping into the next one.
                InitialTuning = so =>
                {
                    // Heat reaches the middle soon enough that well done comes before burnt, even at full heat.
                    so.FindProperty("_surfaceCoreConductance").floatValue = 1.2f;
                    so.FindProperty("_browningRate").floatValue = 1f / 14824f;
                    SetDoneness(so,
                    ("Rare", 45f, 50f), ("Medium", 55f, 60f), ("Well done", 65f, 70f));
                },
            },
            new IngredientType
            {
                Id = "Tomato", Color = new Color(0.90f, 0.18f, 0.12f), Shape = PrimitiveType.Sphere,
                Scale = Vector3.one * 0.28f,
                InitialTuning = so => SetVegetable(so, massKg: 0.12f),
            },
            new IngredientType
            {
                Id = "Onion", Color = new Color(0.93f, 0.84f, 0.62f), Shape = PrimitiveType.Sphere,
                Scale = Vector3.one * 0.26f, CutInto = "DicedOnion", CutsNeeded = 5, ChopSecondsPerSweep = 0.7f, SweetSpotPosition = 110f, SweetSpotGood = 14f, SweetSpotOk = 28f,
                InitialTuning = so => SetVegetable(so, massKg: 0.15f),
            },
            new IngredientType
            {
                // A portion of diced onion: small pieces heat through fast, soften around 80 °C core and
                // brown slowly. Prototyped: softened ~2.6, cooked ~4 in-game minutes at full heat.
                Id = "DicedOnion", DisplayName = "Onions", Color = new Color(0.96f, 0.90f, 0.70f), Shape = PrimitiveType.Sphere,
                Scale = new Vector3(0.18f, 0.05f, 0.18f),
                ScoreDoneness = true, ScoreBrowning = true, ServedHot = true, TargetCore = 92f,
                InitialTuning = so =>
                {
                    SetVegetable(so, massKg: 0.15f);
                    so.FindProperty("_surfaceFraction").floatValue = 0.3f;
                    so.FindProperty("_surfaceCoreConductance").floatValue = 2.5f;
                    so.FindProperty("_panContactConductance").floatValue = 7f;
                    so.FindProperty("_airConductance").floatValue = 0.6f;
                    so.FindProperty("_browningRate").floatValue = 1f / 14000f;
                    so.FindProperty("_freshnessLossPerHourAt20C").floatValue = 1f / 48f;
                    SetDoneness(so, ("Softened", 80f, 85f), ("Cooked", 92f, 92f));
                },
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

        /// <summary>A collider-less copy of the item's shape, e.g. as a sample on a storage bin.</summary>
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
            so.FindProperty("_displayName").stringValue = type.DisplayName ?? type.Id;
            so.FindProperty("_prefab").objectReferenceValue = BuildPrefab(type);
            if (isNew)
                type.InitialTuning?.Invoke(so);
            so.FindProperty("_scoreDoneness").boolValue = type.ScoreDoneness;
            so.FindProperty("_scoreBrowning").boolValue = type.ScoreBrowning;
            so.FindProperty("_needsFlip").boolValue = type.NeedsFlip;
            so.FindProperty("_servedHot").boolValue = type.ServedHot;
            so.FindProperty("_targetCoreTemperature").floatValue = type.TargetCore;
            so.FindProperty("_restTargetSeconds").floatValue = type.RestTargetSeconds;
            so.FindProperty("_cutIntoTypeId").stringValue = type.CutInto ?? string.Empty;
            so.FindProperty("_cutsNeeded").intValue = type.CutsNeeded;
            so.FindProperty("_chopSecondsPerSweep").floatValue = type.ChopSecondsPerSweep;
            so.FindProperty("_sweetSpotPosition").floatValue = type.SweetSpotPosition;
            so.FindProperty("_sweetSpotGood").floatValue = type.SweetSpotGood;
            so.FindProperty("_sweetSpotOk").floatValue = type.SweetSpotOk;
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

            if (type.ServedHot)
                AddSteam(root, itemView, type.Scale.y * 2f);

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

        // Steam: small, faint, slow wisps; SteamView drives the rate from the food's temperature.
        private static readonly Color SteamColor = new Color(1f, 1f, 1f, 0.35f);

        private static void AddSteam(GameObject root, ItemView itemView, float height)
        {
            var steam = GreyboxKitchenware.CreateParticles(root.transform, "Steam", height, 0.06f,
                lifetime: 1.1f, speed: 0.25f, minSize: 0.05f, maxSize: 0.1f, growth: 2.5f, maxParticles: 60);
            var main = steam.main;
            main.startColor = SteamColor;

            var view = root.AddComponent<SteamView>();
            var so = new SerializedObject(view);
            so.FindProperty("_itemView").objectReferenceValue = itemView;
            so.FindProperty("_steam").objectReferenceValue = steam;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        private static void SetDoneness(SerializedObject so, params (string Name, float MinPeakCore, float OrderTarget)[] levels)
        {
            var list = so.FindProperty("_donenessLevels");
            list.arraySize = levels.Length;
            for (var i = 0; i < levels.Length; i++)
            {
                var level = list.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("_name").stringValue = levels[i].Name;
                level.FindPropertyRelative("_minPeakCoreTemperature").floatValue = levels[i].MinPeakCore;
                level.FindPropertyRelative("_orderTargetCoreTemperature").floatValue = levels[i].OrderTarget;
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
