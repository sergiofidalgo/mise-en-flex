using Game.Data;
using Game.Gameplay.Saving;
using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The chef in the opening (user's notes, 2026-11-10): waits by a taxi at the kerb while the
    /// menu is up, wearing player 1's look from the tailor and their colour; once a campaign is
    /// chosen, walks in through the tower's door, round to the lift's doors, which slide open, and
    /// steps in to ride up. Upstairs, player 1 comes out of the floor's elevator: the same chef.
    /// </summary>
    public sealed class OpeningChef : MonoBehaviour
    {
        [Tooltip("The walk, in world space: from where the chef waits to inside the cab.")]
        [SerializeField] private Vector3[] _path = System.Array.Empty<Vector3>();
        [SerializeField, Min(0.3f)] private float _speed = 2.3f;
        [Tooltip("The lift's cab: the chef rides in it once in.")]
        [SerializeField] private Transform _cab;
        [Tooltip("The cab's two door panels, which slide apart as the chef comes.")]
        [SerializeField] private Transform[] _doors = System.Array.Empty<Transform>();
        [SerializeField, Min(0f)] private float _doorTravel = 0.85f;
        [Tooltip("How far from the end of the walk the doors start opening (m).")]
        [SerializeField, Min(0f)] private float _doorsFrom = 4f;
        [SerializeField] private ChefWardrobe _wardrobe;
        [Tooltip("The parts in the player's colour (toque band, apron, neckerchief).")]
        [SerializeField] private Renderer[] _tinted = System.Array.Empty<Renderer>();
        [SerializeField] private CoopSettings _coop;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private float _walked = -1f;
        private float _length;
        private Vector3[] _doorRest;

        /// <summary>The chef is in the cab.</summary>
        public bool Aboard { get; private set; }

        /// <summary>Where the chef's head is, for the camera to watch.</summary>
        public Vector3 Head => transform.position + Vector3.up * 1.4f;

        private void Start()
        {
            _length = OpeningPath.Length(_path);
            _doorRest = new Vector3[_doors.Length];
            for (var i = 0; i < _doors.Length; i++)
                _doorRest[i] = _doors[i] != null ? _doors[i].localPosition : Vector3.zero;
            // Player 1's own chef: their look from the tailor, their colour.
            if (_wardrobe != null)
                _wardrobe.Show(ChefProfileFile.Current.Look(0));
            if (_coop != null)
            {
                var block = new MaterialPropertyBlock();
                foreach (var part in _tinted)
                {
                    if (part == null)
                        continue;
                    part.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, _coop.GetPlayerColor(0));
                    part.SetPropertyBlock(block);
                }
            }
        }

        /// <summary>Off to the lift.</summary>
        public void Begin()
        {
            if (_walked < 0f)
                _walked = 0f;
        }

        private void Update()
        {
            if (_walked < 0f || Aboard)
                return;
            _walked += _speed * Time.deltaTime;
            var at = OpeningPath.PointAlong(_path, _walked, out var heading);
            transform.position = at;
            heading.y = 0f;
            if (heading.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), 10f * Time.deltaTime);
            // The doors slide apart as the chef comes, and close once they're in.
            var open = Mathf.Clamp01((_walked - (_length - _doorsFrom)) / 0.6f);
            if (_walked >= _length)
            {
                Aboard = true;
                transform.SetParent(_cab, worldPositionStays: true);
                transform.rotation = Quaternion.LookRotation(Vector3.back); // looking out over the street
                open = 0f;
            }
            for (var i = 0; i < _doors.Length; i++)
                if (_doors[i] != null)
                    _doors[i].localPosition = _doorRest[i] + Vector3.right * (Mathf.Sign(_doorRest[i].x) * _doorTravel * open);
        }
    }
}
