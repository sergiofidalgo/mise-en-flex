using System;
using System.Collections.Generic;

namespace Game.Simulation.Cooking
{
    /// <summary>
    /// Advances all hobs, pans and ingredients in the kitchen. Units are real-world (°C, W, J/K)
    /// and time is in-game seconds; gameplay passes real time × the game's time scale.
    ///
    /// Model: lumped heat capacities exchanging heat through conductances, Q = G × ΔT, integrated
    /// with explicit Euler. Hob → pan → ingredient surface → ingredient core, plus losses to air
    /// and evaporative cooling of surfaces above boiling.
    /// </summary>
    public sealed class CookingSimulation
    {
        // Explicit Euler is stable while the step is well under the fastest time constant
        // (a steak's surface layer, ~20 s). 0.5 s keeps results the same whatever the frame rate.
        private const float MaxStepSeconds = 0.5f;

        // Water boiling off holds wet surfaces near this temperature.
        private const float BoilingPoint = 100f;

        // Spoilage roughly doubles for every 10 °C warmer (a Q10 of 2).
        private const float SpoilageQ10 = 2f;
        private const float SpoilageReferenceTemperature = 20f;
        private const float SecondsPerHour = 3600f;

        private readonly List<Hob> _hobs = new List<Hob>();
        private readonly List<Pan> _pans = new List<Pan>();
        private readonly List<Ingredient> _ingredients = new List<Ingredient>();

        public CookingSimulation(float ambientTemperature, float spoilageMultiplier = 1f)
        {
            AmbientTemperature = ambientTemperature;
            SpoilageMultiplier = spoilageMultiplier;
        }

        /// <summary>Kitchen air temperature, °C.</summary>
        public float AmbientTemperature { get; set; }

        /// <summary>Scales freshness loss; the spoilage realism dial.</summary>
        public float SpoilageMultiplier { get; set; }

        /// <summary>In-game seconds simulated so far.</summary>
        public double ElapsedSeconds { get; private set; }

        public IReadOnlyList<Hob> Hobs => _hobs;
        public IReadOnlyList<Pan> Pans => _pans;
        public IReadOnlyList<Ingredient> Ingredients => _ingredients;

        public bool Contains(Ingredient ingredient) => _ingredients.Contains(ingredient);

        public Hob AddHob(HobProperties properties)
        {
            var hob = new Hob(properties);
            _hobs.Add(hob);
            return hob;
        }

        public Pan AddPan(PanProperties properties)
        {
            var pan = new Pan(properties, AmbientTemperature);
            _pans.Add(pan);
            return pan;
        }

        public Ingredient AddIngredient(IngredientProperties properties)
        {
            var ingredient = new Ingredient(properties);
            _ingredients.Add(ingredient);
            return ingredient;
        }

        /// <summary>
        /// Stops simulating an ingredient (e.g. it went in the bin or was served).
        /// Take it out of its pan first.
        /// </summary>
        public void Remove(Ingredient ingredient)
        {
            if (ingredient.Pan != null)
                throw new InvalidOperationException("Take the ingredient out of its pan before removing it.");
            _ingredients.Remove(ingredient);
        }

        /// <summary>
        /// Swaps an ingredient for a new form of it (e.g. a whole onion becomes diced onion): the new
        /// one starts at the old one's average temperature and freshness, and the old one is removed.
        /// </summary>
        public Ingredient Transform(Ingredient original, IngredientProperties newForm)
        {
            if (original.Pan != null)
                throw new InvalidOperationException("Take the ingredient out of its pan before transforming it.");

            var transformed = AddIngredient(newForm);
            transformed.CopyStateFrom(original);
            Remove(original);
            return transformed;
        }

        public void Tick(float seconds)
        {
            while (seconds > 0f)
            {
                var step = Math.Min(seconds, MaxStepSeconds);
                Step(step);
                seconds -= step;
            }
        }

