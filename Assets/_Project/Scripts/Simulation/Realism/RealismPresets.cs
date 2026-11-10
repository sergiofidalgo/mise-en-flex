namespace Game.Simulation.Realism
{
    /// <summary>A named set of dials, or Custom when the dials match none of them.</summary>
    public enum RealismPreset
    {
        Relaxed,
        Standard,
        Chef,
        Custom,
    }

    /// <summary>
    /// Relaxed / Standard / Chef on top of the individual dials (game design §12; the user's values,
    /// 2026-11-10). The preset isn't stored: it's read back from the dials, so turning any dial under
    /// Advanced shows Custom, and turning it back shows the preset again.
    /// </summary>
    public sealed class RealismPresets
    {
        private readonly RealismDials _relaxed;
        private readonly RealismDials _standard;
        private readonly RealismDials _chef;

        public RealismPresets(RealismDials relaxed, RealismDials standard, RealismDials chef)
        {
            _relaxed = relaxed;
            _standard = standard;
            _chef = chef;
        }

        /// <summary>A copy of the preset's dials (Custom gives Standard's), safe to change.</summary>
        public RealismDials Get(RealismPreset preset) => preset switch
        {
            RealismPreset.Relaxed => _relaxed.Copy(),
            RealismPreset.Chef => _chef.Copy(),
            _ => _standard.Copy(),
        };

        /// <summary>The preset these dials are, or Custom.</summary>
        public RealismPreset Identify(RealismDials dials)
        {
            if (dials.SameAs(_standard))
                return RealismPreset.Standard;
            if (dials.SameAs(_relaxed))
                return RealismPreset.Relaxed;
            if (dials.SameAs(_chef))
                return RealismPreset.Chef;
            return RealismPreset.Custom;
        }

        /// <summary>
        /// The preset a step left (-1) or right (+1) of <paramref name="current"/>, stopping at the
        /// ends. From Custom, left is Relaxed's side of Standard and right is Chef's.
        /// </summary>
        public static RealismPreset Step(RealismPreset current, int step)
        {
            if (current == RealismPreset.Custom)
                return step < 0 ? RealismPreset.Relaxed : step > 0 ? RealismPreset.Chef : RealismPreset.Standard;
            var index = (int)current + step;
            if (index < (int)RealismPreset.Relaxed)
                index = (int)RealismPreset.Relaxed;
            if (index > (int)RealismPreset.Chef)
                index = (int)RealismPreset.Chef;
            return (RealismPreset)index;
        }
    }
}
