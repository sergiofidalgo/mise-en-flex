using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>Turns steadily about a local axis: rotors, fans, a rotating sign.</summary>
    public sealed class Spin : MonoBehaviour
    {
        [SerializeField] private Vector3 _axis = Vector3.up;
        [SerializeField] private float _degreesPerSecond = 90f;

        public void Set(Vector3 axis, float degreesPerSecond) => (_axis, _degreesPerSecond) = (axis, degreesPerSecond);

        private void Update() => transform.Rotate(_axis, _degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
