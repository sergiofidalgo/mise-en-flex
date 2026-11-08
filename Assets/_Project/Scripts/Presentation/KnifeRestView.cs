using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// A knife put down on a counter lies across it, diagonally and bigger (the user's note,
    /// 2026-11-08), so it reads from the overhead camera as a knife sitting there; at its real size,
    /// pointing straight away, it was a sliver on a whole counter. Not in a chef's hands (the arms
    /// pose it) or in a knife block (its slots hold it). Only looks.
    /// </summary>
    public sealed class KnifeRestView : MonoBehaviour
    {
        [SerializeField] private ItemView _view;
        [Tooltip("How the knife lies on a counter: its offset from where items sit (m), its turn, and its size on top of the counter's own (1.4x).")]
        [SerializeField] private Vector3 _restOffset = new Vector3(-0.07f, 0f, -0.08f);
        [SerializeField] private Vector3 _restTurn = new Vector3(0f, 40f, 0f);
        [SerializeField, Min(0.1f)] private float _restScale = 1.15f;

        private Transform _anchor;
        private bool _resting;

        private void LateUpdate()
        {
            if (_view == null || _view.Anchor == null)
                return;
            if (_view.Anchor != _anchor)
            {
                _anchor = _view.Anchor;
                _resting = _anchor.GetComponentInParent<PlayerInteractor>() == null
                           && _anchor.GetComponentInParent<KnifeRackStation>() == null;
            }
            if (_resting)
                _view.SetHeldPose(_restOffset, Quaternion.Euler(_restTurn), scale: _restScale);
        }
    }
}
