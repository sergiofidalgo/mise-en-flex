using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Renders the built kitchen from the game camera's angle: the whole kitchen, and close views of
    /// chosen stations, to check art where it actually stands without playing. Outside Play mode,
    /// every plate and sample a station can show is visible, so piles show full.
    /// Batch mode: -executeMethod Game.Editor.KitchenCapture.CaptureFromCommandLine -captureFolder &lt;folder&gt;
    /// </summary>
    public static class KitchenCapture
    {
        private const string ScenePath = "Assets/_Project/Scenes/Kitchen_Greybox.unity";
        private const int Width = 1920;
        private const int Height = 1080;
        private const float Pitch = 55f;            // the game camera's
        private const float FieldOfView = 45f;
        private const float CloseHeight = 2.6f;
        private const float PlayHeight = 6.5f;      // the closest the game camera comes
        private const float WholeKitchenHeight = 11f;

        private const int PlateRow = 7;
        private const float PlateSpacing = 0.4f;
        private const float PlateRowHeight = 1.6f;
        private static readonly Vector3 PlateRowStart = new Vector3(20f, 0f, 0f);

        // Stations to look at closely, by name in the scene.
        private static readonly string[] CloseUps = { "Hob_01", "Sink", "PlateStack", "WallCounter_01", "CuttingBoard", "Fridge", "DiningFloor", "Wall_West" };

        [MenuItem("Tools/Game/Art/Capture Kitchen")]
        private static void CaptureFromMenu()
        {
            var folder = EditorUtility.SaveFolderPanel("Save kitchen captures", "", "");
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

            var camera = new GameObject("Capture Camera").AddComponent<Camera>();
            camera.fieldOfView = FieldOfView;
            HouseStyle.SmoothEdges(camera);
            RenderLook.Backdrop(camera);
            // Anti-aliased like the game (4x MSAA in the PC pipeline asset).
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };

            // A throwaway first frame: shader variants not compiled yet come out black in it.
            Render(camera, target, new Vector3(0f, 0.9f, -1.5f), WholeKitchenHeight, null);
            Render(camera, target, new Vector3(0f, 0.9f, -1.5f), WholeKitchenHeight, Path.Combine(folder, "kitchen_whole.png"));
            Render(camera, target, new Vector3(0f, 0.9f, 0.3f), PlayHeight, Path.Combine(folder, "kitchen_play.png"));
            // Food sitting on a counter, at the size counters show it (only seen in play).
            var counterAnchor = GameObject.Find("WallCounter_01")?.transform.Find("ItemAnchor");
            if (counterAnchor != null)
            {
                Spawn("Assets/_Project/Prefabs/Items/PF_Item_Steak.prefab", new Vector3(-0.2f, 0f, 0f), counterAnchor);
                Spawn("Assets/_Project/Prefabs/Items/PF_Item_Container.prefab", new Vector3(0.2f, 0f, 0f), counterAnchor);
            }

            foreach (var name in CloseUps)
            {
                var station = GameObject.Find(name);
                if (station == null)
                    continue;
                Render(camera, target, station.transform.position + Vector3.up * 0.9f, CloseHeight,
                    Path.Combine(folder, $"close_{name}.png"));
            }

            // The sink as it looks while washing: the view only runs in Play mode, so turn its
            // water on and run its particles a while by hand.
            var wash = UnityEngine.Object.FindFirstObjectByType<Game.Presentation.SinkWashView>();
            if (wash != null)
            {
                var so = new SerializedObject(wash);
                ((Renderer)so.FindProperty("_stream").objectReferenceValue).enabled = true;
                foreach (var name in new[] { "_splash", "_bubbles" })
                {
                    var particles = (ParticleSystem)so.FindProperty(name).objectReferenceValue;
                    var emission = particles.emission;
                    emission.rateOverTime = name == "_bubbles" ? 5f : 30f;
                    particles.Simulate(1.5f, true, true);
                }
                // And the bin beside it, half full (BinFillView only runs in Play mode).
                var fill = UnityEngine.Object.FindFirstObjectByType<Game.Presentation.BinFillView>();
                if (fill != null)
                {
                    var trash = (Transform)new SerializedObject(fill).FindProperty("_trash").objectReferenceValue;
                    trash.GetComponent<Renderer>().enabled = true;
                    trash.localPosition = new Vector3(0f, 0.5f, 0f);
                    var flies = (ParticleSystem)new SerializedObject(fill).FindProperty("_flies").objectReferenceValue;
                    var buzz = flies.emission;
                    buzz.rateOverTime = 3f;
                    flies.Simulate(4f, true, true);
                }
                Render(camera, target, wash.transform.position + Vector3.up * 0.9f, CloseHeight, Path.Combine(folder, "close_Sink_washing.png"));
            }

            // The fridge just opened, fogging over (FridgeDoorView only runs in Play mode).
            var fridge = UnityEngine.Object.FindFirstObjectByType<Game.Presentation.FridgeDoorView>();
            if (fridge != null)
            {
                fridge.Show(1f);
                var mist = (ParticleSystem)new SerializedObject(fridge).FindProperty("_mist").objectReferenceValue;
                mist.Emit(32);
                mist.Simulate(0.6f, true, false);
                // Framed a little forward, where the chef stands at it.
                Render(camera, target, fridge.transform.position + new Vector3(0f, 0.5f, -0.6f), CloseHeight, Path.Combine(folder, "close_Fridge_open.png"));
            }

            // A row of plates outside the kitchen: clean, half washed, then dirty with different messes.
            var platePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/PF_Item_Plate.prefab");
            for (var i = 0; i < PlateRow; i++)
            {
                var plate = UnityEngine.Object.Instantiate(platePrefab, PlateRowStart + Vector3.right * (i * PlateSpacing), Quaternion.identity);
                var body = plate.transform.Find("Body").GetComponent<Renderer>();
                var block = new MaterialPropertyBlock();
                Game.Presentation.PlateGrime.Set(block, i == 0 ? 0f : i == 1 ? 0.5f : 1f, i * 0.137f);
                body.SetPropertyBlock(block);
            }
            Render(camera, target, PlateRowStart + new Vector3((PlateRow - 1) * PlateSpacing / 2f, 0f, 0f), PlateRowHeight,
                Path.Combine(folder, "plates_grime.png"));

            // Props, for the knife, knife block, onion and what a chef carries (only seen in play).
            var props = new Vector3(30f, 0f, 0f);
            var rack = Spawn("Assets/_Project/Prefabs/Furniture/PF_KnifeRack.prefab", props);
            if (rack != null)
            {
                foreach (Transform child in rack.transform)
                    if (child.name.StartsWith("Place_") && child.name != "Place_4") // one left empty, to compare
                        Spawn("Assets/_Project/Prefabs/Items/PF_Item_Knife.prefab", Vector3.zero, child);
            }
            Spawn("Assets/_Project/Prefabs/Items/PF_Item_Knife.prefab", props + new Vector3(1.1f, 0f, 0.2f));
            Spawn("Assets/_Project/Prefabs/Items/PF_Item_Onion.prefab", props + new Vector3(1.1f, 0f, -0.4f));
            var chef = Spawn("Assets/_Project/Prefabs/PF_Player.prefab", props + new Vector3(-1.2f, 0f, -0.3f));
            if (chef != null)
            {
                chef.transform.rotation = Quaternion.Euler(0f, 120f, 0f); // its right hip, with the knife sheath, to the camera
                Spawn("Assets/_Project/Prefabs/Items/PF_Item_Pan.prefab", Vector3.zero, chef.transform.Find("CarryAnchor"));
                Spawn("Assets/_Project/Prefabs/Items/PF_Item_Knife.prefab", Vector3.zero, chef.transform.Find("ToolAnchor"));
                var arms = chef.GetComponent<Game.Presentation.ChefArmsView>();
                if (arms != null)
                    arms.PreviewHolding(pan: true);
            }
            Render(camera, target, props + new Vector3(0f, 0.9f, -0.2f), CloseHeight + 0.6f, Path.Combine(folder, "props.png"));

            // A chef at the cutting board, knife out (the arms only move in play).
            var board = props + new Vector3(0f, 0f, 5f);
            var cuttingBoard = Spawn("Assets/_Project/Prefabs/Furniture/PF_CuttingBoard.prefab", board + new Vector3(0.75f, 0f, 0f));
            // Onion rings part way through dicing: three rings pushed aside and some dice (only seen in play).
            var progress = cuttingBoard != null ? cuttingBoard.transform.Find("CutProgress") : null;
            if (progress != null)
                foreach (Transform piece in progress)
                    if ((piece.name.StartsWith("Ring_") && int.Parse(piece.name.Substring(5)) < 3)
                        || (piece.name.StartsWith("Die_") && int.Parse(piece.name.Substring(4)) < 4))
                        piece.gameObject.SetActive(true);
            // The finished cuts beside it: rings, and dice.
            Spawn("Assets/_Project/Prefabs/Items/PF_Item_OnionRings.prefab", board + new Vector3(1.55f, 0.9f, 0.55f));
            Spawn("Assets/_Project/Prefabs/Items/PF_Item_DicedOnion.prefab", board + new Vector3(1.55f, 0.9f, 0.05f));
            // An onion just halved, beside them.
            if (progress != null)
            {
                var halved = UnityEngine.Object.Instantiate(progress.gameObject, board + new Vector3(1.55f, 0.9f, -0.5f), Quaternion.identity);
                foreach (Transform piece in halved.transform)
                    piece.gameObject.SetActive(piece.name.StartsWith("Half_"));
            }
            var cook = Spawn("Assets/_Project/Prefabs/PF_Player.prefab", board);
            if (cook != null)
            {
                cook.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Spawn("Assets/_Project/Prefabs/Items/PF_Item_Knife.prefab", Vector3.zero, cook.transform.Find("ToolAnchor"));
                var cookArms = cook.GetComponent<Game.Presentation.ChefArmsView>();
                if (cookArms != null)
                    cookArms.PreviewHolding(pan: false, cutting: true);
            }
            Render(camera, target, board + new Vector3(0.4f, 0.9f, -0.2f), CloseHeight, Path.Combine(folder, "cutting.png"));

            target.Release();
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            Debug.Log($"[KitchenCapture] Saved to {folder}");
        }

        private static GameObject Spawn(string path, Vector3 position, Transform parent = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return null;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null)
            {
                instance.transform.SetParent(parent, false);
                instance.transform.localPosition = position;
                instance.transform.localRotation = Quaternion.identity;
            }
            else
                instance.transform.position = position;
            return instance;
        }

        // Pitched like the game camera, looking at a point from the given height.
        private static void Render(Camera camera, RenderTexture target, Vector3 lookAt, float height, string path)
        {
            var back = (height - lookAt.y) / Mathf.Tan(Pitch * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(new Vector3(lookAt.x, height, lookAt.z - back), Quaternion.Euler(Pitch, 0f, 0f));

            camera.targetTexture = target;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
                camera.Render();
            camera.targetTexture = null;

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            if (path != null)
                File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
