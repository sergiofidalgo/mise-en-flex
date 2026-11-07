using System;

namespace Game.Simulation.Cooking
{
    /// <summary>How a heat source warms the pan on it (decision 2026-11-07: kinds of heat, not tiers).</summary>
    public enum HeatKind
    {
        /// <summary>A flame on a dial: it gives the pan a set power, and the pan takes its time.</summary>
        Gas,

        /// <summary>A few fixed steps, each with its own temperature, which the pan reaches almost at once.</summary>
        Induction,

        /// <summary>Always at one temperature, like a grill's hot or cool zone.</summary>
        Fixed,
    }

    public sealed class HobProperties
    {
        public HeatKind Kind { get; set; } = HeatKind.Gas;

        /// <summary>
        /// Watts at full dial for gas. A domestic gas burner is around 2–3 kW. For the other kinds,
        /// the most it can put into a pan, or take out of it.
        /// </summary>
        public float MaxPower { get; set; }

        /// <summary>Induction: the pan temperature of each step above off, °C, lowest first.</summary>
        public float[] StepTemperatures { get; set; } = Array.Empty<float>();

        /// <summary>Fixed: the temperature it keeps, °C.</summary>
        public float FixedTemperature { get; set; }

        /// <summary>
        /// Induction and fixed: W/K. How hard it pulls the pan towards its temperature; with the pan's
        /// heat capacity this sets how fast the pan gets there (heat capacity / this, in seconds).
        /// </summary>
        public float Response { get; set; }
    }

    /// <summary>
    /// A heat source a pan stands on: a gas burner with a continuous dial, an induction hob with a
    /// few steps, or a grill zone that's always at one temperature. With no pan it does nothing.
    /// </summary>
    public sealed class Hob
    {
        private float _dial;
        private int _step;

        internal Hob(HobProperties properties)
        {
            Properties = properties;
        }

        public HobProperties Properties { get; }

        public HeatKind Kind => Properties.Kind;

        /// <summary>
        /// 0 = off, 1 = full heat. On induction it reads as the step over the number of steps, and
        /// setting it picks the nearest step; a fixed source always reads full.
        /// </summary>
        public float Dial
        {
            get => Kind switch
            {
                HeatKind.Induction => Steps == 0 ? 0f : (float)_step / Steps,
                HeatKind.Fixed => 1f,
                _ => _dial,
            };
            set
            {
                if (Kind == HeatKind.Induction)
                    Step = (int)Math.Round(Math.Clamp(value, 0f, 1f) * Steps);
                else
                    _dial = Math.Clamp(value, 0f, 1f);
            }
        }

        /// <summary>Induction: how many steps above off.</summary>
        public int Steps => Properties.StepTemperatures.Length;

        /// <summary>Induction: 0 = off, up to <see cref="Steps"/>.</summary>
        public int Step
        {
            get => _step;
            set => _step = Math.Clamp(value, 0, Steps);
        }

        public Pan Pan { get; private set; }

        /// <summary>Joules put into pans so far, for the energy bill. A fixed source doesn't count: it's paid by the day.</summary>
        public double EnergyUsed { get; private set; }

        /// <summary>Gas: watts going into the pan at this dial.</summary>
        public float Power => Kind == HeatKind.Gas ? Properties.MaxPower * _dial : 0f;

        public void PlacePan(Pan pan)
        {
            if (Pan != null)
                throw new InvalidOperationException("Hob already has a pan.");
            Pan = pan;
        }

        public Pan RemovePan()
        {
            var pan = Pan;
            Pan = null;
            return pan;
        }

        /// <summary>Watts into this pan; less than zero when it takes heat out.</summary>
        internal float HeatInto(Pan pan, float ambient)
        {
            var panTemperature = pan.Temperature;
            switch (Kind)
            {
                case HeatKind.Gas:
                    return Power;

                case HeatKind.Induction:
                    // Off is off: the pan cools on its own, as one lifted off would. On a step the pan is
                    // pulled to that step's temperature both ways, hotter or cooler, so changing step
                    // shows at once (the user's call). Real induction can't cool a pan; this is for feel.
                    return _step == 0 ? 0f : TowardsTemperature(Properties.StepTemperatures[_step - 1], pan, ambient);

                default:
                    return TowardsTemperature(Properties.FixedTemperature, pan, ambient);
            }
        }

        // A thermostat: it makes up what the pan loses to the air, so the pan settles on the target
        // itself rather than short of it, and pulls on top of that in proportion to how far off it is.
        private float TowardsTemperature(float target, Pan pan, float ambient)
        {
            var airLoss = pan.Properties.AirLossConductance * (pan.Temperature - ambient);
            var pull = Properties.Response * (target - pan.Temperature);
            return Math.Clamp(airLoss + pull, -Properties.MaxPower, Properties.MaxPower);
        }

        internal void Meter(float watts, float seconds)
        {
            if (Kind != HeatKind.Fixed && watts > 0f)
                EnergyUsed += watts * seconds;
        }
    }
}
