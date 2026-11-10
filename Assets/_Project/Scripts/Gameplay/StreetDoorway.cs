using System.Collections.Generic;
using Game.Gameplay.Stations;
using Game.Simulation.Movement;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Opens the outer wall where the street door stands, so chefs walk out to the street's stalls
    /// and back (Phase 5, milestone 3). The door moves along the walls in management mode; the wall
    /// is split around it again wherever it goes. Pictures hung where the doorway now is come down
    /// until it moves on.
    /// </summary>
    public sealed class StreetDoorway : MonoBehaviour
    {
        // How far the door's root may be from a wall to count as standing on it, in metres.
        private const float OnWallDistance = 0.6f;

        [SerializeField] private StreetDoor _door;
        [Tooltip("The outer walls the door can stand on, as plain boxes square to the room.")]
        [SerializeField] private Transform[] _walls = new Transform[0];
        [Tooltip("The doorway's width, in metres.")]
        [SerializeField, Min(0.5f)] private float _width = 1.2f;
        [Tooltip("Things hung on those walls: hidden while the doorway is where they hang.")]
        [SerializeField] private Transform[] _onWalls = new Transform[0];

        private readonly List<GameObject> _pieces = new List<GameObject>();
        private Vector3 _builtFor = new Vector3(float.NaN, 0f, 0f);

        private void Start() => Rebuild();

        private void LateUpdate()
        {
            if (_door != null && (_door.transform.position - _builtFor).sqrMagnitude > 1e-6f)
                Rebuild();
        }

        private void Rebuild()
        {
            if (_door == null)
                return;
            var at = _door.transform.position;
            _builtFor = at;

            foreach (var piece in _pieces)
                Destroy(piece);
            _pieces.Clear();
            foreach (var wall in _walls)
                SetShown(wall.gameObject, true);
            foreach (var thing in _onWalls)
                if (thing != null)
                    thing.gameObject.SetActive(true);

            var opened = Nearest(at);
            if (opened == null)
                return;

            var centre = opened.position;
            var size = opened.lossyScale;
            var alongX = size.x >= size.z;
            var half = (alongX ? size.x : size.z) / 2f;
            var middle = alongX ? centre.x : centre.z;
            var along = alongX ? at.x : at.z;

            SetShown(opened.gameObject, false);
            foreach (var (from, to) in DoorwayGap.Split(middle - half, middle + half, along, _width))
            {
                var piece = Instantiate(opened.gameObject, opened.parent);
                piece.name = $"{opened.name}_Piece";
                SetShown(piece, true);
                var length = to - from;
                var mid = (from + to) / 2f;
                var local = opened.localScale;
                piece.transform.position = alongX ? new Vector3(mid, centre.y, centre.z) : new Vector3(centre.x, centre.y, mid);
                piece.transform.localScale = alongX
                    ? new Vector3(length / size.x * local.x, local.y, local.z)
                    : new Vector3(local.x, local.y, length / size.z * local.z);
                _pieces.Add(piece);
            }

            // Down from where the doorway is: something hung there would float in the opening.
            foreach (var thing in _onWalls)
            {
                if (thing == null)
                    continue;
                var p = thing.position;
                var across = alongX ? Mathf.Abs(p.z - centre.z) : Mathf.Abs(p.x - centre.x);
                var offset = Mathf.Abs((alongX ? p.x : p.z) - along);
                if (across < OnWallDistance && offset < _width)
                    thing.gameObject.SetActive(false);
            }
        }

        // The wall the door stands on, or null if it's on none.
        private Transform Nearest(Vector3 at)
        {
            Transform best = null;
            var bestDistance = OnWallDistance;
            foreach (var wall in _walls)
            {
                if (wall == null)
                    continue;
                var c = wall.position;
                var s = wall.lossyScale / 2f;
                var dx = Mathf.Max(0f, Mathf.Abs(at.x - c.x) - s.x);
                var dz = Mathf.Max(0f, Mathf.Abs(at.z - c.z) - s.z);
                var distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = wall;
                }
            }
            return best;
        }

        private static void SetShown(GameObject wall, bool shown)
        {
            foreach (var renderer in wall.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = shown;
            foreach (var collider in wall.GetComponentsInChildren<Collider>(true))
                collider.enabled = shown;
        }
    }
}
