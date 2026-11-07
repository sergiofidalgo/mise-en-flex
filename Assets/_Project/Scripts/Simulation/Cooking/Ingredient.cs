using System;
using Game.Simulation.Items;

namespace Game.Simulation.Cooking
{
    public enum IngredientSide
    {
        A,
        B,
    }

    /// <summary>
    /// A cookable item. Its heat is modelled as three layers, surface A, core and surface B,
    /// which is enough for searing one side, flipping, carryover and resting without a full
    /// heat-equation solver. The <see cref="CookingSimulation"/> advances it.
    /// </summary>
    public sealed class Ingredient : Item
    {
        internal const int SurfaceA = 0;
        internal const int Core = 1;
        internal const int SurfaceB = 2;

        internal readonly float[] Temperatures = new float[3];
        internal readonly float[] PendingHeat = new float[3];
        private readonly float[] _peakTemperatures = new float[3];
        private readonly float[] _browning = new float[2];

        internal Ingredient(IngredientProperties properties)
            : base(properties.TypeId)
        {
            Properties = properties;
            for (var i = 0; i < Temperatures.Length; i++)
            {
                Temperatures[i] = properties.StartTemperature;
                _peakTemperatures[i] = properties.StartTemperature;
            }
            Freshness = 1f;
        }

        public IngredientProperties Properties { get; }

        /// <summary>The side currently facing down (into the pan, onto the board).</summary>
        public IngredientSide DownSide { get; private set; } = IngredientSide.A;

        /// <summary>The side facing up: the one players see from above.</summary>
        public IngredientSide UpSide => DownSide == IngredientSide.A ? IngredientSide.B : IngredientSide.A;

        /// <summary>True once it's been flipped at least once.</summary>
        public bool HasBeenFlipped { get; private set; }

        /// <summary>The pan this ingredient is in, or null.</summary>
        public Pan Pan { get; internal set; }

        /// <summary>
        /// On a plate: it cools, but its doneness stops here, whatever heat is still in its outer
        /// layers (decision 2026-11-07). Left in a pan off the heat, it goes on cooking.
        /// </summary>
        public bool IsPlated { get; internal set; }

        /// <summary>
        /// °C of the air around it when that isn't the kitchen's (e.g. inside a fridge); null means
        /// the kitchen ambient temperature.
        /// </summary>
        public float? EnvironmentTemperature { get; set; }

        /// <summary>Multiplies how fast it ages, set by what it's in: below 1 in a container, above in a tray.</summary>
        public float FreshnessFactor { get; set; } = 1f;

        public float CoreTemperature => Temperatures[Core];

        public float SurfaceTemperature(IngredientSide side) => Temperatures[SurfaceIndex(side)];

        /// <summary>Heat-capacity-weighted average temperature: what the diner feels as serving temperature.</summary>
        public float AverageTemperature
        {
            get
            {
                var surface = Properties.SurfaceFraction;
                return surface * (Temperatures[SurfaceA] + Temperatures[SurfaceB]) + (1f - 2f * surface) * Temperatures[Core];
            }
        }

        /// <summary>Highest core temperature reached. Doneness follows it: cooked protein doesn't uncook.</summary>
        public float PeakCoreTemperature => _peakTemperatures[Core];

        /// <summary>Highest temperature a surface layer has reached; shows how far cooking has crept in from that side.</summary>
        public float PeakSurfaceTemperature(IngredientSide side) => _peakTemperatures[SurfaceIndex(side)];

        /// <summary>The level reached so far, or null if raw or the ingredient has no doneness levels.</summary>
        public DonenessLevel? Doneness
        {
            get
            {
                DonenessLevel? reached = null;
                foreach (var level in Properties.DonenessLevels)
                {
                    if (PeakCoreTemperature >= level.MinPeakCoreTemperature)
                        reached = level;
                }
                return reached;
            }
        }

        /// <summary>0 = pale; around 1 = a good crust; at <see cref="IngredientProperties.BurntBrowning"/> it's burnt.</summary>
        public float Browning(IngredientSide side) => _browning[(int)side];

        /// <summary>
        /// True once it has seen real heat: a doneness level, or any browning (a sear on a still-raw
        /// core counts). Storage keeps cooked food apart from raw stock of the same ingredient.
        /// </summary>
        public bool HasBeenCooked => Doneness != null || _browning[0] > 0f || _browning[1] > 0f;

        public bool IsBurnt =>
            _browning[0] >= Properties.BurntBrowning || _browning[1] >= Properties.BurntBrowning;

        /// <summary>1 = fresh, 0 = spoiled.</summary>
        public float Freshness { get; private set; }

        /// <summary>0..1 amount of seasoning. No techniques change it yet.</summary>
        public float Seasoning { get; set; }

        /// <summary>How even the cut was (0..1, from the chopping rhythm), or null if it was never cut.</summary>
        public float? CutEvenness { get; set; }

        /// <summary>In-game seconds since it was last in a pan: how long it has rested.</summary>
        public float SecondsOutOfPan { get; internal set; }

        public void Flip()
        {
            HasBeenFlipped = true;
            DownSide = DownSide == IngredientSide.A ? IngredientSide.B : IngredientSide.A;
        }

        internal static int SurfaceIndex(IngredientSide side) => side == IngredientSide.A ? SurfaceA : SurfaceB;

        internal int DownSurfaceIndex => SurfaceIndex(DownSide);

        internal void AddBrowning(int surfaceIndex, float amount)
        {
            _browning[surfaceIndex == SurfaceA ? 0 : 1] += amount;
        }

        internal void ApplyPendingHeat(float deltaTime)
        {
            var total = Properties.HeatCapacity;
            var surfaceCapacity = total * Properties.SurfaceFraction;
            var coreCapacity = total - 2f * surfaceCapacity;

            Temperatures[SurfaceA] += PendingHeat[SurfaceA] * deltaTime / surfaceCapacity;
            Temperatures[Core] += PendingHeat[Core] * deltaTime / coreCapacity;
            Temperatures[SurfaceB] += PendingHeat[SurfaceB] * deltaTime / surfaceCapacity;
            Array.Clear(PendingHeat, 0, PendingHeat.Length);

            if (IsPlated)
                return;
            for (var i = 0; i < Temperatures.Length; i++)
            {
                if (Temperatures[i] > _peakTemperatures[i])
                    _peakTemperatures[i] = Temperatures[i];
            }
        }

        internal void LoseFreshness(float amount)
        {
            Freshness = Math.Max(0f, Freshness - amount);
        }

        internal void CopyStateFrom(Ingredient other)
        {
            var temperature = other.AverageTemperature;
            for (var i = 0; i < Temperatures.Length; i++)
            {
                Temperatures[i] = temperature;
                _peakTemperatures[i] = temperature;
            }
            Freshness = other.Freshness;
            Seasoning = other.Seasoning;
            EnvironmentTemperature = other.EnvironmentTemperature;
        }
    }
}
