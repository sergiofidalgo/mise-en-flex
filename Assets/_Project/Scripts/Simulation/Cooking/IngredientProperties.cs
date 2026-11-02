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
    }
}
