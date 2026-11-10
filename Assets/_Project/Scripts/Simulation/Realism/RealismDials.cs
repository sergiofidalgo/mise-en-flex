using System;

namespace Game.Simulation.Realism
{
    /// <summary>
    /// The five realism dials (game design §12; decision 2026-11-10), as plain values the simulation
    /// reads. Cooking precision and minigame complexity run 0..1 (0.5 is the tuning as designed);
    /// spoilage, patience and hygiene are multipliers (1 is the tuning as designed).
    /// </summary>
    public sealed class RealismDials
    {
        /// <summary>How narrow the doneness windows are when scoring: 0 generous, 1 strict.</summary>
        public float CookingPrecision { get; set; } = 0.5f;

        /// <summary>Multiplies how fast food loses freshness. 0 never spoils.</summary>
        public float Spoilage { get; set; } = 1f;

        /// <summary>Multiplies how long guests wait. 2 is twice as patient.</summary>
        public float Patience { get; set; } = 1f;

        /// <summary>How demanding the stick games are (washing, cleaning, the extinguisher): 0 gentle, 1 hard.</summary>
        public float MinigameComplexity { get; set; } = 0.5f;

        /// <summary>Multiplies how much dirt a service leaves. 0 leaves none.</summary>
        public float Hygiene { get; set; } = 1f;

        public RealismDials Copy() => new RealismDials
        {
            CookingPrecision = CookingPrecision,
            Spoilage = Spoilage,
            Patience = Patience,
            MinigameComplexity = MinigameComplexity,
            Hygiene = Hygiene,
        };

        /// <summary>True if every dial is the same, give or take float rounding from the settings file.</summary>
        public bool SameAs(RealismDials other)
        {
            const float Epsilon = 0.001f;
            return other != null
                && Math.Abs(CookingPrecision - other.CookingPrecision) < Epsilon
                && Math.Abs(Spoilage - other.Spoilage) < Epsilon
                && Math.Abs(Patience - other.Patience) < Epsilon
                && Math.Abs(MinigameComplexity - other.MinigameComplexity) < Epsilon
                && Math.Abs(Hygiene - other.Hygiene) < Epsilon;
        }
    }
}
