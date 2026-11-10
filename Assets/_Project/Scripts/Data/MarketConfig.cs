using Game.Simulation.Market;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Data
{
    /// <summary>
    /// The street's stalls (Phase 5, milestone 3; decision 2026-11-10): how much one purchase brings, and what
    /// the cheap and premium grades cost and how fresh they start. Each ingredient's own price is its
    /// <see cref="IngredientDefinition.Price"/>, per portion; the factors here scale it per grade.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Market Config", fileName = "MarketConfig")]
    public sealed class MarketConfig : ScriptableObject
    {
        [Tooltip("Most portions one purchase brings, straight into storage. 6 fills one storage slot.")]
        [FormerlySerializedAs("_cratePortions")]
        [SerializeField, Min(1)] private int _portionsPerPurchase = 6;

        [Header("Cheap")]
        [Tooltip("Freshness cheap stock starts at (1 = just picked). It ages as fast as any, so it spoils sooner, and freshness feeds the quality score.")]
        [SerializeField, Range(0.1f, 1f)] private float _cheapFreshness = 0.7f;
        [Tooltip("Times the ingredient's price, per portion.")]
        [SerializeField, Min(0f)] private float _cheapPriceFactor = 1f;

        [Header("Premium")]
        [SerializeField, Range(0.1f, 1f)] private float _premiumFreshness = 1f;
        [SerializeField, Min(0f)] private float _premiumPriceFactor = 1.6f;

        public MarketSettings ToSettings() => new MarketSettings
        {
            PortionsPerPurchase = _portionsPerPurchase,
            CheapFreshness = _cheapFreshness,
            PremiumFreshness = _premiumFreshness,
            CheapPriceFactor = _cheapPriceFactor,
            PremiumPriceFactor = _premiumPriceFactor,
        };
    }
}
