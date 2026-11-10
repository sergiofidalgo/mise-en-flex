using Game.Data;
using Game.Gameplay.Office;
using Game.Presentation.Office;
using Game.Simulation.Campaign;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The lots on the city miniature (Phase 6, milestone 2): one per restaurant in the Miniature
    /// Config, each in a block of the model city, sized by the restaurant's size, with a flag for
    /// owned, a board for sale and a ring for the highlight (MiniatureLotsView). Also wires the
    /// table to the campaign (OfficeCampaign) and the card (MiniatureHud) to its data.
    /// Kept in its own file so the boss's milestone can change the office builder alongside.
    /// </summary>
    public static partial class OfficeBuilder
    {
        // Each lot's block on the miniature's grid (column, row; see Miniature in OfficeBuilder.cs),
        // in the config's order: the grill near the front left, the grand hall at the back, the
        // harbour on the front edge. None is on a street (every 4th column, every 3rd row).
        private static readonly Vector2Int[] LotCells =
        {
            new Vector2Int(2, 1), new Vector2Int(5, 4), new Vector2Int(9, 1),
            new Vector2Int(1, 6), new Vector2Int(12, 0), new Vector2Int(6, 7),
        };

        private const float MiniatureStep = 0.34f; // must match Miniature in OfficeBuilder.cs
        private static readonly Vector3 MiniatureGround = new Vector3(13.4f, 0.89f, 9.0f);
        private static readonly Vector2 MiniatureSize = new Vector2(4.6f, 2.8f);

        /// <summary>True for a block of the miniature's city that a lot stands on instead.</summary>
        private static bool IsLotCell(int column, int row)
        {
            foreach (var cell in LotCells)
                if (cell.x == column && cell.y == row)
                    return true;
            return false;
        }

        private static Vector3 LotCellCentre(Vector2Int cell) => MiniatureGround + new Vector3(
            -MiniatureSize.x / 2f + MiniatureStep * (cell.x + 0.5f), 0f, -MiniatureSize.y / 2f + MiniatureStep * (cell.y + 0.5f));

        private static void AddMiniature(Transform rooms)
        {
            var table = Object.FindFirstObjectByType<MiniatureTable>();
            var city = rooms.Find("BackRoom/MiniatureCity");
            if (table == null || city == null)
            {
                Debug.LogWarning("[OfficeBuilder] No miniature table to put lots on.");
                return;
            }
            var config = RestaurantDefinitions.EnsureMiniature();
            var campaign = table.gameObject.AddComponent<OfficeCampaign>();
            var wiring = new SerializedObject(campaign);
            wiring.FindProperty("_config").objectReferenceValue = config;
            wiring.ApplyModifiedPropertiesWithoutUndo();

            var lots = new GameObject("Lots").transform;
            lots.SetParent(city, false);
            var settings = config.LotSettings();
            var pins = new Transform[settings.Count];
            var facade = Toon("OfficeMiniLot", new Color(0.86f, 0.78f, 0.62f));
            var roof = Toon("OfficeMiniLotRoof", new Color(0.55f, 0.27f, 0.2f));
            var flag = Toon("OfficeBellRed", new Color(0.8f, 0.2f, 0.18f));
            var board = Toon("OfficeMiniSale", new Color(0.93f, 0.74f, 0.33f));
            var post = Toon("OfficeBrass", Brass);
            var ring = Unlit("OfficeMiniRing", new Color(1f, 0.82f, 0.4f));
            for (var i = 0; i < settings.Count; i++)
            {
                if (i >= LotCells.Length)
                {
                    Debug.LogWarning($"[OfficeBuilder] No block on the miniature for lot {settings[i].Id}: add one to LotCells.");
                    continue;
                }
                var at = LotCellCentre(LotCells[i]);
                var pin = new GameObject($"Lot_{settings[i].Id}").transform;
                pin.SetParent(lots, false);
                pin.localPosition = at; // the city and its lots sit at the room's origin, as Miniature's blocks do
                pins[i] = pin;

                // The building: the bigger the restaurant, the bigger the block.
                var (width, height) = settings[i].Size switch
                {
                    RestaurantSize.Counter => (0.18f, 0.09f),
                    RestaurantSize.Small => (0.22f, 0.12f),
                    RestaurantSize.DiningRoom => (0.27f, 0.15f),
                    _ => (0.3f, 0.2f),
                };
                Model(pin, "Building", CityModel, "CityBlock", facade, Vector3.zero, 0f, scale3: new Vector3(width, height, width));
                Cube(pin, "Roof", roof, Vector3.up * height, new Vector3(width + 0.02f, 0.015f, width + 0.02f));

                // Owned: a red flag on a brass pole, as the grill's pin was.
                var owned = new GameObject("Owned").transform;
                owned.SetParent(pin, false);
                owned.localPosition = Vector3.up * height;
                Shape(owned, PrimitiveType.Cylinder, "Pole", post, Vector3.up * 0.22f, new Vector3(0.022f, 0.22f, 0.022f));
                Cube(owned, "Flag", flag, new Vector3(0.09f, 0.3f, 0f), new Vector3(0.18f, 0.13f, 0.015f));

                // On sale: a gold board on a post, facing the camera.
                var sale = new GameObject("ForSale").transform;
                sale.SetParent(pin, false);
                sale.localPosition = Vector3.up * height;
                Shape(sale, PrimitiveType.Cylinder, "Post", post, new Vector3(0f, 0.1f, -width * 0.3f), new Vector3(0.018f, 0.1f, 0.018f));
                Cube(sale, "Board", board, new Vector3(0f, 0.14f, -width * 0.3f - 0.012f), new Vector3(0.22f, 0.13f, 0.018f));

                // The highlight: a glowing ring on the table round the lot and a gold diamond floating
                // over it, big enough to spot from the play camera with four chefs round the table.
                var highlight = new GameObject("Highlight").transform;
                highlight.SetParent(pin, false);
                Shape(highlight, PrimitiveType.Cylinder, "Ring", ring, Vector3.up * 0.003f, new Vector3(width + 0.24f, 0.002f, width + 0.24f));
                Shape(highlight, PrimitiveType.Cube, "Marker", ring, Vector3.up * (height + 0.55f), Vector3.one * 0.11f, Quaternion.Euler(45f, 0f, 45f));
                highlight.gameObject.SetActive(false);

                // These move and switch on and off while the table is used.
                foreach (var part in pin.GetComponentsInChildren<Transform>(true))
                    if (part.name != "Building" && part.name != "Roof")
                        part.gameObject.isStatic = false;
            }

            var tableWiring = new SerializedObject(table);
            tableWiring.FindProperty("_campaign").objectReferenceValue = campaign;
            var pinList = tableWiring.FindProperty("_pins");
            pinList.arraySize = pins.Length;
            for (var i = 0; i < pins.Length; i++)
                pinList.GetArrayElementAtIndex(i).objectReferenceValue = pins[i];
            tableWiring.ApplyModifiedPropertiesWithoutUndo();

            var view = new SerializedObject(table.gameObject.AddComponent<MiniatureLotsView>());
            view.FindProperty("_table").objectReferenceValue = table;
            view.ApplyModifiedPropertiesWithoutUndo();

            // The card's player colours and appliance names (its style and table come with the opening).
            var hud = Object.FindFirstObjectByType<MiniatureHud>();
            if (hud != null)
            {
                var card = new SerializedObject(hud);
                card.FindProperty("_coop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CoopSettings>(SettingsFolder + "/CoopSettings.asset");
                card.FindProperty("_furniture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FurnitureCatalog>(SettingsFolder + "/FurnitureCatalog.asset");
                card.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
