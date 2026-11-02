using System;

namespace Game.Simulation.Cooking
{
    public sealed class HobProperties
    {
        /// <summary>Watts delivered to the pan at full dial. A domestic gas burner is around 2–3 kW.</summary>
        public float MaxPower { get; set; }
    }

    /// <summary>
    /// A burner with a continuous dial. It heats whatever pan sits on it; with no pan it does nothing.
    /// </summary>
    public sealed class Hob
    {
        private float _dial;

        internal Hob(HobProperties properties)
        {
            Properties = properties;
        }

        public HobProperties Properties { get; }

        /// <summary>0 = off, 1 = full heat.</summary>
        public float Dial
        {
            get => _dial;
            set => _dial = Math.Clamp(value, 0f, 1f);
        }

        public Pan Pan { get; private set; }

        public float Power => Properties.MaxPower * _dial;

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
    }
}