        /// <summary>
        /// Jumps ahead with the kitchen closed: hobs off, everything settled at the temperature
        /// around it, ageing at that temperature. Worked out directly rather than stepped, because
        /// a night would take hundreds of thousands of steps.
        /// </summary>
        public void SkipClosedTime(float seconds)
        {
            foreach (var hob in _hobs)
                hob.Dial = 0f;
            foreach (var pan in _pans)
                pan.Temperature = AmbientTemperature;

            foreach (var ingredient in _ingredients)
            {
                var around = ingredient.EnvironmentTemperature ?? AmbientTemperature;
                for (var i = 0; i < ingredient.Temperatures.Length; i++)
                    ingredient.Temperatures[i] = around;
                ingredient.LoseFreshness(FreshnessLossAt(ingredient, around, seconds));
                if (ingredient.Pan == null)
                    ingredient.SecondsOutOfPan += seconds;
            }

            ElapsedSeconds += seconds;
        }

        private void Step(float dt)
        {
            foreach (var pan in _pans)
                pan.PendingHeat = -pan.Properties.AirLossConductance * (pan.Temperature - AmbientTemperature);

            foreach (var hob in _hobs)
            {
                if (hob.Pan != null)
                    hob.Pan.PendingHeat += hob.Power;
            }

            foreach (var ingredient in _ingredients)
            {
                var p = ingredient.Properties;
                var t = ingredient.Temperatures;
                var q = ingredient.PendingHeat;

                // Conduction between each surface layer and the core.
                foreach (var surface in SurfaceIndices)
                {
                    var flow = p.SurfaceCoreConductance * (t[surface] - t[Ingredient.Core]);
                    q[surface] -= flow;
                    q[Ingredient.Core] += flow;

                    if (t[surface] > BoilingPoint)
                        q[surface] -= p.EvaporationConductance * (t[surface] - BoilingPoint);
                }

                // The down surface touches the pan if there is one; every other surface is in air.
                var pan = ingredient.Pan;
                foreach (var surface in SurfaceIndices)
                {
                    if (pan != null && surface == ingredient.DownSurfaceIndex)
                    {
                        var flow = p.PanContactConductance * (pan.Temperature - t[surface]);
                        q[surface] += flow;
                        pan.PendingHeat -= flow;

                        // Browning follows the pan's heat, not the wet surface's (which sits near 100 °C).
                        Brown(ingredient, surface, pan.Temperature, dt);
                    }
                    else
                    {
                        var air = ingredient.EnvironmentTemperature ?? AmbientTemperature;
                        q[surface] += p.AirConductance * (air - t[surface]);
                        Brown(ingredient, surface, t[surface], dt);
                    }
                }
            }

            foreach (var pan in _pans)
                pan.Temperature += pan.PendingHeat * dt / pan.Properties.HeatCapacity;

            foreach (var ingredient in _ingredients)
            {
                ingredient.ApplyPendingHeat(dt);
                ingredient.LoseFreshness(FreshnessLoss(ingredient, dt));
                ingredient.SecondsOutOfPan = ingredient.Pan == null ? ingredient.SecondsOutOfPan + dt : 0f;
            }

            ElapsedSeconds += dt;
        }

        private static readonly int[] SurfaceIndices = { Ingredient.SurfaceA, Ingredient.SurfaceB };

        private static void Brown(Ingredient ingredient, int surface, float temperature, float dt)
        {
            var excess = temperature - ingredient.Properties.BrowningStartTemperature;
            if (excess > 0f)
                ingredient.AddBrowning(surface, ingredient.Properties.BrowningRate * excess * dt);
        }

        private float FreshnessLoss(Ingredient ingredient, float dt) => FreshnessLossAt(ingredient, ingredient.CoreTemperature, dt);

        private float FreshnessLossAt(Ingredient ingredient, float temperature, float dt)
        {
            var rateAtReference = ingredient.Properties.FreshnessLossPerHourAt20C / SecondsPerHour;
            var temperatureFactor = (float)Math.Pow(
                SpoilageQ10, (temperature - SpoilageReferenceTemperature) / 10f);
            return rateAtReference * temperatureFactor * ingredient.FreshnessFactor * SpoilageMultiplier * dt;
        }
    }
}
