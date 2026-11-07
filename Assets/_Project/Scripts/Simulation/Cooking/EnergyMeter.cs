using System.Collections.Generic;

namespace Game.Simulation.Cooking
{
    /// <summary>What energy costs (decision 2026-11-07: electricity cheapest, then gas, then charcoal).</summary>
    public sealed class EnergyPrices
    {
        /// <summary>Money per kWh an induction hob puts into its pan.</summary>
        public float Electricity { get; set; }

        /// <summary>Money per kWh a gas hob puts into its pan.</summary>
        public float Gas { get; set; }

        /// <summary>Money per grill per day: it's always lit, so the charcoal burns whether it's used or not.</summary>
        public float CharcoalPerGrill { get; set; }
    }

    /// <summary>A day's energy, by what it was spent on.</summary>
    public readonly struct EnergyBill
    {
        public EnergyBill(float electricity, float gas, float charcoal)
        {
            Electricity = electricity;
            Gas = gas;
            Charcoal = charcoal;
        }

        public float Electricity { get; }
        public float Gas { get; }
        public float Charcoal { get; }
        public float Total => Electricity + Gas + Charcoal;
    }

    /// <summary>
    /// Reads the hobs' meters once a day and prices what they used since the last reading, so heat
    /// spent preheating in Prep counts too. Grill zones aren't metered; each grill costs by the day.
    /// </summary>
    public sealed class EnergyMeter
    {
        private const double JoulesPerKwh = 3.6e6;

        private readonly Dictionary<Hob, double> _lastReading = new Dictionary<Hob, double>();

        public EnergyBill Read(IEnumerable<Hob> hobs, int grills, EnergyPrices prices)
        {
            double electricity = 0, gas = 0;
            var seen = new HashSet<Hob>();
            foreach (var hob in hobs)
            {
                seen.Add(hob);
                _lastReading.TryGetValue(hob, out var before);
                var kwh = (hob.EnergyUsed - before) / JoulesPerKwh;
                _lastReading[hob] = hob.EnergyUsed;
                if (hob.Kind == HeatKind.Induction)
                    electricity += kwh * prices.Electricity;
                else if (hob.Kind == HeatKind.Gas)
                    gas += kwh * prices.Gas;
            }

            // Hobs sold since the last reading: their energy went unbilled; forget them.
            var gone = new List<Hob>();
            foreach (var hob in _lastReading.Keys)
            {
                if (!seen.Contains(hob))
                    gone.Add(hob);
            }
            foreach (var hob in gone)
                _lastReading.Remove(hob);

            return new EnergyBill((float)electricity, (float)gas, grills * prices.CharcoalPerGrill);
        }
    }
}
