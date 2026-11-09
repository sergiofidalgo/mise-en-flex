using System.Collections.Generic;
using Game.Presentation.Office.Ambience;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The life round the boss's floor (user's note, 2026-11-09: "animate the scene as much as
    /// possible so looking at the environment, for example when entering the terrace, brings
    /// joy"). A few roofs each have one recognisable thing going on (a heliport with a helicopter
    /// warming up, a garden with twinkling lights, a pool, a crane, a giant spinning steak, washing
    /// on a line); people smoke on their decks; pigeons wheel over the roofs and peck along the
    /// terrace rail, gulls circle higher, a helicopter laps the skyline; windows light up and go
    /// dark and a few flicker with televisions; aviation lights blink on the tallest towers. The
    /// office's own people breathe, and the bodyguards glance about. Everything moves with the
    /// Ambience components (pure functions of time), so it costs little and never drifts.
    /// </summary>
    public static partial class OfficeBuilder
    {
        // Handed out in order to the near roofs, the ones the camera sees best.
        private static readonly string[] RooftopSpecials =
            { "Heliport", "Garden", "Crane", "SteakSign", "Pool", "Laundry", "Garden", "Laundry" };

        private static readonly Color AviationRed = new Color(1f, 0.18f, 0.12f);
        private static readonly Color Bulb = new Color(1f, 0.85f, 0.5f);
        private static readonly Color CraneYellow = new Color(0.95f, 0.72f, 0.18f);
        private static readonly Color Pigeon = new Color(0.55f, 0.56f, 0.62f);
        private static readonly Color Gull = new Color(0.94f, 0.94f, 0.92f);

        private static readonly Color[] CasualTops =
        {
            new Color(0.85f, 0.35f, 0.3f), new Color(0.3f, 0.5f, 0.7f), new Color(0.92f, 0.85f, 0.6f),
            new Color(0.35f, 0.55f, 0.4f), new Color(0.6f, 0.4f, 0.65f), new Color(0.95f, 0.95f, 0.92f),
        };

        private static readonly List<Renderer> _cityWindows = new List<Renderer>();
        private static readonly List<Renderer> _televisions = new List<Renderer>();
        private static System.Random _windowsFor;

        // ---- Hooks from the main builder ------------------------------------------------------------

        // A city window: most switch on and off now and then; a few are televisions.
        private static void RegisterWindow(GameObject window)
        {
            if (window == null)
                return;
            if (_windowsFor != _random)
            {
                // A new build: forget the last one's windows.
                _windowsFor = _random;
                _cityWindows.Clear();
                _televisions.Clear();
            }
            window.isStatic = false; // its material changes at runtime
            var renderer = window.GetComponent<Renderer>();
            if (_random.NextDouble() < 0.04)
            {
                renderer.sharedMaterial = Unlit("OfficeWindowTv", new Color(0.45f, 0.6f, 0.9f));
                _televisions.Add(renderer);
            }
            else
            {
                _cityWindows.Add(renderer);
            }
        }

        // Three blades over an air conditioner's fan, turning.
        private static void SpinningFan(Transform parent, Vector3 at)
        {
            var blades = Model(parent, "FanBlades", OfficeModel, "OfficeFanBlades", Toon("OfficeRoofGrey", new Color(0.56f, 0.55f, 0.53f)), at, (float)_random.NextDouble() * 120f);
            if (blades == null)
                return;
            blades.isStatic = false;
            blades.AddComponent<Spin>().Set(Vector3.up, 200f + (float)_random.NextDouble() * 160f);
        }

        // Someone out on their deck for a cigarette: in the players' character kit, casually
        // dressed, raising it now and then and breathing out a puff; the cigarette smokes a wisp.
        private static void Smoker(Transform parent, Vector3 at, float facing)
        {
            var person = new GameObject("Smoker").transform;
            person.SetParent(parent, false);
            person.localPosition = at;
            person.localRotation = Quaternion.Euler(0f, facing, 0f);
            var look = new CharacterArt.Look
            {
                Name = "Smoker",
                Top = CasualTops[_random.Next(CasualTops.Length)],
                Bottom = new Color(0.2f + (float)_random.NextDouble() * 0.2f, 0.22f, 0.3f),
                Skin = CharacterArt.SkinTones[_random.Next(CharacterArt.SkinTones.Length)],
                HairStyle = (CharacterArt.HairKind)_random.Next(5),
                Hair = CharacterArt.NaturalHair[_random.Next(CharacterArt.NaturalHair.Length)],
            };
            // The arms are left to the smoker, not the walk.
            var rig = CharacterArt.Build(person, look, swingArms: false);
            if (rig == null)
            {
                Object.DestroyImmediate(person.gameObject);
                return;
            }
            Breathe(person.gameObject);

            // The cigarette in the right hand, its lit end glowing, a wisp rising from it.
            var hand = new GameObject("Cigarette").transform;
            hand.SetParent(rig.RightShoulder, false);
            hand.localPosition = new Vector3(0f, -CharacterArt.Reach, 0.08f);
            Shape(hand, PrimitiveType.Cylinder, "Paper", Toon("OfficeCigarette", new Color(0.96f, 0.95f, 0.92f)), Vector3.zero, new Vector3(0.025f, 0.05f, 0.025f), Quaternion.Euler(90f, 0f, 0f));
            Shape(hand, PrimitiveType.Sphere, "Ember", Unlit("OfficeEmber", new Color(1f, 0.45f, 0.15f)), new Vector3(0f, 0f, 0.055f), Vector3.one * 0.03f);
            var wisp = Puffs(hand, "Wisp", 0.06f, 2.5f, 0.6f, 0.05f, 0.12f);
            var wispEmission = wisp.emission;
            wispEmission.rateOverTime = 3f;

            // The puff, from in front of the face.
            var mouth = new GameObject("Mouth").transform;
            mouth.SetParent(rig.Waist, false);
            mouth.localPosition = new Vector3(0f, 0.66f, 0.3f);
            var exhale = Puffs(mouth, "Exhale", 0f, 2.2f, 0.5f, 0.1f, 0.22f);

            person.gameObject.AddComponent<Game.Presentation.Office.Ambience.Smoker>()
                .Set(rig.RightShoulder, exhale, 6f + (float)_random.NextDouble() * 4f, (float)_random.NextDouble());
            MakeDynamic(person.gameObject);
        }

        // Soft grey puffs in the kitchen's own smoke look, rising and spreading.
        private static ParticleSystem Puffs(Transform parent, string name, float height, float lifetime, float speed, float minSize, float maxSize)
        {
            var puffs = GreyboxKitchenware.CreateParticles(parent, name, height, 0.01f, lifetime, speed, minSize, maxSize, 3f, 60);
            puffs.GetComponent<ParticleSystemRenderer>().sharedMaterial = PilotArt.EnsureMistMaterial();
            var main = puffs.main;
            main.startColor = new Color(0.86f, 0.86f, 0.88f, 0.7f);
            return puffs;
        }

        // ---- Special roofs ----------------------------------------------------------------------------

        private static void SpecialRoof(string special, Transform roof, float w, float d, Material wall, Material trim)
        {
            switch (special)
            {
                case "Heliport": Heliport(roof, w, d); break;
                case "Garden": Garden(roof, w, d); break;
                case "Crane": Crane(roof, w, d); break;
                case "SteakSign": SteakSign(roof, w, d); break;
                case "Pool": Pool(roof, w, d); break;
                case "Laundry": Laundry(roof, w, d, wall, trim); break;
            }
        }

        // A heliport: a pad, lights round it, a windsock, and a helicopter warming up.
        private static void Heliport(Transform roof, float w, float d)
        {
            var paint = Toon("OfficeHelipadPaint", new Color(0.95f, 0.78f, 0.28f));
            var size = Mathf.Min(w, d) * 0.8f;
            Shape(roof, PrimitiveType.Cylinder, "PadRing", paint, Vector3.up * 0.02f, new Vector3(size, 0.012f, size));
            Shape(roof, PrimitiveType.Cylinder, "Pad", Toon("OfficeHelipad", new Color(0.24f, 0.25f, 0.29f)), Vector3.up * 0.03f, new Vector3(size - 0.4f, 0.012f, size - 0.4f));
            Cube(roof, "H_Left", paint, new Vector3(-0.7f, 0.045f, 0f), new Vector3(0.3f, 0.01f, 2f));
            Cube(roof, "H_Right", paint, new Vector3(0.7f, 0.045f, 0f), new Vector3(0.3f, 0.01f, 2f));
            Cube(roof, "H_Bar", paint, new Vector3(0f, 0.045f, 0f), new Vector3(1.4f, 0.01f, 0.3f));
            for (var k = 0; k < 8; k++)
            {
                var a = k * Mathf.PI / 4f;
                AviationLight(roof, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (size / 2f), 1.2f, k / 8f, green: true);
            }
            var heli = Helicopter(roof, "ParkedHelicopter", new Color(0.82f, 0.22f, 0.18f), rotorSpeed: 280f);
            heli.localPosition = new Vector3(0.3f, 0.05f, 0f);
            heli.localRotation = Quaternion.Euler(0f, 200f, 0f);
            heli.localScale = Vector3.one * 0.8f;

            var mast = new Vector3(w * 0.42f, 0f, d * 0.4f);
            Block(roof, "SockMast", Toon("OfficeRoofGrey", new Color(0.56f, 0.55f, 0.53f)), mast, new Vector3(0.08f, 2.6f, 0.08f));
            var sock = Model(roof, "Windsock", OfficeModel, "OfficeWindsock", Toon("OfficeWindsock", new Color(1f, 0.45f, 0.15f)), mast + Vector3.up * 2.5f, 160f);
            Wobble(sock, Oscillate.Shape.Wave, Vector3.zero, new Vector3(0f, 25f, 8f), 3.5f);
            AviationLight(roof, mast + Vector3.up * 2.65f, 1.6f, 0.3f);
        }

        // A roof garden (user's note: "another a garden"): a lawn, raised beds, two small trees in
        // big planters, a pergola strung with twinkling lights, and a gardener watering.
        private static void Garden(Transform roof, float w, float d)
        {
            Cube(roof, "Lawn", Tiles("OfficeLawn", new Color(0.42f, 0.6f, 0.32f), new Color(0.38f, 0.55f, 0.29f), 0.9f, 0.004f),
                Vector3.up * 0.02f, new Vector3(w * 0.8f, 0.02f, d * 0.75f));
            var teak = Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f));
            foreach (var x in new[] { -0.3f, 0.3f })
            {
                var bed = new Vector3(x * w, 0f, -d * 0.28f);
                Block(roof, "Bed", teak, bed, new Vector3(w * 0.25f, 0.45f, 0.9f));
                Block(roof, "Flowers", Toon("OfficeFlowers", new Color(0.9f, 0.45f, 0.55f)), bed + Vector3.up * 0.45f, new Vector3(w * 0.23f, 0.18f, 0.8f));
            }
            foreach (var x in new[] { -0.32f, 0.32f })
                Tree(roof, new Vector3(x * w, 0f, d * 0.3f));

            // The pergola, its lights on the beams.
            var posts = new[] { new Vector3(-1.6f, 0f, -0.8f), new Vector3(1.6f, 0f, -0.8f), new Vector3(-1.6f, 0f, 1.2f), new Vector3(1.6f, 0f, 1.2f) };
            foreach (var post in posts)
                Block(roof, "PergolaPost", teak, post, new Vector3(0.14f, 2.4f, 0.14f));
            foreach (var z in new[] { -0.8f, 1.2f })
                Block(roof, "PergolaBeam", teak, new Vector3(0f, 2.4f, z), new Vector3(3.6f, 0.14f, 0.14f));
            for (var x = -1.4f; x <= 1.41f; x += 0.7f)
                Block(roof, "PergolaSlat", teak, new Vector3(x, 2.54f, 0.2f), new Vector3(0.08f, 0.08f, 2.4f));
            var bulb = Unlit("OfficeBulb", Bulb);
            for (var i = 0; i < 12; i++)
            {
                var z = i < 6 ? -0.8f : 1.2f;
                var x = -1.5f + (i % 6) * 0.6f;
                TwinklingBulb(roof, bulb, new Vector3(x, 2.32f, z), 2f + (float)_random.NextDouble() * 2f, 0.6f + (float)_random.NextDouble() * 0.3f);
            }
            Block(roof, "Table", teak, new Vector3(0f, 0f, 0.2f), new Vector3(1.2f, 0.72f, 0.8f));

            // The gardener, bending to water the beds now and then.
            var gardener = Person(roof, "Gardener", new Vector3(-0.3f * w + 1.2f, 0f, -d * 0.12f), 180f, new Color(0.35f, 0.5f, 0.3f), new Color(0.3f, 0.26f, 0.2f));
            Block(gardener, "WateringCan", Toon("OfficeCan", new Color(0.3f, 0.55f, 0.45f)), new Vector3(0.32f, 0.5f, 0.25f), new Vector3(0.18f, 0.22f, 0.3f));
            Wobble(gardener.gameObject, Oscillate.Shape.Pulse, Vector3.zero, new Vector3(25f, 0f, 0f), 5f, 0.4f);
        }

        // A building site: a yellow tower crane slowly swinging its jib, the load swaying under it,
        // red lights blinking at the top and the jib's tip.
        private static void Crane(Transform roof, float w, float d)
        {
            var yellow = Toon("OfficeCrane", CraneYellow);
            var grey = Toon("OfficeRoofGrey", new Color(0.56f, 0.55f, 0.53f));
            var mastAt = new Vector3(-w * 0.25f, 0f, d * 0.15f);
            const float mastHeight = 15f;
            Block(roof, "Mast", yellow, mastAt, new Vector3(0.9f, mastHeight, 0.9f));
            for (var y = 1f; y < mastHeight; y += 1.5f)
                Block(roof, "MastBrace", Toon("OfficeCraneDark", new Color(0.55f, 0.4f, 0.12f)), mastAt + Vector3.up * y, new Vector3(0.96f, 0.08f, 0.96f));

            var slew = new GameObject("Slew").transform;
            slew.SetParent(roof, false);
            slew.localPosition = mastAt + Vector3.up * mastHeight;
            Block(slew, "Cab", Toon("OfficeCraneCab", new Color(0.9f, 0.9f, 0.88f)), new Vector3(0.9f, -1.2f, 0f), new Vector3(1f, 1.1f, 1f));
            Block(slew, "Jib", yellow, new Vector3(8f, 0f, 0f), new Vector3(16f, 0.6f, 0.6f));
            Block(slew, "CounterJib", yellow, new Vector3(-2.5f, 0f, 0f), new Vector3(5f, 0.5f, 0.6f));
            Block(slew, "Counterweight", grey, new Vector3(-4.2f, -0.9f, 0f), new Vector3(1.4f, 1.2f, 1f));
            Block(slew, "Peak", yellow, new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 2.2f, 0.5f));
            Block(slew, "Trolley", grey, new Vector3(11f, -0.35f, 0f), new Vector3(0.7f, 0.3f, 0.7f));
            var hook = new GameObject("Hook").transform;
            hook.SetParent(slew, false);
            hook.localPosition = new Vector3(11f, -0.4f, 0f);
            Block(hook, "Cable", Toon("OfficeInk", Ink), new Vector3(0f, -6f, 0f), new Vector3(0.05f, 6f, 0.05f));
            Block(hook, "Load", Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f)), new Vector3(0f, -6.9f, 0f), new Vector3(1.4f, 0.9f, 1.0f));
            Wobble(hook.gameObject, Oscillate.Shape.Wave, Vector3.zero, new Vector3(4f, 0f, 3f), 3.2f);
            AviationLight(slew, new Vector3(16f, 0.4f, 0f), 1.5f, 0f);
            AviationLight(slew, new Vector3(0f, 1.9f, 0f), 1.5f, 0.5f);
            Wobble(slew.gameObject, Oscillate.Shape.Wave, Vector3.zero, new Vector3(0f, 55f, 0f), 34f);

            // The site below it: stacked materials and a skip.
            for (var i = 0; i < 3; i++)
                Block(roof, "Pallet", Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f)), new Vector3(w * 0.15f + i * 1.3f, 0f, -d * 0.25f), new Vector3(1.1f, 0.5f + i * 0.2f, 1.1f));
            Block(roof, "Skip", Toon("OfficeSkip", new Color(0.85f, 0.45f, 0.15f)), new Vector3(w * 0.25f, 0f, d * 0.25f), new Vector3(2.2f, 0.9f, 1.2f));
        }

        // A steakhouse's rooftop sign: a giant steak turning on a pole, ringed with chasing bulbs.
        private static void SteakSign(Transform roof, float w, float d)
        {
            Block(roof, "Pole", Toon("OfficeRoofGrey", new Color(0.56f, 0.55f, 0.53f)), Vector3.zero, new Vector3(0.35f, 4.5f, 0.35f));
            var turn = new GameObject("Turning").transform;
            turn.SetParent(roof, false);
            turn.localPosition = Vector3.up * 6.2f;
            turn.gameObject.AddComponent<Spin>().Set(Vector3.up, 35f);
            var steak = Model(turn, "GiantSteak", "SM_Steak", "SM_Steak", Toon("OfficeSignSteak", new Color(0.62f, 0.28f, 0.2f)), Vector3.zero, 0f, scale: 11f);
            if (steak != null)
                steak.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // standing up, like a sign
            var bulb = Unlit("OfficeBulb", Bulb);
            const int bulbs = 16;
            for (var i = 0; i < bulbs; i++)
            {
                var a = i * 2f * Mathf.PI / bulbs;
                // Chasing: each bulb a step behind the last.
                ChaseBulb(turn, bulb, new Vector3(Mathf.Cos(a) * 1.9f, Mathf.Sin(a) * 1.9f, 0.25f), (float)i / bulbs);
            }
            Block(roof, "SignBase", Toon("OfficeWalnut", Walnut), Vector3.zero, new Vector3(1.4f, 0.4f, 1.4f));
        }

        // A pool, someone drifting round it on a rubber ring, loungers and an umbrella.
        private static void Pool(Transform roof, float w, float d)
        {
            var pool = new Vector2(w * 0.55f, d * 0.45f);
            Block(roof, "PoolEdge", Toon("OfficePoolEdge", new Color(0.92f, 0.9f, 0.84f)), new Vector3(0f, 0f, -d * 0.05f), new Vector3(pool.x + 0.6f, 0.35f, pool.y + 0.6f));
            Cube(roof, "Water", Toon("OfficePoolWater", new Color(0.35f, 0.72f, 0.82f)), new Vector3(0f, 0.36f, -d * 0.05f), new Vector3(pool.x, 0.01f, pool.y));
            var floater = new GameObject("Floater").transform;
            floater.SetParent(roof, false);
            floater.gameObject.AddComponent<FlyLoop>().Set(new Vector3(0f, 0.37f, -d * 0.05f), pool * 0.3f, 0.03f, 26f, (float)_random.NextDouble(), false, 0f);
            Shape(floater, PrimitiveType.Cylinder, "Ring", Toon("OfficeRing", new Color(1f, 0.5f, 0.2f)), Vector3.zero, new Vector3(1f, 0.08f, 1f));
            var swimmer = Person(floater, "Swimmer", new Vector3(0f, -1.0f, 0f), 0f, CasualTops[_random.Next(CasualTops.Length)], new Color(0.2f, 0.4f, 0.7f), 0.9f);
            Wobble(swimmer.gameObject, Oscillate.Shape.Wave, new Vector3(0f, 0.05f, 0f), new Vector3(4f, 0f, 4f), 2.6f);
            var cushion = Toon("OfficeCushion", new Color(0.45f, 0.55f, 0.75f));
            foreach (var x in new[] { -0.3f, 0f, 0.3f })
            {
                var at = new Vector3(x * w, 0f, d * 0.33f);
                Model(roof, "Lounger", OfficeModel, "OfficeLoungerFrame", Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f)), at, 0f);
                Model(roof, "LoungerCushion", OfficeModel, "OfficeLoungerCushion", cushion, at, 0f);
            }
            var u = _random.Next(UmbrellaColors.Length);
            Model(roof, "UmbrellaPole", OfficeModel, "OfficeUmbrellaPole", Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f)), new Vector3(w * 0.42f, 0f, d * 0.3f), 0f);
            Model(roof, "UmbrellaCanopy", OfficeModel, "OfficeUmbrellaCanopy", Toon($"OfficeUmbrella{u}", UmbrellaColors[u]), new Vector3(w * 0.42f, 0f, d * 0.3f), 22.5f);
        }

        // Washing on a line, swaying, and someone out for a smoke beside it.
        private static void Laundry(Transform roof, float w, float d, Material wall, Material trim)
        {
            var grey = Toon("OfficeRoofGrey", new Color(0.56f, 0.55f, 0.53f));
            var from = new Vector3(-w * 0.3f, 0f, 0f);
            var to = new Vector3(w * 0.3f, 0f, 0f);
            Block(roof, "LinePost", grey, from, new Vector3(0.08f, 1.9f, 0.08f));
            Block(roof, "LinePost", grey, to, new Vector3(0.08f, 1.9f, 0.08f));
            Block(roof, "Line", Toon("OfficeCigarette", new Color(0.96f, 0.95f, 0.92f)), new Vector3(0f, 1.86f, 0f), new Vector3(w * 0.6f, 0.02f, 0.02f));
            var sheets = new[] { Cream, new Color(0.55f, 0.75f, 0.9f), new Color(0.95f, 0.75f, 0.75f), new Color(0.98f, 0.9f, 0.55f), Cream, new Color(0.6f, 0.82f, 0.6f) };
            var count = Mathf.Max(3, Mathf.FloorToInt(w * 0.6f / 1.0f));
            for (var i = 0; i < count; i++)
            {
                var peg = new GameObject("Peg").transform;
                peg.SetParent(roof, false);
                peg.localPosition = Vector3.Lerp(from, to, (i + 0.5f) / count) + Vector3.up * 1.85f;
                var c = i % sheets.Length;
                Block(peg, "Sheet", Toon($"OfficeSheet{c}", sheets[c]), new Vector3(0f, -0.95f, 0f), new Vector3(0.8f, 0.95f, 0.02f));
                Wobble(peg.gameObject, Oscillate.Shape.Wave, Vector3.zero, new Vector3(16f, 0f, 3f), 2.4f + (float)_random.NextDouble());
            }
            Block(roof, "StairHut", wall, new Vector3(-w * 0.3f, 0f, d * 0.3f), new Vector3(2.2f, 2.2f, 1.8f));
            Block(roof, "StairHutRoof", trim, new Vector3(-w * 0.3f, 2.2f, d * 0.3f), new Vector3(2.4f, 0.12f, 2.0f));
            Smoker(roof, new Vector3(w * 0.15f, 0f, d * 0.25f), 200f);
        }

        // ---- Over the whole scene -----------------------------------------------------------------

        // Called after the rooms and the city are built.
        private static void AddLife(Transform rooms)
        {
            var city = GameObject.Find("City").transform;
            var life = new GameObject("Life").transform;
            life.SetParent(city, false);

            // Windows switching on and off, and the televisions.
            city.gameObject.AddComponent<WindowFlicker>().Set(_cityWindows.ToArray(), Unlit("OfficeWindowLit", WindowLit), Unlit("OfficeWindowDark", WindowDark), _televisions.ToArray());

            // Pigeons wheeling round the near roofs on either side, gulls circling high over the city.
            Flock(life, new Vector3(-30f, -5f, 2f), new Vector2(10f, 7f), 8, 13f, gull: false);
            Flock(life, new Vector3(31f, -7f, -6f), new Vector2(9f, 8f), 7, 15f, gull: false);
            Flock(life, new Vector3(0f, 11f, 42f), new Vector2(55f, 30f), 5, 46f, gull: true);
            Flock(life, new Vector3(-10f, 7f, -40f), new Vector2(40f, 18f), 4, 38f, gull: true);

            // A helicopter lapping the skyline.
            var flyer = Helicopter(life, "FlyingHelicopter", new Color(0.2f, 0.32f, 0.52f), rotorSpeed: 900f);
            flyer.gameObject.AddComponent<FlyLoop>().Set(new Vector3(0f, 16f, 40f), new Vector2(95f, 60f), 2f, 75f, 0.15f, false, 12f);
            MakeDynamic(flyer.gameObject);

            // Pigeons on the boss's terrace: along the rail and on his helipad, pecking.
            var terrace = rooms.Find("Terrace");
            foreach (var x in new[] { -15.6f, -15.2f, -6.8f, 2.4f, 2.8f, 9.1f })
                PerchedBird(terrace, new Vector3(x, 1.04f, TerraceNorth), (float)_random.NextDouble() * 360f);
            foreach (var at in new[] { new Vector3(11.2f, 0.05f, 15.2f), new Vector3(14.3f, 0.05f, 17.9f), new Vector3(-4.4f, 0.01f, 15.6f) })
                PerchedBird(terrace, at, (float)_random.NextDouble() * 360f);

            // The office's people breathe; the bodyguards glance along the room now and then.
            // (One Oscillate per person: two would fight over the same transform.)
            foreach (var person in rooms.GetComponentsInChildren<CapsuleCollider>())
            {
                var who = person.gameObject;
                if (who.GetComponent<Oscillate>() != null)
                    continue;
                if (who.name.StartsWith("Bodyguard"))
                    Wobble(who, Oscillate.Shape.Pulse, Vector3.zero, new Vector3(0f, who.name.EndsWith("_L") ? -35f : 35f, 0f), 7f + (float)_random.NextDouble() * 3f, 0.35f);
                else
                    Breathe(who);
            }
        }

        // ---- Pieces ------------------------------------------------------------------------------

        // A flock of birds flying one loop, spread round it, each a little higher or lower and
        // wider or tighter, flapping their wings.
        private static void Flock(Transform parent, Vector3 centre, Vector2 radii, int count, float period, bool gull)
        {
            var clockwise = _random.NextDouble() < 0.5;
            for (var i = 0; i < count; i++)
            {
                var bird = FlyingBird(parent, gull);
                var spread = 1f + ((float)_random.NextDouble() - 0.5f) * 0.25f;
                bird.gameObject.AddComponent<FlyLoop>().Set(centre + Vector3.up * ((float)_random.NextDouble() * 2f - 1f), radii * spread,
                    gull ? 3f : 1.2f, period * spread, i * 0.6f / count + (float)_random.NextDouble() * 0.04f, clockwise, gull ? 14f : 22f);
            }
        }

        private static Transform FlyingBird(Transform parent, bool gull)
        {
            var bird = BirdBody(parent, "Bird", gull);
            foreach (var side in new[] { -1f, 1f })
            {
                var shoulder = new GameObject(side < 0 ? "Wing_L" : "Wing_R").transform;
                shoulder.SetParent(bird, false);
                shoulder.localPosition = new Vector3(side * 0.06f, 0.18f, 0f);
                var wing = Model(shoulder, "Wing", OfficeModel, "OfficeBirdWing", Toon(gull ? "OfficeGull" : "OfficePigeon", gull ? Gull : Pigeon), Vector3.zero, 0f);
                if (wing != null)
                    wing.transform.localScale = new Vector3(side, 1f, 1f);
                // Gulls glide with slow, deep beats; pigeons flap quickly.
                Wobble(shoulder.gameObject, Oscillate.Shape.Wave, Vector3.zero, new Vector3(0f, 0f, side * (gull ? 25f : 40f)), gull ? 0.9f : 0.28f);
            }
            MakeDynamic(bird.gameObject);
            return bird;
        }

        // A bird standing still, now and then dipping its head to peck.
        private static void PerchedBird(Transform parent, Vector3 at, float facing)
        {
            var root = new GameObject("PerchedBird").transform;
            root.SetParent(parent, false);
            root.localPosition = at;
            root.localRotation = Quaternion.Euler(0f, facing, 0f);
            var body = BirdBody(root, "Bird", gull: false);
            Wobble(body.gameObject, Oscillate.Shape.Pulse, Vector3.zero, new Vector3(38f, 0f, 0f), 2.5f + (float)_random.NextDouble() * 3f, 0.18f);
            MakeDynamic(root.gameObject);
        }

        private static Transform BirdBody(Transform parent, string name, bool gull)
        {
            var bird = new GameObject(name).transform;
            bird.SetParent(parent, false);
            // Exaggerated, as everything in the city is, so a bird reads at a glance from the camera.
            bird.localScale = Vector3.one * (gull ? 2.6f : 1.7f);
            Model(bird, "Body", OfficeModel, "OfficeBirdBody", Toon(gull ? "OfficeGull" : "OfficePigeon", gull ? Gull : Pigeon), Vector3.zero, 0f);
            Model(bird, "Beak", OfficeModel, "OfficeBirdBeak", Toon("OfficeBeak", new Color(0.95f, 0.7f, 0.2f)), Vector3.zero, 0f);
            return bird;
        }

        // A helicopter in the office's helicopter models, its rotor turning.
        private static Transform Helicopter(Transform parent, string name, Color paint, float rotorSpeed)
        {
            var heli = new GameObject(name).transform;
            heli.SetParent(parent, false);
            Model(heli, "Body", OfficeModel, "OfficeHeliBody", Toon($"OfficeHeli_{ColorUtility.ToHtmlStringRGB(paint)}", paint), Vector3.zero, 0f);
            Model(heli, "Glass", OfficeModel, "OfficeHeliGlass", PilotArt.EnsureGlassMaterial("OfficeHeliGlass", new Color(0.55f, 0.75f, 0.92f, 0.7f)), Vector3.zero, 0f);
            Model(heli, "Skids", OfficeModel, "OfficeHeliSkids", Toon("OfficeBrass", Brass), Vector3.zero, 0f);
            var rotor = Model(heli, "Rotor", OfficeModel, "OfficeHeliRotor", Toon("OfficeInk", Ink), Vector3.zero, 0f);
            if (rotor != null)
                rotor.AddComponent<Spin>().Set(Vector3.up, rotorSpeed);
            MakeDynamic(heli.gameObject);
            return heli;
        }

        // A red light that blinks (green for a heliport's edge lights).
        private static void AviationLight(Transform parent, Vector3 at, float period, float phase, bool green = false)
        {
            var holder = new GameObject("AviationLight").transform;
            holder.SetParent(parent, false);
            holder.localPosition = at;
            var lamp = Shape(holder, PrimitiveType.Sphere, "Lamp", Unlit(green ? "OfficeHeliportGreen" : "OfficeAviation", green ? new Color(0.4f, 1f, 0.5f) : AviationRed), Vector3.zero, Vector3.one * 0.22f);
            holder.gameObject.AddComponent<Blink>().Set(lamp, period, green ? 0.5f : 0.25f, phase);
            MakeDynamic(holder.gameObject);
        }

        private static void TwinklingBulb(Transform parent, Material bulb, Vector3 at, float period, float onFraction)
        {
            var holder = new GameObject("StringLight").transform;
            holder.SetParent(parent, false);
            holder.localPosition = at;
            var lamp = Shape(holder, PrimitiveType.Sphere, "Bulb", bulb, Vector3.zero, Vector3.one * 0.09f);
            holder.gameObject.AddComponent<Blink>().Set(lamp, period, onFraction, (float)_random.NextDouble());
            MakeDynamic(holder.gameObject);
        }

        private static void ChaseBulb(Transform parent, Material bulb, Vector3 at, float phase)
        {
            var holder = new GameObject("SignBulb").transform;
            holder.SetParent(parent, false);
            holder.localPosition = at;
            var lamp = Shape(holder, PrimitiveType.Sphere, "Bulb", bulb, Vector3.zero, Vector3.one * 0.16f);
            holder.gameObject.AddComponent<Blink>().Set(lamp, 1.2f, 0.5f, phase);
            MakeDynamic(holder.gameObject);
        }

        // Breathing: a slow, slight rise of the chest, never in step with anyone else.
        private static void Breathe(GameObject person) =>
            Wobble(person, Oscillate.Shape.Wave, Vector3.zero, Vector3.zero, 3.2f + (float)_random.NextDouble(), grow: new Vector3(0.01f, 0.018f, 0.01f));

        private static void Wobble(GameObject target, Oscillate.Shape shape, Vector3 move, Vector3 turn, float period, float duty = 0.2f, Vector3? grow = null)
        {
            if (target == null)
                return;
            target.AddComponent<Oscillate>().Set(shape, move, turn, grow ?? Vector3.zero, period, duty, (float)_random.NextDouble());
            MakeDynamic(target);
        }

        // Things that move must not be statically batched, which would freeze them in place.
        private static void MakeDynamic(GameObject root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.isStatic = false;
        }
    }
}
