using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using Game.Simulation.Cooking;
using Game.Simulation.Prep;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows a cut coming along on the cutting board (the user's choice, 2026-11-08): the first chop
    /// splits the onion into two halves lying cut face up, then each chop piles up more dice beside
    /// them until the last leaves the cut onion's heap (whose dice these are). Bits fly on every chop.
    /// Only looks: the cut itself is the simulation's <see cref="PrepCounter"/>.
    /// </summary>
    public sealed class CutProgressView : MonoBehaviour
    {
        [SerializeField] private PrepStation _station;
        [Tooltip("Where the item on the board sits; its view is hidden while the cut pieces show.")]
        [SerializeField] private Transform _boardAnchor;
        [Tooltip("The ingredient these pieces are cut from.")]
        [SerializeField] private string _typeId = "Onion";
        [Tooltip("The halves after the first chop; one goes before the last chop.")]
        [SerializeField] private GameObject[] _halves;
        [Tooltip("The dice, in the order they pile up.")]
        [SerializeField] private GameObject[] _pieces;
        [Tooltip("Bits that fly off with each chop.")]
        [SerializeField] private ParticleSystem _bits;
        [SerializeField, Min(0)] private int _bitsPerChop = 7;
        [Tooltip("How much the pieces bounce when a chop changes them, and for how long (s).")]
        [SerializeField, Min(0f)] private float _bounce = 0.12f;
        [SerializeField, Min(0.01f)] private float _bounceTime = 0.15f;

        private ItemView _hidden;
        private int _shownCuts = -1;
        private float _bounceAge = float.MaxValue;

        private void OnEnable()
        {
            if (_station != null)
                _station.Chopped += HandleChopped;
        }

        private void OnDisable()
        {
            if (_station != null)
                _station.Chopped -= HandleChopped;
            Hide(null);
        }

        private void HandleChopped(ChopGrade grade)
        {
            if (_bits != null)
                _bits.Emit(_bitsPerChop);
        }

        private void LateUpdate()
        {
            var counter = _station != null ? _station.Counter : null;
            var cuts = counter?.Cuts ?? 0;
            var needed = counter?.CutsNeeded ?? 0;
            var cutting = counter?.Current is Ingredient ingredient && ingredient.TypeId == _typeId && cuts > 0 && cuts < needed;
            if (!cutting)
                cuts = 0;

            // The whole onion hides while its pieces show.
            Hide(cutting ? _boardAnchor.GetComponentInChildren<ItemView>() : null);

            if (cuts != _shownCuts)
            {
                Show(cuts, needed);
                _shownCuts = cuts;
                _bounceAge = 0f;
            }
            _bounceAge += Time.deltaTime;
            var t = Mathf.Clamp01(_bounceAge / _bounceTime);
            transform.localScale = Vector3.one * (1f + _bounce * Mathf.Sin(Mathf.PI * t));
        }

        // Two halves from the first chop, one left by the last; the dice grow from none after the
        // first chop to most of the heap before the last, which makes the whole heap.
        private void Show(int cuts, int needed)
        {
            var halves = cuts <= 0 ? 0 : cuts >= needed - 1 ? 1 : 2;
            for (var i = 0; i < _halves.Length; i++)
                _halves[i].SetActive(i < halves);
            var pieces = cuts <= 0 || needed <= 1 ? 0 : Mathf.RoundToInt(_pieces.Length * (cuts - 1f) / (needed - 1f));
            for (var i = 0; i < _pieces.Length; i++)
                _pieces[i].SetActive(i < pieces);
        }

        private void Hide(ItemView view)
        {
            if (view == _hidden)
                return;
            SetVisible(_hidden, true);
            SetVisible(view, false);
            _hidden = view;
        }

        private static void SetVisible(ItemView view, bool visible)
        {
            if (view == null)
                return;
            foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = visible;
        }
    }
}
