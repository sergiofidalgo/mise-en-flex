using System;
using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using Game.Simulation.Cooking;
using Game.Simulation.Prep;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows a cut coming along on the cutting board (the user's notes, 2026-11-08). For each kind
    /// of food being cut, what lies on the board after each chop: an onion's first chop leaves two
    /// halves (its second makes rings, an item of their own); each chop into the rings leaves fewer
    /// rings and more dice, until the last makes the dice heap. The food's own view hides while
    /// its pieces show. Bits fly on every chop. Only looks: the cut itself is the simulation's
    /// <see cref="PrepCounter"/>.
    /// </summary>
    public sealed class CutProgressView : MonoBehaviour
    {
        [Serializable]
        public sealed class Stage
        {
            [Tooltip("What lies on the board after this chop.")]
            public GameObject[] Shown = Array.Empty<GameObject>();
        }

        [Serializable]
        public sealed class Cut
        {
            [Tooltip("The food being cut.")]
            public string TypeId;
            [Tooltip("After its first chop, its second, and so on; the last chop makes the cut item instead.")]
            public Stage[] Stages = Array.Empty<Stage>();
        }

        [SerializeField] private PrepStation _station;
        [Tooltip("Where the item on the board sits; its view is hidden while the cut pieces show.")]
        [SerializeField] private Transform _boardAnchor;
        [SerializeField] private Cut[] _cuts = Array.Empty<Cut>();
        [Tooltip("Bits that fly off with each chop.")]
        [SerializeField] private ParticleSystem _bits;
        [SerializeField, Min(0)] private int _bitsPerChop = 7;
        [Tooltip("How much the pieces bounce when a chop changes them, and for how long (s).")]
        [SerializeField, Min(0f)] private float _bounce = 0.12f;
        [SerializeField, Min(0.01f)] private float _bounceTime = 0.15f;

        private ItemView _hidden;
        private Stage _shown;
        private Vector3 _baseScale = Vector3.one;
        private float _bounceAge = float.MaxValue;

        private void Awake() => _baseScale = transform.localScale;

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
            var stage = CurrentStage();

            // The food hides while its pieces show.
            Hide(stage != null ? _boardAnchor.GetComponentInChildren<ItemView>() : null);

            if (stage != _shown)
            {
                Show(stage);
                _shown = stage;
                _bounceAge = 0f;
            }
            _bounceAge += Time.deltaTime;
            var t = Mathf.Clamp01(_bounceAge / _bounceTime);
            transform.localScale = _baseScale * (1f + _bounce * Mathf.Sin(Mathf.PI * t));
        }

        // The stage for what's on the board and how many chops it has had, or null for none.
        private Stage CurrentStage()
        {
            var counter = _station != null ? _station.Counter : null;
            if (!(counter?.Current is Ingredient ingredient) || counter.Cuts <= 0)
                return null;
            foreach (var cut in _cuts)
            {
                if (cut.TypeId == ingredient.TypeId)
                    return counter.Cuts - 1 < cut.Stages.Length ? cut.Stages[counter.Cuts - 1] : null;
            }
            return null;
        }

        private void Show(Stage stage)
        {
            foreach (var cut in _cuts)
                foreach (var each in cut.Stages)
                    foreach (var piece in each.Shown)
                        if (piece != null)
                            piece.SetActive(false);
            if (stage == null)
                return;
            foreach (var piece in stage.Shown)
                if (piece != null)
                    piece.SetActive(true);
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
