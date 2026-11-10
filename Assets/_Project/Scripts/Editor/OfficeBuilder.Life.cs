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
        {
            "Heliport", "Garden", "Crane", "SteakSign", "Pool", "Billboard", "Hoops", "Cleaners",
            "Coop", "Laundry", "Garden", "Billboard", "Cleaners", "Laundry",
        };

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

        // A city window: its glass painted with a room behind it (WindowArt); most switch on and
        // off now and then; a few are televisions. True when it's lit.
        private static bool RegisterWindow(GameObject window)
        {
            if (window == null)
                return false;
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
                return true;
            }
            _cityWindows.Add(renderer);
            var lit = renderer.sharedMaterial != Unlit("OfficeWindowDark", WindowDark);
            var lits = WindowArt.LitWindows();
            renderer.sharedMaterial = lit ? lits[_random.Next(lits.Length)] : WindowArt.DarkWindow();
            return lit;
        }

        // Someone who walks past this lit window now and then (WindowSilhouette): a cut-out figure
        // just inside the glass, its feet at the sill.
        private static void PassingSilhouette(GameObject window)
        {
            var figure = GreyboxAssets.CreatePrimitive(PrimitiveType.Quad, "Silhouette", window.transform, WindowArt.Silhouette(), keepCollider: false);
            figure.transform.localPosition = new Vector3(0f, -0.12f, -0.002f);
            figure.transform.localScale = new Vector3(0.45f, 0.76f, 1f);
            figure.AddComponent<WindowSilhouette>().Set(0.45f, 2.5f + (float)_random.NextDouble() * 2.5f, new Vector2(3f, 14f), _random.Next());
            figure.isStatic = false;
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

        // Builds a roof's special story; true when the roof's middle is still free for an ordinary use.
        private static bool SpecialRoof(string special, Transform roof, float w, float d, Material wall, Material trim)
        {
            switch (special)
            {
                case "Heliport": Heliport(roof, w, d); return false;
                case "Garden": Garden(roof, w, d); return false;
                case "Crane": Crane(roof, w, d); return false;
                case "SteakSign": SteakSign(roof, w, d); return false;
                case "Pool": Pool(roof, w, d); return false;
                case "Laundry": Laundry(roof, w, d, wall, trim); return false;
                case "Billboard": Billboard(roof, w, d); return true;
                case "Hoops": Hoops(roof, w, d); return false;
                case "Coop": Coop(roof, w, d); return false;
                case "Cleaners": Cleaners(roof, w, d); return true;
                default: return true;
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
            Glow(roof, new Vector3(0f, 3f, 0f), new Color(0.9f, 0.95f, 1f), 7f, 2f);
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
            Glow(roof, new Vector3(0f, 2.1f, 0.2f), Bulb, 5f, 2f);

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
            // Tall, but its jib must swing below the penthouse floor, never through the office's view.
            var mastHeight = Mathf.Clamp(-roof.position.y - 4f, 6f, 15f);
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
            Spot(slew, "CraneFlood", new Vector3(10f, -0.5f, 0f), Vector3.zero, CoolWhite, 14f, 50f, 4f);
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
            Glow(roof, new Vector3(0f, 6.2f, -1.8f), Bulb, 6f, 3f);
        }

        // A pool, someone drifting round it on a rubber ring, loungers and an umbrella.
        private static void Pool(Transform roof, float w, float d)
        {
            var pool = new Vector2(w * 0.55f, d * 0.45f);
            Block(roof, "PoolEdge", Toon("OfficePoolEdge", new Color(0.92f, 0.9f, 0.84f)), new Vector3(0f, 0f, -d * 0.05f), new Vector3(pool.x + 0.6f, 0.35f, pool.y + 0.6f));
            // Lit from under the water at night: it glows.
            Cube(roof, "Water", Unlit("OfficePoolWater", new Color(0.3f, 0.78f, 0.9f)), new Vector3(0f, 0.36f, -d * 0.05f), new Vector3(pool.x, 0.01f, pool.y));
            Glow(roof, new Vector3(0f, 1.2f, -d * 0.05f), new Color(0.35f, 0.85f, 1f), 7f, 2.5f);
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
            Smoker(roof, new Vector3(w * 0.15f, 0f, d * 0.2f), 200f);
        }

        // ---- Roofs, in general ---------------------------------------------------------------------

        // A real flat roof (user's note, 2026-11-09: the roofs were too simple): gravel inside a
        // parapet with a coping stone, on top of the cornice. Gives back the place for the roof's
        // things, at the gravel's level.
        private static Transform RoofShell(Transform building, float w, float d, Material trim)
        {
            const float top = 0.35f; // the cornice model's top
            var gravel = Tiles("OfficeRoofGravel", new Color(0.46f, 0.43f, 0.42f), new Color(0.40f, 0.37f, 0.36f), 0.5f, 0.01f);
            Cube(building, "Gravel", gravel, new Vector3(0f, top, 0f), new Vector3(w - 0.3f, 0.02f, d - 0.3f));
            var parapet = Toon("OfficeParapet", new Color(0.52f, 0.47f, 0.45f));
            const float height = 0.6f, thickness = 0.25f;
            foreach (var z in new[] { -1f, 1f })
            {
                Block(building, "Parapet", parapet, new Vector3(0f, top, z * (d / 2f + 0.1f)), new Vector3(w + 0.45f, height, thickness));
                Block(building, "Coping", trim, new Vector3(0f, top + height, z * (d / 2f + 0.1f)), new Vector3(w + 0.55f, 0.07f, thickness + 0.1f));
            }
            foreach (var x in new[] { -1f, 1f })
            {
                Block(building, "Parapet", parapet, new Vector3(x * (w / 2f + 0.1f), top, 0f), new Vector3(thickness, height, d + 0.45f));
                Block(building, "Coping", trim, new Vector3(x * (w / 2f + 0.1f), top + height, 0f), new Vector3(thickness + 0.1f, 0.07f, d + 0.55f));
            }
            var roof = new GameObject("Roof").transform;
            roof.SetParent(building, false);
            roof.localPosition = Vector3.up * (top + 0.02f);
            return roof;
        }

        // The middle ring's roofs, further off in the haze: a plant room and a unit or a tank.
        private static void SimpleRoof(Transform roof, float w, float d, Material wall, Material trim)
        {
            PlantRoom(roof, new Vector3(-w * 0.25f, 0f, d * 0.2f), wall, trim, antenna: false);
            if (_random.NextDouble() < 0.4)
                WaterTower(roof, new Vector3(w * 0.2f, 0f, -d * 0.1f));
            else
                Block(roof, "AirCon", Toon("OfficeRoofGrey", new Color(0.56f, 0.55f, 0.53f)), new Vector3(w * 0.2f, 0f, -d * 0.1f), new Vector3(1.8f, 0.9f, 1.2f));
        }

        // The machinery round a near roof's edges: a plant room with its door and louvres, air
        // conditioners with spinning fans and a duct, an exhaust stack breathing steam, turbine
        // vents turning in the wind, a satellite dish, a hatch, pipes; sometimes someone fixing a unit.
        private static void RoofKit(Transform roof, float w, float d, Material wall, Material trim)
        {
            var grey = Toon("OfficeRoofGrey", new Color(0.56f, 0.55f, 0.53f));
            var metal = Toon("OfficeRoofMetal", new Color(0.7f, 0.71f, 0.72f));
            var room = new Vector3(-w / 2f + 1.7f, 0f, d / 2f - 1.4f);
            PlantRoom(roof, room, wall, trim, antenna: _random.NextDouble() < 0.6);

            // Air conditioners along the back, a duct running into the plant room.
            var units = 2 + _random.Next(2);
            var firstUnit = room + new Vector3(2.4f, 0f, 0.2f);
            for (var k = 0; k < units && firstUnit.x + k * 1.7f < w / 2f - 1.2f; k++)
            {
                var unit = firstUnit + new Vector3(k * 1.7f, 0f, 0f);
                Block(roof, "AirCon", grey, unit, new Vector3(1.3f, 0.8f, 1.0f));
                Block(roof, "AirConGrille", Toon("OfficeInk", Ink), unit + new Vector3(0f, 0.2f, -0.51f), new Vector3(1.0f, 0.4f, 0.02f));
                Shape(roof, PrimitiveType.Cylinder, "FanWell", Toon("OfficeInk", Ink), unit + Vector3.up * 0.81f, new Vector3(0.7f, 0.01f, 0.7f));
                SpinningFan(roof, unit + Vector3.up * 0.83f);
            }
            Block(roof, "Duct", metal, room + new Vector3(1.3f, 0.55f, 0.2f), new Vector3(1.4f, 0.35f, 0.35f));

            // The exhaust stack at the front corner, steaming.
            var stack = new Vector3(w / 2f - 1.2f, 0f, -d / 2f + 1.3f);
            Shape(roof, PrimitiveType.Cylinder, "Stack", metal, stack + Vector3.up * 1.1f, new Vector3(0.45f, 1.1f, 0.45f));
            Shape(roof, PrimitiveType.Cylinder, "StackCap", Toon("OfficeInk", Ink), stack + Vector3.up * 2.25f, new Vector3(0.6f, 0.05f, 0.6f));
            Steam(roof, stack + Vector3.up * 2.3f);

            // Turbine vents along the side.
            var vents = 2 + _random.Next(2);
            for (var k = 0; k < vents; k++)
                TurbineVent(roof, new Vector3(w / 2f - 1.0f, 0f, -d / 2f + 3.0f + k * 1.3f), metal);

            // A satellite dish on the front edge, turned to the south.
            Dish(roof, new Vector3(-w / 2f + 1.3f, 0f, -d / 2f + 1.3f));

            // A hatch and a run of pipes on low supports.
            Block(roof, "Hatch", grey, new Vector3(0f, 0f, -d / 2f + 1.2f), new Vector3(0.9f, 0.35f, 0.9f));
            var pipeZ = -d / 2f + 2.4f;
            Shape(roof, PrimitiveType.Cylinder, "Pipe", Toon("OfficePipe", new Color(0.62f, 0.38f, 0.26f)), new Vector3(-w * 0.1f, 0.35f, pipeZ), new Vector3(0.18f, w * 0.3f, 0.18f), Quaternion.Euler(0f, 0f, 90f));
            for (var x = -w * 0.35f; x < w * 0.15f; x += 1.6f)
                Block(roof, "PipeSupport", grey, new Vector3(x, 0f, pipeZ), new Vector3(0.12f, 0.26f, 0.3f));

            // Now and then someone in a hard hat at work on a unit.
            if (_random.NextDouble() < 0.35)
            {
                var worker = Person(roof, "Engineer", firstUnit + new Vector3(0f, 0f, -1.1f), 0f, new Color(1f, 0.55f, 0.15f), new Color(0.25f, 0.3f, 0.45f), 1f,
                    look => { look.Hat = CharacterArt.HatKind.Cap; look.HatColor = new Color(0.98f, 0.82f, 0.2f); });
                Wobble(worker.gameObject, Oscillate.Shape.Pulse, Vector3.zero, new Vector3(22f, 0f, 0f), 2.2f, 0.5f);
            }
        }

        // The roof's middle when it has no special story: a deck with someone smoking, solar
        // panels, a water tower, or left as gravel with a skylight.
        private static void CentrePiece(Transform roof, float w, float d, Material wall, Material trim)
        {
            var kind = _random.NextDouble();
            if (kind < 0.35)
            {
                var deck = new Vector3(w * 0.05f, 0f, -d * 0.02f);
                var deckSize = new Vector2(w * 0.42f, d * 0.4f);
                Cube(roof, "Deck", Planks("OfficeDecking", new Color(0.45f, 0.38f, 0.32f), new Color(0.27f, 0.22f, 0.19f), 0.14f, 2.4f),
                    deck, new Vector3(deckSize.x, 0.04f, deckSize.y));
                var rail = Toon("OfficeRoofRail", new Color(0.24f, 0.24f, 0.26f));
                foreach (var z in new[] { -deckSize.y / 2f, deckSize.y / 2f })
                    Block(roof, "Railing", rail, deck + new Vector3(0f, 0.95f, z), new Vector3(deckSize.x, 0.05f, 0.05f));
                foreach (var x in new[] { -deckSize.x / 2f, deckSize.x / 2f })
                    Block(roof, "Railing", rail, deck + new Vector3(x, 0.95f, 0f), new Vector3(0.05f, 0.05f, deckSize.y));
                var u = _random.Next(UmbrellaColors.Length);
                Model(roof, "UmbrellaPole", OfficeModel, "OfficeUmbrellaPole", Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f)), deck + new Vector3(deckSize.x * 0.2f, 0f, 0f), 0f);
                Model(roof, "UmbrellaCanopy", OfficeModel, "OfficeUmbrellaCanopy", Toon($"OfficeUmbrella{u}", UmbrellaColors[u]), deck + new Vector3(deckSize.x * 0.2f, 0f, 0f), 22.5f);
                foreach (var x in new[] { -0.35f, 0.35f })
                {
                    var box = deck + new Vector3(x * deckSize.x, 0f, deckSize.y / 2f - 0.35f);
                    Block(roof, "PlanterBox", Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f)), box, new Vector3(1.2f, 0.45f, 0.45f));
                    Block(roof, "Shrubs", Toon("OfficePlant", Plant), box + Vector3.up * 0.45f, new Vector3(1.1f, 0.3f, 0.38f));
                }
                Smoker(roof, deck + new Vector3(-deckSize.x * 0.25f, 0f, -deckSize.y * 0.15f), (float)_random.NextDouble() * 360f);
                Glow(roof, deck + new Vector3(deckSize.x * 0.2f, 2f, 0f), Bulb, 5f, 1.6f);
            }
            else if (kind < 0.6)
            {
                var navy = Toon("OfficeSolar", new Color(0.16f, 0.22f, 0.36f));
                var frameGrey = Toon("OfficeRoofGrey", new Color(0.56f, 0.55f, 0.53f));
                for (var r = 0; r < 2; r++)
                for (var k = 0; k < 3; k++)
                {
                    var spot = new Vector3(-w * 0.15f + k * 2.0f, 0.3f, -d * 0.05f + r * 1.8f);
                    var panel = Block(roof, "SolarPanel", navy, spot, new Vector3(1.8f, 0.05f, 1.1f));
                    if (panel != null) panel.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
                    Block(roof, "SolarLeg", frameGrey, spot + new Vector3(0f, -0.3f, 0.3f), new Vector3(1.6f, 0.45f, 0.06f));
                }
            }
            else if (kind < 0.8)
            {
                WaterTower(roof, new Vector3(w * 0.05f, 0f, 0f));
            }
            else
            {
                Block(roof, "SkylightFrame", trim, new Vector3(w * 0.05f, 0f, 0f), new Vector3(2.4f, 0.35f, 1.6f));
                Cube(roof, "SkylightGlow", Unlit("OfficeWindowLit", WindowLit), new Vector3(w * 0.05f, 0.35f, 0f), new Vector3(2.1f, 0.01f, 1.3f));
            }
        }

        // A plant room: the box that houses the lifts' motors and the roof's stair, with a door,
        // louvres and maybe a mast with a blinking light.
        private static void PlantRoom(Transform roof, Vector3 at, Material wall, Material trim, bool antenna)
        {
            Block(roof, "PlantRoom", wall, at, new Vector3(2.8f, 2.3f, 2.0f));
            Block(roof, "PlantRoomRoof", trim, at + Vector3.up * 2.3f, new Vector3(3.0f, 0.12f, 2.2f));
            Block(roof, "Door", Toon("OfficeRoofDoor", new Color(0.32f, 0.36f, 0.4f)), at + new Vector3(-0.6f, 0f, -1.01f), new Vector3(0.8f, 1.9f, 0.05f));
            for (var y = 0.9f; y < 1.9f; y += 0.18f)
                Block(roof, "Louvre", Toon("OfficeInk", Ink), at + new Vector3(0.6f, y, -1.02f), new Vector3(0.8f, 0.06f, 0.03f));
            if (!antenna)
                return;
            Block(roof, "Mast", Toon("OfficeInk", Ink), at + new Vector3(0.9f, 2.4f, 0.5f), new Vector3(0.08f, 3.2f, 0.08f));
            AviationLight(roof, at + new Vector3(0.9f, 5.7f, 0.5f), 1.4f + (float)_random.NextDouble(), (float)_random.NextDouble());
        }

        // A turbine vent: a ribbed head that the wind keeps turning.
        private static void TurbineVent(Transform roof, Vector3 at, Material metal)
        {
            Shape(roof, PrimitiveType.Cylinder, "VentNeck", metal, at + Vector3.up * 0.3f, new Vector3(0.25f, 0.3f, 0.25f));
            var head = new GameObject("TurbineHead").transform;
            head.SetParent(roof, false);
            head.localPosition = at + Vector3.up * 0.75f;
            Shape(head, PrimitiveType.Sphere, "Dome", metal, Vector3.zero, new Vector3(0.5f, 0.4f, 0.5f));
            for (var k = 0; k < 6; k++)
                Block(head, "Rib", Toon("OfficeInk", Ink), Vector3.down * 0.15f, new Vector3(0.52f, 0.3f, 0.03f)).transform.localRotation = Quaternion.Euler(0f, k * 30f, 0f);
            head.gameObject.AddComponent<Spin>().Set(Vector3.up, 90f + (float)_random.NextDouble() * 120f);
            MakeDynamic(head.gameObject);
        }

        private static void Dish(Transform roof, Vector3 at)
        {
            var grey = Toon("OfficeRoofMetal", new Color(0.7f, 0.71f, 0.72f));
            Block(roof, "DishPole", Toon("OfficeInk", Ink), at, new Vector3(0.08f, 0.9f, 0.08f));
            var dish = Shape(roof, PrimitiveType.Sphere, "Dish", Toon("OfficeDish", new Color(0.9f, 0.9f, 0.88f)), at + Vector3.up * 1.05f, new Vector3(0.9f, 0.9f, 0.22f));
            dish.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            Block(roof, "DishArm", grey, at + new Vector3(0f, 1.1f, -0.25f), new Vector3(0.04f, 0.04f, 0.5f));
        }

        // Steam from an exhaust stack: slow, soft, large puffs drifting up.
        private static void Steam(Transform roof, Vector3 at)
        {
            var holder = new GameObject("SteamFrom").transform;
            holder.SetParent(roof, false);
            holder.localPosition = at;
            var steam = Puffs(holder, "Steam", 0f, 4f, 0.7f, 0.4f, 0.8f);
            var emission = steam.emission;
            emission.rateOverTime = 1.5f + (float)_random.NextDouble() * 1.5f;
            var main = steam.main;
            main.startColor = new Color(0.95f, 0.95f, 0.97f, 0.55f);
        }

        // ---- More roof stories ------------------------------------------------------------------

        // A billboard on steel legs at the back of the roof, with a giant burger and a ring of
        // chasing bulbs, lit by two lamps.
        private static void Billboard(Transform roof, float w, float d)
        {
            var steel = Toon("OfficeInk", Ink);
            var board = new Vector3(w * 0.1f, 1.6f, d * 0.22f);
            foreach (var x in new[] { -2.6f, 2.6f })
                Block(roof, "BillboardLeg", steel, new Vector3(board.x + x, 0f, board.z + 0.3f), new Vector3(0.18f, board.y, 0.18f));
            var colours = new[] { new Color(0.98f, 0.82f, 0.3f), new Color(0.35f, 0.65f, 0.85f), new Color(0.95f, 0.5f, 0.45f) };
            var face = colours[_random.Next(colours.Length)];
            Block(roof, "BillboardBack", steel, board + Vector3.forward * 0.15f, new Vector3(6.6f, 3.4f, 0.12f));
            Block(roof, "BillboardFace", Toon($"OfficeBillboard{ColorUtility.ToHtmlStringRGB(face)}", face), board, new Vector3(6.4f, 3.2f, 0.12f));
            // The burger, seen from the side: a domed bun, lettuce, a patty, cheese, the base.
            var burger = new GameObject("Burger").transform;
            burger.SetParent(roof, false);
            burger.localPosition = board + new Vector3(0f, 1.6f, -0.1f);
            Shape(burger, PrimitiveType.Sphere, "BunTop", Toon("OfficeBun", new Color(0.88f, 0.6f, 0.3f)), new Vector3(0f, 0.45f, 0f), new Vector3(2.6f, 1.2f, 0.2f));
            Shape(burger, PrimitiveType.Cube, "Lettuce", Toon("OfficeLettuce", new Color(0.45f, 0.75f, 0.3f)), new Vector3(0f, 0f, 0f), new Vector3(2.7f, 0.22f, 0.2f));
            Shape(burger, PrimitiveType.Cube, "Cheese", Toon("OfficeCheese", new Color(1f, 0.82f, 0.25f)), new Vector3(0f, -0.2f, -0.01f), new Vector3(2.6f, 0.18f, 0.2f));
            Shape(burger, PrimitiveType.Cube, "Patty", Toon("OfficePatty", new Color(0.42f, 0.22f, 0.14f)), new Vector3(0f, -0.45f, 0f), new Vector3(2.5f, 0.38f, 0.2f));
            Shape(burger, PrimitiveType.Cube, "BunBase", Toon("OfficeBun", new Color(0.88f, 0.6f, 0.3f)), new Vector3(0f, -0.8f, 0f), new Vector3(2.5f, 0.35f, 0.2f));
            Wobble(burger.gameObject, Oscillate.Shape.Wave, new Vector3(0f, 0.08f, 0f), Vector3.zero, 1.6f, grow: new Vector3(0.04f, 0.04f, 0f));
            var bulb = Unlit("OfficeBulb", Bulb);
            const int across = 12, up = 5;
            var index = 0;
            var total = 2 * (across + up);
            for (var i = 0; i < across; i++)
            {
                var x = -3.1f + 6.2f * i / (across - 1);
                ChaseBulb(roof, bulb, board + new Vector3(x, 1.55f, -0.08f), (float)index++ / total);
                ChaseBulb(roof, bulb, board + new Vector3(-x, -1.55f, -0.08f), (float)index++ / total);
            }
            for (var i = 1; i < up; i++)
            {
                var y = -1.55f + 3.1f * i / up;
                ChaseBulb(roof, bulb, board + new Vector3(3.1f, -y, -0.08f), (float)index++ / total);
                ChaseBulb(roof, bulb, board + new Vector3(-3.1f, y, -0.08f), (float)index++ / total);
            }
            Spot(roof, "BillboardFlood", board + new Vector3(0f, -1.6f, -2.2f), new Vector3(-60f, 0f, 0f), Showroom, 7f, 70f, 6f);
            foreach (var x in new[] { -1.8f, 1.8f })
                Shape(roof, PrimitiveType.Cylinder, "Floodlight", Unlit("OfficeLampGlow", new Color(1f, 0.86f, 0.55f)), board + new Vector3(x, -1.7f, -0.7f), new Vector3(0.3f, 0.12f, 0.3f), Quaternion.Euler(-60f, 0f, 0f));
        }

        // A basketball half court: painted lines, a hoop, one player dribbling and one guarding.
        private static void Hoops(Transform roof, float w, float d)
        {
            Cube(roof, "Court", Tiles("OfficeCourt", new Color(0.78f, 0.45f, 0.3f), new Color(0.95f, 0.92f, 0.85f), 4f, 0.06f),
                Vector3.zero, new Vector3(w * 0.7f, 0.02f, d * 0.6f));
            Cube(roof, "Key", Toon("OfficeCourtKey", new Color(0.25f, 0.42f, 0.6f)), new Vector3(0f, 0.025f, d * 0.18f), new Vector3(2.4f, 0.01f, d * 0.22f));
            var hoop = new Vector3(0f, 0f, d * 0.29f);
            Block(roof, "HoopPole", Toon("OfficeInk", Ink), hoop, new Vector3(0.14f, 3.2f, 0.14f));
            Block(roof, "Backboard", Toon("OfficeDish", new Color(0.95f, 0.95f, 0.93f)), hoop + new Vector3(0f, 2.9f, -0.2f), new Vector3(1.6f, 1.0f, 0.06f));
            Shape(roof, PrimitiveType.Cylinder, "Rim", Toon("OfficeRim", new Color(0.95f, 0.4f, 0.15f)), hoop + new Vector3(0f, 3.0f, -0.55f), new Vector3(0.5f, 0.02f, 0.5f));
            var player = Person(roof, "Player", new Vector3(-0.8f, 0f, -0.4f), 20f, new Color(0.95f, 0.85f, 0.2f), new Color(0.2f, 0.25f, 0.6f));
            Wobble(player.gameObject, Oscillate.Shape.Wave, new Vector3(0.25f, 0f, 0f), new Vector3(0f, 12f, 0f), 2.4f);
            var ball = new GameObject("Dribble").transform;
            ball.SetParent(player, false);
            ball.localPosition = new Vector3(0.35f, 0.12f, 0.35f);
            Shape(ball, PrimitiveType.Sphere, "Ball", Toon("OfficeRim", new Color(0.95f, 0.4f, 0.15f)), Vector3.zero, Vector3.one * 0.24f);
            Wobble(ball.gameObject, Oscillate.Shape.Pulse, new Vector3(0f, 0.75f, 0f), Vector3.zero, 0.55f, 1f);
            var guard = Person(roof, "Guard", new Vector3(0.1f, 0f, 0.7f), 200f, new Color(0.9f, 0.3f, 0.3f), new Color(0.15f, 0.15f, 0.18f));
            Wobble(guard.gameObject, Oscillate.Shape.Wave, new Vector3(0.3f, 0f, 0f), Vector3.zero, 2.4f);
        }

        // A pigeon keeper's loft: a wooden coop, the keeper waving a flag on a pole, the flock
        // wheeling tight overhead.
        private static void Coop(Transform roof, float w, float d)
        {
            var wood = Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f));
            var coop = new Vector3(w * 0.1f, 0f, d * 0.15f);
            Block(roof, "Coop", wood, coop, new Vector3(3.2f, 1.8f, 1.8f));
            Block(roof, "CoopRoof", Toon("OfficeRoofGrey", new Color(0.56f, 0.55f, 0.53f)), coop + Vector3.up * 1.8f, new Vector3(3.4f, 0.15f, 2.0f));
            for (var x = -1.3f; x <= 1.31f; x += 0.65f)
                Block(roof, "CoopBar", Toon("OfficeInk", Ink), coop + new Vector3(x, 0.4f, -0.91f), new Vector3(0.04f, 1.2f, 0.03f));
            foreach (var x in new[] { -1f, 0f, 1f })
                PerchedBird(roof, coop + new Vector3(x, 1.95f, 0f), (float)_random.NextDouble() * 360f);
            var keeper = Person(roof, "Keeper", coop + new Vector3(-0.5f, 0f, -1.8f), 160f, new Color(0.5f, 0.45f, 0.35f), new Color(0.25f, 0.22f, 0.2f));
            var flag = new GameObject("FlagPole").transform;
            flag.SetParent(keeper, false);
            flag.localPosition = new Vector3(0.35f, 1.0f, 0.1f);
            Block(flag, "Stick", Toon("OfficeInk", Ink), Vector3.zero, new Vector3(0.04f, 2.2f, 0.04f));
            Block(flag, "Cloth", Toon("OfficeFlag", new Color(0.9f, 0.2f, 0.2f)), new Vector3(0.35f, 1.7f, 0f), new Vector3(0.7f, 0.45f, 0.02f));
            Wobble(flag.gameObject, Oscillate.Shape.Wave, Vector3.zero, new Vector3(0f, 0f, 35f), 1.4f);
            Flock(roof, coop + Vector3.up * 5f, new Vector2(5f, 4f), 9, 7f, gull: false);
        }

        // Window cleaners: a davit arm over the roof's front edge and a gondola with two cleaners
        // riding slowly up and down the facade.
        private static void Cleaners(Transform roof, float w, float d)
        {
            var steel = Toon("OfficeRoofMetal", new Color(0.7f, 0.71f, 0.72f));
            var front = -d / 2f;
            foreach (var x in new[] { -1.3f, 1.3f })
            {
                Block(roof, "Davit", steel, new Vector3(x, 0f, front + 0.8f), new Vector3(0.14f, 1.8f, 0.14f));
                Block(roof, "DavitArm", steel, new Vector3(x, 1.8f, front + 0.1f), new Vector3(0.14f, 0.14f, 1.6f));
            }
            var gondola = new GameObject("Gondola").transform;
            gondola.SetParent(roof, false);
            gondola.localPosition = new Vector3(0f, -6f, front - 0.75f);
            Block(gondola, "Cradle", Toon("OfficeCrane", CraneYellow), Vector3.zero, new Vector3(3f, 0.15f, 0.8f));
            Block(gondola, "CradleRailFront", Toon("OfficeCrane", CraneYellow), new Vector3(0f, 0.15f, -0.38f), new Vector3(3f, 0.9f, 0.05f));
            foreach (var x in new[] { -1.3f, 1.3f })
                Block(gondola, "Cable", Toon("OfficeInk", Ink), new Vector3(x, 0.1f, 0.2f), new Vector3(0.03f, 8f, 0.03f));
            foreach (var (x, top) in new[] { (-0.7f, new Color(0.3f, 0.5f, 0.75f)), (0.7f, new Color(0.95f, 0.55f, 0.2f)) })
            {
                var cleaner = Person(gondola, "Cleaner", new Vector3(x, 0.15f, 0.05f), 0f, top, new Color(0.22f, 0.24f, 0.3f), 1f,
                    look => { look.Hat = CharacterArt.HatKind.Cap; look.HatColor = new Color(0.98f, 0.82f, 0.2f); });
                // Wiping the glass, side to side.
                Wobble(cleaner.gameObject, Oscillate.Shape.Wave, Vector3.zero, new Vector3(0f, 25f, 0f), 1.3f);
            }
            Wobble(gondola.gameObject, Oscillate.Shape.Wave, new Vector3(0f, 4.5f, 0f), Vector3.zero, 26f);
        }

        // ---- Over the whole scene -----------------------------------------------------------------

        // Called after the rooms and the city are built.
        private static void AddLife(Transform rooms)
        {
            var city = GameObject.Find("City").transform;
            var life = new GameObject("Life").transform;
            life.SetParent(city, false);

            // Windows switching on and off, and the televisions.
            city.gameObject.AddComponent<WindowFlicker>().Set(_cityWindows.ToArray(), WindowArt.LitWindows(), WindowArt.DarkWindow(), _televisions.ToArray());

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
            // Its glow goes out with it (a child of the bulb).
            Kit.Halo(lamp.transform, Vector3.zero, 0.55f, Bulb, 0.7f);
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
