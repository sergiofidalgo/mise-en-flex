using System.Collections.Generic;
using Game.Presentation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Models for plates, the plate stack's rack, the sink and the cutting board, swapped in by the
    /// builders when the models exist. Only meshes change: footprints, anchors, slots and colliders
    /// stay as the greybox builders made them, so gameplay is untouched.
    /// </summary>
    public static class KitchenwareArt
    {
        private const string PlateModel = "SM_Plate";
        private const string RackModel = "SM_PlateRack";
        private const string SinkModel = "SM_Sink";
        private const string BoardModel = "SM_Board";

        // Must match PLATE_RADIUS in blender/build_pilot_models.py; the plate's origin is its base.
        private const float PlateRadius = 0.16f;
        private const float PlateHeight = 0.042f;
        // Plates in the rack stand on edge, one behind the other, on rails this high. Must match
        // RACK_SPACING and RACK_WIRE in blender/build_pilot_models.py.
        private const float RackSpacing = 0.08f;
        private const float RackRailHeight = 0.016f;
        // A loose pile is nudged and turned a little per plate, so it reads as dirty dishes.
        private const float MessyNudge = 0.03f;

        /// <summary>The sink's basin floor, where dirty plates sit; must match build_sink().</summary>
        public const float SinkBasinFloor = 0.68f;
        /// <summary>The basin's centre across the sink, in the game's x: one big basin, in the middle.</summary>
        public const float SinkBasinX = 0f;

        // A soft blue line round a white plate, and a light steel rack (decision 2026-11-08).
        // A stronger blue rim and a dark steel rack, so plates and the rack read at a glance
        // (the user's note, 2026-11-08).
        private static readonly Color RimColor = new Color(0.42f, 0.62f, 0.9f);
        // The same steel as the tray rack's frame (PanArt), not black (the user's note, 2026-11-08).
        private static readonly Color RackSteel = new Color(0.58f, 0.6f, 0.64f);
        // The plate rack and its plates show bigger, standing taller over the counter.
        private const float RackSize = 1.25f;
        private static readonly Color SteelColor = new Color(0.74f, 0.76f, 0.8f);
        private static readonly Color SinkRimSteel = new Color(0.88f, 0.9f, 0.93f);
        private static readonly Color SinkBasinGrey = new Color(0.48f, 0.5f, 0.55f);
        // The basin's walls darker than its floor and much darker than the rim, so the drop reads.
        private static readonly Color SinkBasinWallGrey = new Color(0.33f, 0.35f, 0.39f);
        private static readonly Color SinkDrainDark = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color SinkChrome = new Color(0.93f, 0.95f, 0.98f);

        // The tap's nozzle, from the sink's model: water falls from here to the basin's floor.
        // Must match build_sink() in blender/build_pilot_models.py (Blender y -0.2 is the game's z 0.2).
        private const float NozzleHeight = 1.08f;
        private const float NozzleBack = 0.2f;
        private const float StreamWidth = 0.028f;
        // The water in the basin: about half way up its 0.23 m, over the plate on its floor.
        // The basin's opening, from build_sink(): 0.76 by 0.64 m.
        private const float WaterLevel = 0.79f;
        private const float BasinWidth = 0.74f;
        private const float BasinDepth = 0.62f;
        private const string BinModel = "SM_Bin";
        private static readonly Color BinGrey = new Color(0.45f, 0.47f, 0.5f);
        private static readonly Color BinBagBlack = new Color(0.12f, 0.12f, 0.13f);
        private static readonly Color BinTrashColor = new Color(0.5f, 0.45f, 0.38f);

        /// <summary>
        /// Shows the bin's fill (the user's note, 2026-11-08): its rubbish mound, made by DressBin,
        /// rises with what's thrown away. Only with the bin's model.
        /// </summary>
        public static void AddBinFill(Game.Gameplay.Stations.BinStation bin, Transform body)
        {
            var trash = body.Find("Trash");
            if (trash == null)
                return;
            trash.GetComponent<Renderer>().enabled = false; // empty to start

            // Flies buzzing round it once it's getting full (the user's note, 2026-11-08): dark
            // specks darting about over the rim.
            var flies = GreyboxKitchenware.CreateParticles(body, "Flies", 0.92f, 0.14f, lifetime: 3f, speed: 0f,
                minSize: 0.05f, maxSize: 0.065f, growth: 1f, maxParticles: 24);
            var fliesMain = flies.main;
            fliesMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            fliesMain.startColor = new Color(0.08f, 0.08f, 0.09f, 1f);
            var fliesShape = flies.shape;
            fliesShape.shapeType = ParticleSystemShapeType.Sphere;
            fliesShape.radius = 0.14f;
            // Circling the bin, with a jittery dart: they stay round it rather than wander off.
            var circle = flies.velocityOverLifetime;
            circle.enabled = true;
            circle.space = ParticleSystemSimulationSpace.Local;
            circle.orbitalY = new ParticleSystem.MinMaxCurve(3f, 6f);
            var buzz = flies.noise;
            buzz.enabled = true;
            buzz.strength = 0.1f;
            buzz.frequency = 3f;
            buzz.scrollSpeed = 2f;
            var steady = flies.colorOverLifetime;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.05f), new GradientAlphaKey(1f, 0.95f), new GradientAlphaKey(0f, 1f) });
            steady.color = fade;


            var view = new SerializedObject(bin.gameObject.AddComponent<BinFillView>());
            view.FindProperty("_bin").objectReferenceValue = bin;
            view.FindProperty("_trash").objectReferenceValue = trash;
            view.FindProperty("_flies").objectReferenceValue = flies;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        public enum PlateLayout
        {
            /// <summary>Standing in a wire rack, facing the camera, from the back to the front.</summary>
            Rack,
            /// <summary>A neat flat stack.</summary>
            Stack,
            /// <summary>A flat stack, each plate nudged and turned a little.</summary>
            Messy,
        }

        public static bool HasPlateModel => PilotArt.FindMesh(PlateModel, "PlateBody") != null;

        /// <summary>
        /// Plates for a pile view (PlatePileView shows the first <c>n</c>), so the last one made is
        /// the first to go. Falls back to the greybox discs without the plate model.
        /// </summary>
        public static GameObject[] CreatePlates(Transform parent, float baseHeight, int count, Color color, string materialName,
            Vector3 offset, PlateLayout layout, bool dirty = false)
        {
            if (!HasPlateModel)
                return GreyboxKitchenware.CreatePlatePile(parent, baseHeight, count, color, materialName, offset);

            var material = EnsurePlateMaterial();
            if (layout == PlateLayout.Rack)
            {
                var rack = CreatePart(parent, $"{materialName}Rack", RackModel, "PlateRack",
                    StyleTest.EnsureToonMaterial("PlateRack", RackSteel));
                rack.transform.localPosition = offset + Vector3.up * baseHeight;
                rack.transform.localScale = Vector3.one * RackSize;
            }

            var plates = new GameObject[count];
            for (var i = 0; i < count; i++)
            {
                var plate = CreatePlate(parent, $"{materialName}_{i + 1}", material);
                if (dirty)
                {
                    // A new mess each time it's shown.
                    var grime = new SerializedObject(plate.AddComponent<PlateGrime>());
                    grime.FindProperty("_body").objectReferenceValue = plate.GetComponent<Renderer>();
                    grime.ApplyModifiedPropertiesWithoutUndo();
                }
                switch (layout)
                {
                    case PlateLayout.Rack:
                        // Stood on its edge facing the camera: its base turns towards the camera.
                        plate.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                        plate.transform.localScale *= RackSize;
                        plate.transform.localPosition = offset + new Vector3(0f, baseHeight + (RackRailHeight + PlateRadius) * RackSize,
                            ((count - 1) / 2f - i) * RackSpacing * RackSize);
                        break;
                    case PlateLayout.Messy:
                        plate.transform.localRotation = Quaternion.Euler(0f, Noise(i, 1) * 360f, 0f);
                        plate.transform.localPosition = offset + new Vector3((Noise(i, 2) - 0.5f) * 2f * MessyNudge,
                            baseHeight + i * PlateHeight, (Noise(i, 3) - 0.5f) * 2f * MessyNudge);
                        break;
                    default:
                        plate.transform.localPosition = offset + Vector3.up * (baseHeight + i * PlateHeight);
                        break;
                }
                plates[i] = plate;
            }
            return plates;
        }

        /// <summary>Gives a carried plate's body the plate model and its gold rim.</summary>
        public static void DressPlateItem(GameObject body)
        {
            if (!PilotArt.TryUseMesh(body, PlateModel, "PlateBody"))
                return;
            body.GetComponent<Renderer>().sharedMaterial = EnsurePlateMaterial();
            AddRim(body.transform);
        }

        /// <summary>
        /// The sink's steel top, rolled rim, basin, drain and tap on its cabinet (CabinetArt,
        /// underSink), in their own tones so its shape reads (the user's note, 2026-11-08): parts
        /// under the station's body, so they shake and light up with it. Empty without the model.
        /// </summary>
        public static List<Renderer> DressSink(Transform body)
        {
            var parts = new List<Renderer>();
            Add("SinkTop", "SinkBody", SteelColor);
            if (parts.Count == 0)
                return parts;
            Add("Rim", "SinkRim", SinkRimSteel);
            Add("Basin", "SinkBasin", SinkBasinGrey);
            Add("BasinWall", "SinkBasinWall", SinkBasinWallGrey);
            Add("Drain", "SinkDrain", SinkDrainDark);
            Add("Tap", "SinkTap", SinkChrome);
            return parts;

            void Add(string name, string mesh, Color color)
            {
                var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, body,
                    StyleTest.EnsureToonMaterial(name == "SinkTop" ? "SinkSteel" : $"Sink{name}", color), keepCollider: false);
                if (PilotArt.TryUseMesh(part, SinkModel, mesh))
                    parts.Add(part.GetComponent<Renderer>());
                else
                    Object.DestroyImmediate(part);
            }
        }

        /// <summary>
        /// The water that runs while the sink is in use (the user's note, 2026-11-08): a stream from
        /// the tap's nozzle to the basin, splashes where it lands and soap bubbles round the plate,
        /// all under the sink, with the view that runs them. Only with the sink's model.
        /// </summary>
        public static void AddWashEffects(Game.Gameplay.Stations.SinkStation sink)
        {
            // Always half-full of water (the user's note, 2026-11-08): a see-through surface across
            // the basin, so its depth reads, with the plate being washed visible beneath it.
            var water = GreyboxAssets.CreatePrimitive(PrimitiveType.Quad, "Water", sink.transform,
                PilotArt.EnsureSinkWaterMaterial(), keepCollider: false);
            water.transform.localPosition = new Vector3(0f, WaterLevel, 0f);
            water.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            water.transform.localScale = new Vector3(BasinWidth, BasinDepth, 1f);
            water.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var stream = GreyboxAssets.CreatePrimitive(PrimitiveType.Cylinder, "WaterStream", sink.transform,
                PilotArt.EnsureWaterMaterial(), keepCollider: false);
            var length = NozzleHeight - WaterLevel;
            stream.transform.localPosition = new Vector3(0f, (NozzleHeight + WaterLevel) / 2f, NozzleBack);
            stream.transform.localScale = new Vector3(StreamWidth, length / 2f, StreamWidth); // a cylinder is 2 tall
            var streamRenderer = stream.GetComponent<Renderer>();
            streamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            streamRenderer.enabled = false; // runs only while washing (SinkWashView)

            // Splashes and bubbles are meant to be seen and enjoyed (the user's note, 2026-11-08):
            // chunky droplets thrown well up, and big soap bubbles floating out of the sink.
            var splash = GreyboxKitchenware.CreateParticles(sink.transform, "Splash", WaterLevel, 0.04f,
                lifetime: 0.5f, speed: 1.3f, minSize: 0.03f, maxSize: 0.055f, growth: 0.6f, maxParticles: 120);
            splash.transform.localPosition = new Vector3(0f, WaterLevel, NozzleBack);
            var splashMain = splash.main;
            splashMain.gravityModifier = 2.5f;
            splashMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            splashMain.startColor = new Color(0.8f, 0.92f, 1f, 0.9f);
            var splashShape = splash.shape;
            splashShape.angle = 60f;

            // Soap bubbles: shiny rings in pastel tints, a few at a time, drifting off the water in
            // every direction, towards the chef too, wobbling, swelling a little and popping (the
            // user's notes, 2026-11-08: big, but few, so present but subtle).
            var bubbles = GreyboxKitchenware.CreateParticles(sink.transform, "Bubbles", WaterLevel + 0.005f, 0.2f,
                lifetime: 2.2f, speed: 0.2f, minSize: 0.1f, maxSize: 0.2f, growth: 1f, maxParticles: 24);
            var spread = bubbles.shape;
            spread.shapeType = ParticleSystemShapeType.Cone;
            spread.angle = 70f; // up off the water and out every way
            spread.radius = 0.2f;
            var bubblesMain = bubbles.main;
            bubblesMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            bubblesMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.95f, 1f), new Color(0.8f, 0.95f, 1f, 1f));
            var bubbleNoise = bubbles.noise;
            bubbleNoise.enabled = true;
            bubbleNoise.strength = 0.18f;
            bubbleNoise.frequency = 1.2f;
            var pop = bubbles.sizeOverLifetime;
            pop.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.8f, 1f), new Keyframe(0.9f, 1.25f), new Keyframe(1f, 0f)));
            var stay = bubbles.colorOverLifetime;
            var visible = new Gradient();
            visible.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.9f), new GradientAlphaKey(0f, 1f) });
            stay.color = visible;
            bubbles.GetComponent<ParticleSystemRenderer>().sharedMaterial = PilotArt.EnsureBubbleMaterial();

            var view = new SerializedObject(sink.gameObject.AddComponent<SinkWashView>());
            view.FindProperty("_sink").objectReferenceValue = sink;
            view.FindProperty("_stream").objectReferenceValue = streamRenderer;
            view.FindProperty("_splash").objectReferenceValue = splash;
            view.FindProperty("_bubbles").objectReferenceValue = bubbles;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The round bin's model on the bin station's body, grey with a black bag over its rim.
        /// Returns its other parts' renderers, for the station's highlight; empty without the model.
        /// </summary>
        public static List<Renderer> DressBin(Transform body)
        {
            var parts = new List<Renderer>();
            if (!PilotArt.TryUseMesh(body.gameObject, BinModel, "BinBody"))
                return parts;
            body.GetComponent<Renderer>().sharedMaterial = StyleTest.EnsureToonMaterial("BinBody", BinGrey);
            AddBinPart("Bag", "BinBag", StyleTest.EnsureToonMaterial("BinBag", BinBagBlack));
            AddBinPart("Trash", "BinTrash", StyleTest.EnsureToonMaterial("BinTrash", BinTrashColor));
            return parts;

            void AddBinPart(string name, string mesh, Material material)
            {
                var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, body, material, keepCollider: false);
                if (PilotArt.TryUseMesh(part, BinModel, mesh))
                    parts.Add(part.GetComponent<Renderer>());
                else
                    Object.DestroyImmediate(part);
            }
        }

        /// <summary>
        /// The bamboo board's model, on the board's own object, which stays where it is so its
        /// highlight and shake keep working. The model's base sits on the counter.
        /// </summary>
        public static void DressBoard(GameObject board, float counterHeight)
        {
            if (!PilotArt.TryUseMesh(board, BoardModel, "BoardBody"))
                return;
            board.transform.localPosition = Vector3.up * counterHeight;
            board.GetComponent<Renderer>().sharedMaterial = StyleTest.BoardMaterial(); // wood grain, toon shaded
        }

        // The dirty plate in the sink (the user's notes, 2026-11-08): it looks like the dirty plate that
        // was carried in, the plate's own grime rather than separate scraps, and lies nearly flat just
        // under the water, its far edge breaking the surface. Propped up with scraps on it, it read as a
        // speaker cone.
        private const float SinkPlateTip = 12f;
        private const float SinkPlateSize = 1.2f;
        private static readonly Vector3 SinkPlateSpot = new Vector3(0f, 0.765f, 0.03f);

        /// <summary>
        /// Lays the sink's dirty plate in the basin, tipped a little towards the tap, wearing the
        /// dirty plate's grime even in the Editor (washing still fades it, via <see cref="Game.Presentation.PlateGrime"/>).
        /// </summary>
        public static void LeanInSink(GameObject plate)
        {
            plate.transform.localPosition = SinkPlateSpot;
            plate.transform.localRotation = Quaternion.Euler(SinkPlateTip, 0f, 0f);
            plate.transform.localScale *= SinkPlateSize;
            if (plate.TryGetComponent<Renderer>(out var body))
                body.sharedMaterial = EnsureDirtyPlateMaterial();

            // A different landing each time a dirty plate goes in (the user's note, 2026-11-08).
            var shuffle = new SerializedObject(plate.AddComponent<Game.Presentation.RestPoseShuffle>());
            shuffle.FindProperty("_basePosition").vector3Value = SinkPlateSpot;
            shuffle.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The plate material with its grime on (MAT_Plate_Dirty): a dirty plate shown by a station.</summary>
        public static Material EnsureDirtyPlateMaterial()
        {
            const string path = "Assets/_Project/Art/Materials/MAT_Plate_Dirty.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(EnsurePlateMaterial());
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_Dirt", 1f);
            material.SetFloat("_Seed", 0.37f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The plate shader's material (MAT_Plate): clean, until a view makes it dirty.</summary>
        public static Material EnsurePlateMaterial()
        {
            const string path = "Assets/_Project/Art/Materials/MAT_Plate.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Game/Kitchen/Plate"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject CreatePlate(Transform parent, string name, Material material)
        {
            var plate = CreatePart(parent, name, PlateModel, "PlateBody", material);
            AddRim(plate.transform);
            return plate;
        }

        private static void AddRim(Transform plate) =>
            CreatePart(plate, "Rim", PlateModel, "PlateRim", StyleTest.EnsureToonMaterial("PlateRim", RimColor));

        private static GameObject CreatePart(Transform parent, string name, string model, string mesh, Material material)
        {
            var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cylinder, name, parent, material, keepCollider: false);
            PilotArt.TryUseMesh(part, model, mesh);
            return part;
        }

        // A repeatable 0..1 per plate and channel, so the messy pile looks the same every build.
        private static float Noise(int index, int channel)
        {
            unchecked
            {
                var hash = (uint)(index * 73856093) ^ (uint)(channel * 19349663);
                hash *= 2654435761u;
                return (hash >> 8) / 16777216f;
            }
        }
    }
}
