using System;
using System.IO;
using System.Linq;
using Game.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Renders the v9.2 characters (concept 2026-11-09) where they'll be judged: in the built
    /// kitchen, from the game camera's angle. Four players in their colours carrying plates (facing
    /// towards and away from the camera), a wide build pressed against the island, a full afro
    /// against the back counter, the dishwasher, every customer look in the queue, and a close
    /// lineup of all of them. Nothing is saved to the scene.
    /// Batch mode: -executeMethod Game.Editor.CharacterCapture.CaptureFromCommandLine -captureFolder &lt;folder&gt;
    /// </summary>
    public static class CharacterCapture
    {
        private const string ScenePath = "Assets/_Project/Scenes/Kitchen_Greybox.unity";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/PF_Player.prefab";
        private const string PlatePath ="Assets/_Project/Prefabs/Items/PF_Item_Plate.prefab";
        private const string SteakPath = "Assets/_Project/Prefabs/Items/PF_Item_Steak.prefab";
        private const string CoopSettingsPath = "Assets/_Project/Data/Settings/CoopSettings.asset";
        private const int Width = 1920;
        private const int Height = 1080;
        private const float Pitch = 55f;          // the game camera's
        private const float FieldOfView = 45f;
        private const float PlayHeight = 6.5f;    // the closest the game camera comes
        private const float WideHeight = 11f;     // the whole kitchen
        private const float CloseHeight = 3.2f;
        private const float BodyRadius = 0.35f;   // the player's capsule (GreyboxKitchenBuilder.CollisionRadius)

        // Where a chef carries things, and how much bigger they show (GreyboxKitchenBuilder, ItemSizes).
        private static readonly Vector3 CarryAnchor = new Vector3(0f, 1.05f, 0.5f);
        private static readonly Vector3 LineupStart = new Vector3(40f, 0f, 0f);
        private const float LineupSpacing = 0.95f;

        [MenuItem("Tools/Game/Art/Capture Characters")]
        private static void CaptureFromMenu()
        {
            var folder = EditorUtility.SaveFolderPanel("Save character captures", "", "");
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
            if (!CharacterArt.Available)
                throw new InvalidOperationException("Export SM_Characters.fbx from Blender first.");
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var coop = AssetDatabase.LoadAssetAtPath<CoopSettings>(CoopSettingsPath);

            var players = PlayerLooks();
            var island = Bounds("Zone_Island");
            var centre = island.center;

            // Players in the kitchen: two carrying plates south of the island, one facing the camera
            // and one facing away; one at the back counters; one against the island's far side.
            Person(players[0], coop.GetPlayerColor(0), new Vector3(centre.x - 1.6f, 0f, island.min.z - 1.1f), 0f, carry: true);
            Person(players[1], coop.GetPlayerColor(1), new Vector3(centre.x + 0.2f, 0f, island.min.z - 1.1f), 180f, carry: true);
            Person(players[2], coop.GetPlayerColor(2), new Vector3(centre.x + 1.8f, 0f, island.max.z + BodyRadius), 180f, carry: true);
            // A wide build tight against the island, where its arms meet the counter.
            var wide = CharacterArt.PlayerChef();
            wide.Build = 1.15f;
            Person(wide, coop.GetPlayerColor(3), new Vector3(centre.x - 1.0f, 0f, island.max.z + BodyRadius), 180f, carry: false);
            // The fullest hair against the back counters, where the 2.8 m wall is behind it.
            var back = Bounds("Zone_WallCounter");
            Person(players[3], coop.GetPlayerColor(3), new Vector3(back.center.x, 0f, back.min.z - BodyRadius), 0f, carry: false);

            var sink = GameObject.Find("Sink");
            if (sink != null)
            {
                var at = sink.transform.position - sink.transform.forward * 0.8f;
                Person(CharacterArt.Dishwasher(), Color.white, new Vector3(at.x, 0f, at.z), sink.transform.eulerAngles.y, carry: false);
            }

            // The real player prefab bent fully into the shelf and the chest fridge (StorageUseView),
            // where a bigger head on a lower waist swings furthest forward.
            var bends = new System.Collections.Generic.List<Vector3>();
            var benders = new System.Collections.Generic.List<GameObject>();
            foreach (var name in new[] { "Shelf", "Fridge" })
            {
                var station = GameObject.Find(name);
                if (station == null)
                    continue;
                var chef = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
                var stand = station.transform.position + Vector3.back * (0.5f + BodyRadius);
                chef.transform.SetPositionAndRotation(new Vector3(stand.x, 0f, stand.z), Quaternion.identity);
                chef.GetComponent<Game.Presentation.StorageUseView>()?.Show(1f);
                if (name == "Fridge")
                    station.GetComponent<Game.Presentation.FridgeDoorView>()?.Show(1f);
                bends.Add(station.transform.position);
                benders.Add(chef);
            }

            // Customers waiting in the queue, one of each look.
            var looks = CharacterArt.CustomerLooks();
            for (var i = 0; i < looks.Count; i++)
            {
                var spot = GameObject.Find($"QueueSpot_{i + 1}");
                if (spot != null)
                    Person(looks[i], Color.white, spot.transform.position, 180f + (i % 3 - 1) * 25f, carry: false);
            }

            // The lineup outside the kitchen: players, a hat-off player, the dishwasher, then customers.
            var lineup = players.Select((look, i) => (look, coop.GetPlayerColor(i % 4))).ToList();
            lineup.Add((wide, coop.GetPlayerColor(3)));
            lineup.Add((CharacterArt.Dishwasher(), Color.white));
            lineup.AddRange(looks.Select(look => (look, Color.white)));
            for (var i = 0; i < lineup.Count; i++)
                Person(lineup[i].look, lineup[i].Item2, LineupStart + Vector3.right * (i * LineupSpacing), 180f, carry: false);
            var lineupMiddle = LineupStart + Vector3.right * ((lineup.Count - 1) * LineupSpacing / 2f);

            // The carry test: one chef with a plate in each of eight directions, at the game camera's
            // closest framing, to see where the big head and toque hide what's carried.
            var ring = new Vector3(60f, 0f, 0f);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * 45f;
                var offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 1.6f;
                Person(players[0], coop.GetPlayerColor(0), ring + offset, angle, carry: true);
            }

            var camera = new GameObject("Capture Camera").AddComponent<Camera>();
            camera.fieldOfView = FieldOfView;
            HouseStyle.SmoothEdges(camera);
            RenderLook.Backdrop(camera);
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };

            Render(camera, target, new Vector3(0f, 0.9f, 0.3f), PlayHeight, null); // compiles shaders
            Render(camera, target, new Vector3(0f, 0.9f, 0.3f), PlayHeight, Path.Combine(folder, "kitchen_play.png"));
            Render(camera, target, new Vector3(0f, 0.9f, -1.5f), WideHeight, Path.Combine(folder, "kitchen_whole.png"));
            Render(camera, target, centre + new Vector3(0f, 0f, -0.6f), CloseHeight + 1f, Path.Combine(folder, "kitchen_island.png"));
            Render(camera, target, new Vector3(back.center.x, 0.9f, back.min.z - 0.4f), CloseHeight, Path.Combine(folder, "kitchen_backcounter.png"));
            var queue = GameObject.Find("QueueSpot_1");
            if (queue != null)
                Render(camera, target, queue.transform.position + new Vector3(0.4f, 0.9f, 0.6f), CloseHeight + 1.2f, Path.Combine(folder, "dining_queue.png"));
            Render(camera, target, lineupMiddle + new Vector3(0f, 0.9f, 0f), PlayHeight, Path.Combine(folder, "lineup_play.png"));
            Render(camera, target, lineupMiddle + new Vector3(-3.3f, 0.9f, 0f), CloseHeight, Path.Combine(folder, "lineup_close_left.png"));
            Render(camera, target, lineupMiddle + new Vector3(3.3f, 0.9f, 0f), CloseHeight, Path.Combine(folder, "lineup_close_right.png"));
            RenderFront(camera, target, lineupMiddle, Path.Combine(folder, "lineup_front.png"));
            camera.transform.SetPositionAndRotation(LineupStart + new Vector3(0.5f, 1.0f, -3.4f), Quaternion.Euler(4f, 0f, 0f));
            Draw(camera, target, Path.Combine(folder, "chef_front_close.png"));
            Render(camera, target, ring + new Vector3(0f, 0.9f, 0f), PlayHeight, Path.Combine(folder, "carry_facings.png"));
            for (var i = 0; i < bends.Count; i++)
                Render(camera, target, bends[i] + new Vector3(0f, 0.9f, -0.5f), CloseHeight, Path.Combine(folder, $"bend_{i}.png"));
            // And from the side, alone, where clipping into the station shows.
            for (var i = 0; i < bends.Count; i++)
            {
                for (var j = 0; j < benders.Count; j++)
                    benders[j].SetActive(j == i);
                camera.transform.SetPositionAndRotation(bends[i] + new Vector3(2.4f, 1.2f, -0.6f), Quaternion.Euler(8f, -90f, 0f));
                Draw(camera, target, Path.Combine(folder, $"bend_{i}_side.png"));
            }

            target.Release();
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            Debug.Log($"[CharacterCapture] Saved to {folder}");
        }

        // The four players' starting choices for this test: toque, cap, bandana, and hat off.
        private static CharacterArt.Look[] PlayerLooks()
        {
            var toque = CharacterArt.PlayerChef();
            var cap = CharacterArt.PlayerChef();
            cap.Hat = CharacterArt.HatKind.Cap;
            cap.HatColor = new Color(0.97f, 0.97f, 0.95f);
            cap.Skin = new Color(0.45f, 0.29f, 0.2f);
            cap.Hair = CharacterArt.DarkHair;
            var horns = CharacterArt.PlayerChef();
            horns.Hat = CharacterArt.HatKind.Horns;
            horns.HatColor = new Color(0.72f, 0.14f, 0.12f);
            horns.Hair = new Color(0.78f, 0.36f, 0.14f);
            horns.Glasses = true;
            horns.Moustache = true;
            var bare = CharacterArt.PlayerChef();
            bare.Hat = CharacterArt.HatKind.Headband;
            bare.HairStyle = CharacterArt.HairKind.Afro;
            bare.Skin = new Color(0.36f, 0.22f, 0.15f);
            bare.Hair = CharacterArt.DarkHair;
            return new[] { toque, cap, horns, bare };
        }

        // A person standing at `at`, turned `facing` degrees, in `colour`; carrying a plate of steak
        // at the chef's carry point, both hands aimed at it as ChefArmsView does.
        private static void Person(CharacterArt.Look look, Color colour, Vector3 at, float facing, bool carry)
        {
            var root = new GameObject(look.Name).transform;
            root.SetPositionAndRotation(new Vector3(at.x, 0f, at.z), Quaternion.Euler(0f, facing, 0f));
            var rig = CharacterArt.Build(root, look);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", colour);
            foreach (var renderer in rig.Tinted)
                renderer.SetPropertyBlock(block);
            if (!carry)
                return;

            var anchor = new GameObject("CarryAnchor").transform;
            anchor.SetParent(root, false);
            anchor.localPosition = CarryAnchor;
            anchor.localScale = Vector3.one * ItemSizes.Carried;
            var plate = Spawn(PlatePath, anchor);
            if (plate != null)
                Spawn(SteakPath, plate.transform).transform.localPosition = new Vector3(0f, 0.012f, 0f);
            foreach (var (shoulder, side) in new[] { (rig.LeftShoulder, -1f), (rig.RightShoulder, 1f) })
            {
                var towards = anchor.position + root.right * (side * 0.17f) - shoulder.position;
                shoulder.rotation = Quaternion.FromToRotation(-root.up, towards.normalized) * root.rotation;
            }
        }

        private static GameObject Spawn(string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return null;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
        }

        // Everything rendered under a named object, in world space.
        private static Bounds Bounds(string name)
        {
            var root = GameObject.Find(name);
            if (root == null)
                throw new InvalidOperationException($"No {name} in the kitchen scene.");
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        // Pitched like the game camera, looking at a point from the given height.
        private static void Render(Camera camera, RenderTexture target, Vector3 lookAt, float height, string path)
        {
            var back = (height - lookAt.y) / Mathf.Tan(Pitch * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(new Vector3(lookAt.x, height, lookAt.z - back), Quaternion.Euler(Pitch, 0f, 0f));
            Draw(camera, target, path);
        }

        // Straight on at head height, like the concept sheet, to judge faces and proportions.
        private static void RenderFront(Camera camera, RenderTexture target, Vector3 middle, string path)
        {
            camera.transform.SetPositionAndRotation(middle + new Vector3(0f, 1.1f, -9.5f), Quaternion.Euler(4f, 0f, 0f));
            Draw(camera, target, path);
        }

        private static void Draw(Camera camera, RenderTexture target, string path)
        {
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
