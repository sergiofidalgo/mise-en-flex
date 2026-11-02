using Game.Gameplay.Items;
using Game.Simulation.Cooking;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Steam rising from hot food, fading as it cools: a plated dish visibly loses its heat while
    /// it waits, so players feel the pressure to get it to the pass.
    /// </summary>
    public sealed class SteamView : MonoBehaviour
    {
        [SerializeField] private ItemView _itemView;
        [SerializeField] private ParticleSystem _steam;

        [Tooltip("Average °C below which there's no steam.")]
        [SerializeField] private float _from = 42f;
        [Tooltip("Average °C at which steam is at its strongest.")]
        [SerializeField] private float _full = 65f;
        [Tooltip("Particles per second at full steam.")]
        [SerializeField, Min(0f)] private float _rate = 6f;

        private void Update()
        {
            if (!(_itemView.Item is Ingredient ingredient))
                return;

            // No steam while it's sizzling in a pan: the pan's own cues cover that.
            var strength = ingredient.Pan == null ? Mathf.InverseLerp(_from, _full, ingredient.AverageTemperature) : 0f;
            var emission = _steam.emission;
            emission.rateOverTime = strength * _rate;
        }
    }
}
