using Game.Data;
using Game.Gameplay.Saving;
using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The chef in the opening (the user's notes, 2026-11-10): already in the tower's lift at the
    /// street while the menu is up, wearing player 1's look from the tailor and their colour, and
    /// riding up beside the camera the moment a campaign is chosen. Upstairs, player 1 comes out of
    /// the floor's elevator: the same chef. (They first walked in from the kerb, but that was hard
    /// to notice and held the climb back.) Players who join in the menu ride up with them: copies
    /// of this chef in their own look and colour (2026-11-13).
    /// </summary>
    public sealed class OpeningChef : MonoBehaviour
    {
        [SerializeField] private ChefWardrobe _wardrobe;
        [Tooltip("The parts in the player's colour (toque band, apron, neckerchief).")]
        [SerializeField] private Renderer[] _tinted = System.Array.Empty<Renderer>();
        [SerializeField] private CoopSettings _coop;

        [Tooltip("Seconds a chef who has just joined takes to pop into the cab.")]
        [SerializeField, Min(0.01f)] private float _popSeconds = 0.28f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private int _slot;
        private float _popped = -1f;   // seconds into the pop, or -1 when not popping
        private Vector3 _scale = Vector3.one;

        /// <summary>The player slot this chef stands for: 0 for player 1, who's in from the start.</summary>
        public int Slot => _slot;

        private bool _dressed;

        // Player 1's chef dresses itself; a copy for a later player is dressed as it arrives.
        private void Start()
        {
            if (!_dressed)
                Dress(_slot);
        }

        /// <summary>
        /// A player who has just joined in the menu (2026-11-13): wears their look and colour, and
        /// pops in from nothing with a small overshoot, so the join reads at once.
        /// </summary>
        public void Arrive(int slot)
        {
            _slot = slot;
            Dress(slot);
            _scale = transform.localScale;
            _popped = 0f;
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            if (_popped < 0f)
                return;
            _popped += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_popped / _popSeconds);
            // Ease out with a little overshoot (back-out), settling at full size.
            const float overshoot = 1.7f;
            var u = t - 1f;
            var grow = 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
            transform.localScale = _scale * grow;
            if (t >= 1f)
            {
                transform.localScale = _scale;
                _popped = -1f;
            }
        }

        /// <summary>Wears player <paramref name="slot"/>'s look and colour at once (also for stills outside Play mode).</summary>
        public void Wear(int slot)
        {
            _slot = slot;
            Dress(slot);
        }

        // The player's own chef: their look from the tailor, their colour.
        private void Dress(int slot)
        {
            _dressed = true;
            if (_wardrobe != null)
                _wardrobe.Show(ChefProfileFile.Current.Look(slot));
            if (_coop == null)
                return;
            var block = new MaterialPropertyBlock();
            foreach (var part in _tinted)
            {
                if (part == null)
                    continue;
                part.GetPropertyBlock(block);
                block.SetColor(BaseColorId, _coop.GetPlayerColor(slot));
                part.SetPropertyBlock(block);
            }
        }
    }
}
