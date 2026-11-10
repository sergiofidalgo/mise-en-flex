namespace Game.Simulation.Cleaning
{
    /// <summary>
    /// Turns what happened in service into dirt (decision 2026-11-08: dirt comes from what happened),
    /// so a chaotic night leaves a messier morning. Gameplay reports each event with where it was;
    /// how much each leaves is in <see cref="DirtSettings"/>.
    /// </summary>
    public sealed class MessMaker
    {
        private readonly DirtMap _dirt;
        private int _guestsSinceScuff;

        public MessMaker(DirtMap dirt)
        {
            _dirt = dirt;
        }

        private DirtSettings Settings => _dirt.Settings;

        /// <summary>A guest finished eating: crumbs and sauce where their plate was.</summary>
        /// <param name="surface">Table, or Counter for a stool at the pass.</param>
        public DirtSpot DinerFinished(DirtSurface surface, object host, float x, float z) =>
            _dirt.Add(DirtKind.Smudge, surface, host, x, z, Settings.DinerSmudge);

        /// <summary>A plate dropped or broken: a spill on the floor.</summary>
        public DirtSpot PlateBroken(float x, float z) =>
            _dirt.Add(DirtKind.Spill, DirtSurface.Floor, null, x, z, Settings.PlateSpill);

        /// <summary>Food burnt in a pan on the heat: soot beside it, on a counter, or else the floor.</summary>
        /// <param name="counter">The counter beside the hob, or null for the floor.</param>
        public DirtSpot PanBurnt(object counter, float x, float z) =>
            _dirt.Add(DirtKind.Scorch, counter != null ? DirtSurface.Counter : DirtSurface.Floor, counter, x, z, Settings.BurntScorch);

        /// <summary>A guest walked through the dining room. Every few leave a scuff where they went.</summary>
        public DirtSpot GuestWalked(float x, float z)
        {
            _guestsSinceScuff++;
            if (_guestsSinceScuff < System.Math.Max(1, Settings.GuestsPerScuff))
                return null;
            _guestsSinceScuff = 0;
            return _dirt.Add(DirtKind.Scuff, DirtSurface.Floor, null, x, z, Settings.Scuff);
        }

        /// <summary>A fire was put out: foam on the floor in front of the hob.</summary>
        public DirtSpot FireOut(float x, float z) =>
            _dirt.Add(DirtKind.Foam, DirtSurface.Floor, null, x, z, Settings.FireFoam);
    }
}
