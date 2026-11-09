using Game.Presentation;
using Game.Presentation.Office;
using Game.Presentation.Office.Ambience;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// The office at night (user's note, 2026-11-09: "it looks flat; the scene happens at night;
    /// play with sources of light out and inside the office so each room gets its own personality").
    /// The sun becomes a low, cool moon, the city fades into a deep blue haze, and the rooms are lit
    /// by their own lamps, each in its own way. Lamps light surfaces naturally rather than in toon
    /// bands (the user's call), through ToonCommon's LocalLights, which LampLighting turns on for
    /// this scene only.
    /// </summary>
    public static partial class OfficeBuilder
    {
        private static readonly Color Moon = new Color(0.55f, 0.62f, 0.95f);
        private static readonly Color Night = new Color(0.07f, 0.08f, 0.16f);
        private static readonly Color Incandescent = new Color(1f, 0.78f, 0.52f);
        private static readonly Color Candle = new Color(1f, 0.62f, 0.3f);
        private static readonly Color CoolWhite = new Color(0.85f, 0.93f, 1f);
        private static readonly Color Showroom = new Color(1f, 0.96f, 0.9f);
        private static readonly Color Screen = new Color(0.45f, 0.62f, 1f);
        private static readonly Color MapGlow = new Color(0.3f, 0.7f, 1f);

        // The moon, the night sky, the haze and the switch that lets lamps light the toon surfaces.
        private static void NightLighting(Light sun, Camera camera)
        {
            sun.color = Moon;
            // Bright enough that no corner goes black (user's note, 2026-11-09: some rooms too dark).
            sun.intensity = 0.95f;
            sun.shadowStrength = 0.6f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.3f, 0.32f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.18f, 0.18f, 0.26f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.08f);
            RenderSettings.fogColor = Night;
            camera.backgroundColor = Night;
            // Lamps fall off with the square of distance, so ceiling lights 3-4 m up need a strong
            // hand; 2.5 lets intensities stay readable numbers per lamp.
            var lighting = new UnityEditor.SerializedObject(new GameObject("LampLighting").AddComponent<LampLighting>());
            lighting.FindProperty("_localLights").floatValue = 3f;
            lighting.ApplyModifiedPropertiesWithoutUndo();
        }

        // Every room's lamps, and the fixtures that hold them.
        private static void LightRooms(Transform rooms)
        {
            var lights = new GameObject("RoomLights").transform;
            lights.SetParent(rooms, false);

            // A broad, soft fill in every room, in the room's own tone, so nothing is lost in the
            // dark and the lamps below are the accents (user's note, 2026-11-09: some rooms too dark).
            RoomFill(lights, 1, 0, Incandescent, 4.32f);              // reception: welcoming
            RoomFill(lights, 1, 1, new Color(0.8f, 0.7f, 0.65f), 2.16f); // guarded doors: kept dim
            RoomFill(lights, 1, 2, Candle, 3.24f);                    // the boss: firelit
            RoomFill(lights, 0, 2, Showroom, 3.78f);                  // tailor: a showroom
            RoomFill(lights, 0, 1, CoolWhite, 4.32f);                 // test kitchen: fluorescent
            RoomFill(lights, 2, 2, new Color(0.55f, 0.7f, 1f), 2.16f); // miniature: dark, blue
            RoomFill(lights, 2, 1, Incandescent, 2.97f);              // hall of fame: a museum
            RoomFill(lights, 0, 0, Candle, 4.86f);                    // lounge: cosy
            RoomFill(lights, 2, 0, new Color(0.85f, 0.95f, 0.85f), 3.24f); // accountant: greenish tube light

            // Reception: warm and welcoming, four pendants over the lobby.
            foreach (var x in new[] { -4.5f, 4.5f })
            foreach (var z in new[] { -6.2f, -11f })
                Point(lights, "ReceptionPendant", new Vector3(x, 3.0f, z), Incandescent, 9f, 4.5f);

            // The guarded doors: dim and watchful. Amber sconces on the columns, a cold light from
            // above on the bodyguards, the rest left in shadow.
            foreach (var x in new[] { -1.5f, 1.5f })
                Point(lights, "Sconce", new Vector3(x, 2.1f, 2.6f), Candle, 4f, 2.2f);
            Spot(lights, "GuardSpot", new Vector3(0f, 5f, 1.8f), new Vector3(0f, 0f, 0f), CoolWhite, 9f, 60f, 5.4f);

            // The boss's office: a golden pool of desk lamp, amber floor lamps and a fire.
            Point(lights, "DeskLamp", new Vector3(0.9f, 1.5f, 10.6f), Incandescent, 6f, 3f);
            Fireplace(rooms.Find("BossOffice"), new Vector3(-8.65f, 0f, 8.4f), lights);

            // The tailor: a showroom, a bright spot down on each plinth.
            foreach (var plinth in new[] { new Vector3(-15.4f, 0f, 9.8f), new Vector3(-13.25f, 0f, 10.6f), new Vector3(-11.1f, 0f, 9.8f) })
                Spot(lights, "PlinthSpot", plinth + Vector3.up * 3.6f, Vector3.zero, Showroom, 6f, 45f, 8.4f);
            Point(lights, "TailorFill", new Vector3(-13.25f, 2.8f, 5.5f), Incandescent, 6f, 2.5f);

            // The test kitchen: cold fluorescent tubes, one of them on its way out.
            FluorescentTube(rooms, lights, new Vector3(-15.0f, 0f, 0.6f), faulty: false);
            FluorescentTube(rooms, lights, new Vector3(-11.6f, 0f, 0.6f), faulty: true);

            // The miniature room: kept dark, so the city on the table is the light, one strong spot
            // over it and a blue glow off the model city.
            Spot(lights, "MiniatureSpot", new Vector3(13.4f, 4.5f, 8.8f), Vector3.zero, Showroom, 7f, 55f, 8.4f);
            Point(lights, "MapGlow", new Vector3(13.4f, 1.2f, 9.0f), MapGlow, 4f, 1.6f);

            // The hall of fame: a museum, a spot on each trophy plinth, the lectern and the portraits.
            foreach (var x in new[] { 10.4f, 11.85f, 13.3f, 14.75f, 16.2f })
                Spot(lights, "PlinthSpot", new Vector3(x, 3.4f, 2.2f), new Vector3(-12f, 0f, 0f), Incandescent, 5f, 34f, 7.2f);
            Spot(lights, "LecternSpot", new Vector3(13.25f, 3.8f, -1.0f), new Vector3(-10f, 0f, 0f), Showroom, 6f, 45f, 8.4f);
            Spot(lights, "PortraitWash", new Vector3(15.6f, 2.6f, -1.2f), new Vector3(0f, 0f, -35f), Candle, 5f, 80f, 3.6f);

            // The lounge: cosy floor lamps (FloorLamp lights itself) and a television on.
            Television(rooms.Find("WaitingLounge"), new Vector3(-10.6f, 0f, -5.3f), lights); // clear of the door to the test kitchen

            // The accountant: a green banker's lamp on the desk under a tired tube.
            BankersLamp(rooms.Find("Accountant"), new Vector3(12.15f, 0.66f, -9.75f), lights);
            FluorescentTube(rooms, lights, new Vector3(13.25f, 0f, -7.8f), faulty: true);

            // The terrace: string lights along the rail (the lamp posts light themselves).
            TerraceStringLights(rooms.Find("Terrace"), lights);
        }

        // A room's fill: high over its middle, reaching its corners.
        private static void RoomFill(Transform lights, int column, int row, Color color, float intensity)
        {
            var centre = RoomCentre(column, row);
            var size = RoomSize(column, row);
            Point(lights, "RoomFill", new Vector3(centre.x, 4.5f, centre.y), color, Mathf.Max(size.x, size.y) * 0.85f, intensity);
        }

        // ---- Fixtures ----------------------------------------------------------------------------

        // A fireplace against a wall, facing into the room (+X): a stone surround, the fire, and a
        // flickering orange light.
        private static void Fireplace(Transform room, Vector3 at, Transform lights)
        {
            var stone = Toon("OfficeFireplace", new Color(0.62f, 0.56f, 0.5f));
            var fire = new GameObject("Fireplace").transform;
            fire.SetParent(room, false);
            fire.localPosition = at;
            Block(fire, "Surround", stone, Vector3.zero, new Vector3(0.6f, 1.3f, 2.0f), collider: true);
            Block(fire, "Mantel", Toon("OfficeWalnut", Walnut), new Vector3(0.05f, 1.3f, 0f), new Vector3(0.75f, 0.1f, 2.2f));
            Block(fire, "Hearth", Toon("OfficeInk", Ink), new Vector3(0.31f, 0.15f, 0f), new Vector3(0.02f, 0.75f, 1.2f));
            var flames = Shape(fire, PrimitiveType.Sphere, "Flames", Unlit("OfficeFire", new Color(1f, 0.55f, 0.15f)), new Vector3(0.3f, 0.35f, 0f), new Vector3(0.2f, 0.45f, 0.8f));
            Wobble(flames, Oscillate.Shape.Wave, Vector3.zero, Vector3.zero, 0.37f, grow: new Vector3(0f, 0.08f, 0.05f));
            var light = Point(lights, "FireLight", at + new Vector3(1.0f, 0.7f, 0f), Candle, 7f, 2.6f);
            light.gameObject.AddComponent<FlickerLight>().Set(light, FlickerLight.Mood.Fire, null, 0.2f, (float)_random.NextDouble() * 10f);
        }

        // A television on a low cabinet against the lounge's back wall, facing the sofas.
        private static void Television(Transform room, Vector3 at, Transform lights)
        {
            var tv = new GameObject("Television").transform;
            tv.SetParent(room, false);
            tv.localPosition = at;
            Block(tv, "Cabinet", Toon("OfficeWalnut", Walnut), Vector3.zero, new Vector3(1.8f, 0.5f, 0.45f), collider: true);
            Block(tv, "Set", Toon("OfficeInk", Ink), new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 0.85f, 0.12f));
            var screen = Cube(tv, "Screen", Unlit("OfficeTvScreen", Screen), new Vector3(0f, 0.57f, -0.07f), new Vector3(1.25f, 0.72f, 0.01f));
            screen.isStatic = false;
            var light = Spot(lights, "TvLight", at + new Vector3(0f, 1.0f, -0.4f), new Vector3(10f, 180f, 0f), Screen, 7f, 110f, 2.1f);
            light.gameObject.AddComponent<FlickerLight>().Set(light, FlickerLight.Mood.Television, null, 0.22f, (float)_random.NextDouble() * 10f);
        }

        // A banker's lamp: a brass stem under a green glass shade, a warm pool on the desk.
        private static void BankersLamp(Transform room, Vector3 at, Transform lights)
        {
            Block(room, "BankersLampStem", Toon("OfficeBrass", Brass), at, new Vector3(0.05f, 0.32f, 0.05f));
            Shape(room, PrimitiveType.Cylinder, "BankersLampShade", Unlit("OfficeBankersGreen", new Color(0.25f, 0.75f, 0.4f)), at + new Vector3(0f, 0.36f, 0f), new Vector3(0.45f, 0.05f, 0.18f), Quaternion.Euler(0f, 0f, 90f));
            Point(lights, "BankersLamp", at + new Vector3(0f, 0.3f, 0f), Incandescent, 3.5f, 2.5f);
        }

        // A fluorescent tube high over a room: a glowing strip and a cool light; a faulty one
        // stutters every few seconds and the strip goes dark with it.
        private static void FluorescentTube(Transform rooms, Transform lights, Vector3 at, bool faulty)
        {
            var light = Point(lights, faulty ? "FaultyTube" : "Tube", at + Vector3.up * 2.8f, CoolWhite, 8f, faulty ? 4f : 4.5f);
            if (!faulty)
                return;
            light.gameObject.AddComponent<FlickerLight>().Set(light, FlickerLight.Mood.Faulty, null, 5f + (float)_random.NextDouble() * 4f, (float)_random.NextDouble() * 10f);
        }

        // Warm bulbs strung along the terrace's outer rail, every few metres a light among them.
        private static void TerraceStringLights(Transform terrace, Transform lights)
        {
            var bulb = Unlit("OfficeBulb", Bulb);
            var line = Toon("OfficeInk", Ink);
            Block(terrace, "StringLine", line, new Vector3(0f, 1.6f, TerraceNorth - 0.05f), new Vector3(East - West, 0.015f, 0.015f));
            var i = 0;
            for (var x = West + 0.6f; x < East - 0.5f; x += 1.1f, i++)
            {
                // Hanging in shallow swags between the posts.
                var sag = Mathf.Abs(Mathf.Sin((x - West) / 3.3f * Mathf.PI)) * 0.18f;
                TwinklingBulb(terrace, bulb, new Vector3(x, 1.55f - sag, TerraceNorth - 0.05f), 6f + (float)_random.NextDouble() * 4f, 0.92f);
                if (i % 6 == 3)
                    Point(lights, "StringGlow", new Vector3(x, 1.4f, TerraceNorth - 0.6f), Incandescent, 4.5f, 1.4f);
            }
        }

        // ---- Lights ----------------------------------------------------------------------------

        private static Light Point(Transform parent, string name, Vector3 at, Color color, float range, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        // A spot pointing straight down, then tilted by `tilt` (degrees about X, Y, Z).
        private static Light Spot(Transform parent, string name, Vector3 at, Vector3 tilt, Color color, float range, float angle, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = at;
            light.transform.localRotation = Quaternion.Euler(tilt) * Quaternion.Euler(90f, 0f, 0f);
            light.type = LightType.Spot;
            light.color = color;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.6f;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        // A light in the city: a warm or coloured glow round something on a roof.
        private static void Glow(Transform parent, Vector3 at, Color color, float range, float intensity) =>
            Point(parent, "Glow", at, color, range, intensity);
    }
}
