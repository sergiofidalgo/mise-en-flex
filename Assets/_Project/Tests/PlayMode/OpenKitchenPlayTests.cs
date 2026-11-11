using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Rearranging after the decisions of 2026-11-11: no piece shows inside a wall while carried, at
    /// any of the eight turns, and appliances may stand in the dining room and tables in the kitchen,
    /// where they're listed as out of their room.
    /// </summary>
    public class OpenKitchenPlayTests
    {
        private const string SceneName = "Kitchen_Greybox";
        // How far a model may reach past its footprint into a wall. Cabinets' worktops overhang their
        // 1 m body by 2.5 cm (CabinetArt), as in the kitchen the builder lays out, flush to the walls:
        // hidden inside a 20 cm wall at counter height. Anything more shows.
        private const float Tolerance = 0.03f;

        private KitchenCompositionRoot _root;
        private LayoutEditor _editor;
        private LayoutEditor.Cursor _cursor;

        private IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            var player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;
            _editor = Object.FindFirstObjectByType<LayoutEditor>();
            _cursor = _editor.Enter(player);
        }

        [UnityTest]
        public IEnumerator NoPieceShowsInsideAWallWhileCarriedAtAnyTurn()
        {
            yield return Open();
            var floor = _root.Services.FloorPlan;
            // The door belongs on a wall: its own rule puts it there.
            var pieces = _root.Services.Stations.All.Where(s => !(s is StallStation) && !(s is StreetDoor)).ToList();
            Assert.That(pieces, Is.Not.Empty);

            // Each piece's worst overhang, so one run names every piece that clips.
            var problems = new Dictionary<string, (float By, string Where)>();
            foreach (var station in pieces)
            {
                _cursor.Position = Floor(station.transform.position);
                Assert.That(_editor.PickUp(_cursor), Is.True, station.name);
                var piece = _cursor.Held;
                var start = _cursor.Position;
                var pushes = new[]
                {
                    new SimVector2(20f, start.Y), new SimVector2(-20f, start.Y), new SimVector2(start.X, 20f), new SimVector2(start.X, -20f),
                    new SimVector2(start.X, -2.95f), new SimVector2(start.X, -3.05f), new SimVector2(20f, 20f), new SimVector2(-20f, -20f),
                };
                for (var turn = 0; turn < 8; turn++)
                {
                    foreach (var push in pushes)
                    {
                        _cursor.Position = push;
                        _editor.MoveCursor(_cursor, SimVector2.Zero);
                        // The look eases round; measure it at the angle it's easing to.
                        piece.transform.rotation = Quaternion.Euler(0f, _cursor.Degrees, 0f);
                        Physics.SyncTransforms();
                        var (by, side) = Overhang(piece, floor);
                        if (by > Tolerance && (!problems.TryGetValue(piece.name, out var worst) || by > worst.By))
                            problems[piece.name] = (by, $"{side} at {_cursor.Degrees}°, pushed to {push}");
                    }
                    _editor.Rotate(_cursor, 45f);
                }
                _editor.Cancel(_cursor);
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems.Select(p => $"{p.Key}: {p.Value.By:0.000} m {p.Value.Where}")));
        }

        [UnityTest]
        public IEnumerator AHobCanGoInTheDiningRoomAndIsListedAsOutOfItsRoom()
        {
            yield return Open();
            var hob = _root.Services.Stations.All.OfType<HobStation>().First();
            Assert.That(_editor.OutOfTheirRooms(), Is.Empty, "the greybox starts with everything in its room");

            _cursor.Position = Floor(hob.transform.position);
            Assert.That(_editor.PickUp(_cursor), Is.True);
            Assert.That(PlaceSomewhereIn(_root.Services.FloorPlan.DiningAreaMin, _root.Services.FloorPlan.DiningAreaMax), Is.True, "nowhere free in the dining room");

            Assert.That(_editor.OutOfTheirRooms(), Is.EquivalentTo(new Interactable[] { hob }));
            Assert.That(_editor.IsOutOfItsRoom(hob), Is.True);
        }

        [UnityTest]
        public IEnumerator ATableCanGoInTheKitchenAndIsListedAsOutOfItsRoom()
        {
            yield return Open();
            _root.Services.Days.Receive(500f);
            _cursor.Position = new SimVector2(0f, 0f);
            Assert.That(_editor.Buy(_cursor, _editor.Shop.First(e => e.Id == "table")), Is.True);
            var table = (TableStation)_cursor.Held;
            Assert.That(PlaceSomewhereIn(_root.Services.FloorPlan.KitchenAreaMin, _root.Services.FloorPlan.KitchenAreaMax), Is.True, "nowhere free in the kitchen");

            Assert.That(_editor.OutOfTheirRooms(), Does.Contain(table));
            Assert.That(_editor.IsOutOfItsRoom(table), Is.True);
        }

        // Tries the room's floor in a grid until the carried piece can go down, warning of mess.
        private bool PlaceSomewhereIn(SimVector2 min, SimVector2 max)
        {
            for (var x = min.X; x <= max.X; x += 0.25f)
            {
                for (var y = min.Y; y <= max.Y; y += 0.25f)
                {
                    _cursor.Position = new SimVector2(x, y);
                    _editor.MoveCursor(_cursor, SimVector2.Zero);
                    if (_cursor.Result != Game.Simulation.Movement.Placement.Ok)
                        continue;
                    Assert.That(_cursor.OutOfItsRoom, Is.True, $"at {_cursor.Landing} it should warn of mess");
                    return _editor.Place(_cursor);
                }
            }
            return false;
        }

        // How far the piece's own models reach into a wall, and which wall and model: the outer
        // walls (beyond the rooms' faces) or an inside wall, but not a door's gap in it.
        internal static (float By, string Side) Overhang(Interactable piece, Game.Data.FloorPlanConfig floor)
        {
            var half = floor.StationHalfSize;
            var west = floor.KitchenAreaMin.X - half;
            var east = floor.KitchenAreaMax.X + half;
            var north = floor.KitchenAreaMax.Y + half;
            var south = floor.DiningAreaMin.Y - half;
            var worst = (By: float.MinValue, Side: (string)null);
            foreach (var renderer in piece.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled))
            {
                var bounds = renderer.bounds;
                var sides = new List<(float, string)>
                {
                    (west - bounds.min.x, "west"), (bounds.max.x - east, "east"),
                    (south - bounds.min.z, "south"), (bounds.max.z - north, "north"),
                };
                foreach (var wall in floor.Walls)
                {
                    var intoX = Mathf.Min(bounds.max.x - wall.xMin, wall.xMax - bounds.min.x);
                    var intoZ = Mathf.Min(bounds.max.z - wall.yMin, wall.yMax - bounds.min.z);
                    if (intoX > 0f && intoZ > 0f)
                        sides.Add((Mathf.Min(intoX, intoZ), $"inside wall at {wall}"));
                }
                foreach (var side in sides)
                {
                    if (side.Item1 > worst.By)
                        worst = (side.Item1, $"{side.Item2} ({renderer.name})");
                }
            }
            return worst.Side == null ? (0f, null) : worst;
        }

        private static SimVector2 Floor(Vector3 position) => new SimVector2(position.x, position.z);
    }
}
