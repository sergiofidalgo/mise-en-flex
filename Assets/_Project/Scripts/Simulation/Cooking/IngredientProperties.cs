using System;
using System.Collections.Generic;

namespace Game.Simulation.Cooking
{
    /// <summary>A named doneness level, reached once the core's peak temperature passes a threshold.</summary>
    public readonly struct DonenessLevel
    {
        public DonenessLevel(string name, float minPeakCoreTemperature)
        {
            Name = name;
            MinPeakCoreTemperature = minPeakCoreTemperature;
        }

        public string Name { get; }

        /// <summary>°C the core must have reached for this level.</summary>
        public float MinPeakCoreTemperature { get; }
    }

    /// <summary>
    /// Physical properties of one ingredient type, in real-world units (°C, J/K, W/K, in-game
    /// seconds). Filled from an IngredientDefinition asset; plain C# so the simulation can be
    /// tested without Unity.
    /// </summary>
    public sealed class IngredientProperties
    {
        public string TypeId { get; set; }

        /// <summary>Whole-item heat capacity in J/K (mass × specific heat; meat ≈ 3500 J/kg·K).</summary>
        public float HeatCapacity { get; set; }

        /// <summary>Share of the heat capacity in each surface layer (the core gets the rest).</summary>
        public float SurfaceFraction { get; set; }

        /// <summary>W/K between each surface layer and the core (conductivity × area / distance).</summary>
        public float SurfaceCoreConductance { get; set; }

        /// <summary>W/K between the pan and the surface lying in it.</summary>
        public float PanContactConductance { get; set; }

        /// <summary>W/K between an exposed surface and the kitchen air.</summary>
        public float AirConductance { get; set; }

        /// <summary>W/K of evaporative cooling per °C a surface is above boiling; keeps wet surfaces near 100 °C.</summary>
        public float EvaporationConductance { get; set; }

        /// <summary>°C of the pan (or exposed surface) above which browning starts (Maillard reaction).</summary>
        public float BrowningStartTemperature { get; set; }

        /// <summary>Browning gained per °C above the start temperature per in-game second.</summary>
        public float BrowningRate { get; set; }

        /// <summary>Browning level at which a surface counts as burnt.</summary>
        public float BurntBrowning { get; set; }

        /// <summary>Doneness levels in ascending order of temperature. Empty for ingredients without doneness.</summary>
        public IReadOnlyList<DonenessLevel> DonenessLevels { get; set; } = Array.Empty<DonenessLevel>();

        /// <summary>°C the ingredient has when it comes out of storage.</summary>
        public float StartTemperature { get; set; }

        /// <summary>Freshness (0..1) lost per in-game hour at 20 °C; halves for every 10 °C colder.</summary>
        public float FreshnessLossPerHourAt20C { get; set; }

        /// <summary>
        /// Peak core °C the kitchen aims for when scoring doneness, or null if doneness isn't scored.
        /// Placeholder house standard until customer orders (Phase 2) choose it per dish.
        /// </summary>
        public float? TargetCoreTemperature { get; set; }

        /// <summary>True if the food should reach the table hot, so serving temperature is scored.</summary>
        public bool ServedHot { get; set; }

        /// <summary>Average °C at or above which a hot dish scores full marks for temperature.</summary>
        public float ServingIdealTemperature { get; set; }

        /// <summary>Average °C at or below which a hot dish scores nothing for temperature.</summary>
        public float ServingColdTemperature { get; set; }

        /// <summary>True if the crust (browning) is part of the score, e.g. a seared steak.</summary>
        public bool ScoresBrowning { get; set; }

        /// <summary>Type id this becomes when cut at the prep counter (e.g. Onion → DicedOnion), or null if it isn't cut.</summary>
        public string CutIntoTypeId { get; set; }

        /// <summary>Knife chops needed to finish the cut.</summary>
        public int CutsNeeded { get; set; }

        /// <summary>Real seconds for the cutting dial's needle to sweep the arc once, right to left.</summary>
        public float ChopSecondsPerSweep { get; set; } = 0.7f;

        /// <summary>Centre of the cutting sweet spot, degrees along the sweep from its right end.</summary>
        public float SweetSpotPosition { get; set; }

        /// <summary>Degrees either side of the sweet spot's centre for a Good cut.</summary>
        public float SweetSpotGoodHalfWidth { get; set; } = 20f;

        /// <summary>Degrees either side for an Early/Late cut; beyond is a Miss.</summary>
        public float SweetSpotOkHalfWidth { get; set; } = 40f;

        /// <summary>In-game seconds of rest off the pan for full marks (steak); 0 if resting isn't scored.</summary>
        public float RestTargetSeconds { get; set; }
    }
}
