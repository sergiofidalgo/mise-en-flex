using Game.Gameplay.Items;
using Game.Presentation;
using Game.Simulation.Items;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds the pan and plate prefabs: a primitive body plus a slot per item they can hold.
    /// Stylised, slightly oversized proportions so they read from the overhead camera.
    /// </summary>
    public static class GreyboxKitchenware
    {
        public const string PanTypeId = "Pan";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Items";

        private const float PanDiameter = 0.44f;
        private const float PanDepth = 0.04f;
        private static readonly Vector3 PanHandleScale = new Vector3(0.05f, 0.03f, 0.22f);
        // One piece of food per pan (decision 2026-11-02), in the middle.
        private static readonly Vector3[] PanSlots = { Vector3.zero };

        private const float PlateDiameter = 0.32f;
        private const float PlateThickness = 0.015f;
        private static readonly Vector3[] PlateSlots =
        {
            new Vector3(0f, 0f, 0.05f), new Vector3(-0.07f, 0f, -0.05f), new Vector3(0.07f, 0f, -0.05f),
        };

        public static readonly Color PanColor = new Color(0.30f, 0.30f, 0.33f);
        private static readonly Color PanHandleColor = new Color(0.16f, 0.11f, 0.08f);
        public static readonly Color PlateColor = new Color(0.95f, 0.95f, 0.93f);

        // Smoke: soft puffs that rise, spread and fade. Emission is driven at runtime by PanView.
        private const float SmokeLifetime = 1.4f;
        private const float SmokeSpeed = 0.35f;
        private const float SmokeMinSize = 0.12f;
        private const float SmokeMaxSize = 0.25f;
        private const float SmokeGrowth = 2.5f;
        private const int SmokeMaxParticles = 200;

        public static GameObject BuildPan()
        {
            var root = new GameObject($"PF_Item_{PanTypeId}");
            var body = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cylinder, "Body", root.transform, GreyboxAssets.EnsureMaterial("Pan", PanColor), keepCollider: false);
            body.transform.localScale = new Vector3(PanDiameter, PanDepth / 2f, PanDiameter); // cylinder is 2 units tall
            body.transform.localPosition = new Vector3(0f, PanDepth / 2f, 0f);

            // Handle pointing towards the camera (south), so the pan reads as a pan from above.
            var handle = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "Handle", root.transform, GreyboxAssets.EnsureMaterial("Pan", PanColor), keepCollider: false);
            handle.transform.localScale = PanHandleScale;
            handle.transform.localPosition = new Vector3(0f, PanDepth * 0.75f, -(PanDiameter + PanHandleScale.z) / 2f);

            // PanView tints the body with heat, so the handle gets its own colour with the model.
            PilotArt.TryUseMesh(body, "SM_Pan", "PanBody");
            if (PilotArt.TryUseMesh(handle, "SM_Pan", "PanHandle"))
                handle.GetComponent<Renderer>().sharedMaterial = GreyboxAssets.EnsureMaterial("PanHandle", PanHandleColor);

            var view = AddSlots(root, PanSlots, PanDepth);
            var smoke = CreateParticles(root.transform, "Smoke", PanDepth, PanDiameter * 0.3f,
                SmokeLifetime, SmokeSpeed, SmokeMinSize, SmokeMaxSize, SmokeGrowth, SmokeMaxParticles);

            var panView = root.AddComponent<PanView>();
            var so = new SerializedObject(panView);
            so.FindProperty("_itemView").objectReferenceValue = view;
            so.FindProperty("_body").objectReferenceValue = body.GetComponent<Renderer>();
            so.FindProperty("_smoke").objectReferenceValue = smoke;
            so.ApplyModifiedPropertiesWithoutUndo();

            return Save(root, PanTypeId);
        }

        /// <summary>
        /// A soft particle emitter that rises, spreads and fades, emitting nothing until a view
        /// sets its rate (smoke from pans, steam from hot food). Simulated in world space so a
        /// carried item leaves its trail behind.
        /// </summary>
        public static ParticleSystem CreateParticles(Transform parent, string name, float height, float radius,
            float lifetime, float speed, float minSize, float maxSize, float growth, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * height;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // emit upwards

            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // a carried pan leaves its smoke behind
            main.playOnAwake = true;
            main.loop = true;

            var emission = particles.emission;
            emission.rateOverTime = 0f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = radius;

            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var grow = particles.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, growth));

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = GreyboxAssets.EnsureParticleMaterial("Smoke");
            return particles;
        }

        // Knife: grey blade and dark handle, lying flat; root at its base.
        private static readonly Vector3 BladeScale = new Vector3(0.035f, 0.008f, 0.2f);
        private static readonly Vector3 HandleScale = new Vector3(0.035f, 0.02f, 0.1f);
        public static readonly Color TrayColor = new Color(0.32f, 0.34f, 0.38f);
        private static readonly Vector3 TrayScale = new Vector3(0.72f, 0.02f, 0.72f);
        private static readonly Vector3[] TraySlots =
        {
            new Vector3(-0.18f, 0f, -0.18f), new Vector3(0.18f, 0f, -0.18f),
            new Vector3(-0.18f, 0f, 0.18f), new Vector3(0.18f, 0f, 0.18f),
        };
        public static readonly Color ContainerColor = new Color(0.72f, 0.78f, 0.82f);
        private static readonly Vector3 ContainerScale = new Vector3(0.5f, 0.06f, 0.5f);
        private const float ContainerPileStep = 0.015f;
        private static readonly Color BladeColor = new Color(0.78f, 0.80f, 0.84f);
        private static readonly Color HandleColor = new Color(0.18f, 0.12f, 0.10f);

        public static GameObject BuildKnife()
        {
            var root = new GameObject($"PF_Item_{Knife.KnifeTypeId}");
            root.AddComponent<ItemView>();
            var blade = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "Blade", root.transform, GreyboxAssets.EnsureMaterial("KnifeBlade", BladeColor), keepCollider: false);
            blade.transform.localScale = BladeScale;
            blade.transform.localPosition = new Vector3(0f, BladeScale.y / 2f, BladeScale.z / 2f);
            var handle = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "Handle", root.transform, GreyboxAssets.EnsureMaterial("KnifeHandle", HandleColor), keepCollider: false);
            handle.transform.localScale = HandleScale;
            handle.transform.localPosition = new Vector3(0f, HandleScale.y / 2f, -HandleScale.z / 2f);
            return Save(root, Knife.KnifeTypeId);
        }

        public static GameObject BuildTray()
        {
            var root = new GameObject($"PF_Item_{Tray.TrayTypeId}");
            var body = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "Body", root.transform, GreyboxAssets.EnsureMaterial("Tray", TrayColor), keepCollider: false);
            body.transform.localScale = TrayScale;
            body.transform.localPosition = new Vector3(0f, TrayScale.y / 2f, 0f);
            AddSlots(root, TraySlots, TrayScale.y);
            return Save(root, Tray.TrayTypeId);
        }

        public static GameObject BuildContainer()
        {
            var root = new GameObject($"PF_Item_{FoodContainer.ContainerTypeId}");
            var body = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "Body", root.transform, GreyboxAssets.EnsureMaterial("Container", ContainerColor), keepCollider: false);
            body.transform.localScale = ContainerScale;
            body.transform.localPosition = new Vector3(0f, ContainerScale.y / 2f, 0f);

            // Portions pile up in the middle: a full container reads as a heap, not six neat items.
            var slots = new Vector3[6];
            for (var i = 0; i < slots.Length; i++)
                slots[i] = new Vector3(0f, i * ContainerPileStep, 0f);
            AddSlots(root, slots, ContainerScale.y);
            return Save(root, FoodContainer.ContainerTypeId);
        }

        public static GameObject BuildPlate()
        {
            var root = new GameObject($"PF_Item_{Plate.PlateTypeId}");
            var body = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cylinder, "Body", root.transform, GreyboxAssets.EnsureMaterial("Plate", PlateColor), keepCollider: false);
            body.transform.localScale = new Vector3(PlateDiameter, PlateThickness / 2f, PlateDiameter);
            body.transform.localPosition = new Vector3(0f, PlateThickness / 2f, 0f);

            var itemView = AddSlots(root, PlateSlots, PlateThickness);
            var view = new SerializedObject(root.AddComponent<PlateView>());
            view.FindProperty("_itemView").objectReferenceValue = itemView;
            view.FindProperty("_body").objectReferenceValue = body.GetComponent<Renderer>();
            view.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, Plate.PlateTypeId);
        }

        /// <summary>A short stack of plate discs, as a label for the plate stack station.</summary>
        /// <summary>A pile of plate discs from <paramref name="baseHeight"/> up; the view shows as many as there are plates.</summary>
        public static GameObject[] CreatePlatePile(Transform parent, float baseHeight, int count, Color color, string materialName, Vector3 offset)
        {
            var discs = new GameObject[count];
            for (var i = 0; i < count; i++)
            {
                var disc = GreyboxAssets.CreatePrimitive(
                    PrimitiveType.Cylinder, $"{materialName}_{i + 1}", parent, GreyboxAssets.EnsureMaterial(materialName, color), keepCollider: false);
                disc.transform.localScale = new Vector3(PlateDiameter, PlateThickness / 2f, PlateDiameter);
                disc.transform.localPosition = offset + new Vector3(0f, baseHeight + PlateThickness * (i + 0.5f) * 1.6f, 0f);
                discs[i] = disc;
            }
            return discs;
        }

        private static ItemView AddSlots(GameObject root, Vector3[] positions, float height)
        {
            var slots = new Transform[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                slots[i] = new GameObject($"Slot_{i + 1}").transform;
                slots[i].SetParent(root.transform, false);
                slots[i].localPosition = positions[i] + Vector3.up * height;
            }

            var view = root.AddComponent<ItemView>();
            var so = new SerializedObject(view);
            var list = so.FindProperty("_slotAnchors");
            list.arraySize = slots.Length;
            for (var i = 0; i < slots.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static GameObject Save(GameObject root, string typeId)
        {
            GreyboxAssets.EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/PF_Item_{typeId}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
