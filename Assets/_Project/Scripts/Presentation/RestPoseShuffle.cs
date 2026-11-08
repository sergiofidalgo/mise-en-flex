using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Each time it's shown, a station's display piece lands a little differently: turned any way,
    /// tipped a few degrees in a random direction, and shifted a touch (the user's note, 2026-11-08:
    /// the dirty plate in the sink was the same every time). Only looks.
    /// </summary>
    public sealed class RestPoseShuffle : MonoBehaviour
    {
        [Tooltip("Where it rests before the shift (m, in its parent).")]
        [SerializeField] private Vector3 _basePosition;
        [Tooltip("How far it can shift from there, across and along (m).")]
        [SerializeField] private Vector2 _maxShift = new Vector2(0.04f, 0.05f);
        [Tooltip("How far it tips, at least and at most (degrees), in a random direction.")]
        [SerializeField, Range(0f, 45f)] private float _minTip = 6f;
        [SerializeField, Range(0f, 45f)] private float _maxTip = 16f;

        private void OnEnable()
        {
            transform.localPosition = _basePosition
                + new Vector3(Random.Range(-_maxShift.x, _maxShift.x), 0f, Random.Range(-_maxShift.y, _maxShift.y));
            var tipAxis = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.right;
            transform.localRotation = Quaternion.AngleAxis(Random.Range(_minTip, _maxTip), tipAxis)
                * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
    }
}
