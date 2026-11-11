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
        private int _guestsSinceOpenKitchenMess;

        public MessMaker(DirtMap dirt)
        {
            _dirt = dirt;
        }

        private DirtSettings Settings => _dirt.Settings;

        /// <summary>
        /// The hygiene realism dial (decision 2026-11-10): multiplies how much each event leaves, so
        /// 0.5 is half the cleaning and 0 leaves none. How often scuffs happen doesn't change, only
        /// how much each takes, so the mess still comes from what happened.
        /// </summary>
        public float Hygiene { get; set; } = 1f;

        private float Amount(float wipes) => wipes * System.Math.Max(0f, Hygiene);

        /// <summary>A guest finished eating: crumbs and sauce where their plate was.</summary>
        /// <param name="surface">Table, or Counter for a stool at the pass.</param>
        public DirtSpot DinerFinished(DirtSurface surface, object host, float x, float z) =>
            _dirt.Add(DirtKind.Smudge, surface, host, x, z, Amount(Settings.DinerSmudge));

        /// <summary>A plate dropped or broken: a spill on the floor.</summary>
        public DirtSpot PlateBroken(float x, float z) =>
            _dirt.Add(DirtKind.Spill, DirtSurface.Floor, null, x, z, Amount(Settings.PlateSpill));

        /// <summary>Food burnt in a pan on the heat: soot beside it, on a counter, or else the floor.</summary>
        /// <param name="counter">The counter beside the hob, or null for the floor.</param>
        public DirtSpot PanBurnt(object counter, float x, float z) =>
            _dirt.Add(DirtKind.Scorch, counter != null ? DirtSurface.Counter : DirtSurface.Floor, counter, x, z, Amount(Settings.BurntScorch));

        /// <summary>A guest walked through the dining room. Every few leave a scuff where they went.</summary>
        public DirtSpot GuestWalked(float x, float z)
        {
            _guestsSinceScuff++;
            if (_guestsSinceScuff < System.Math.Max(1, Settings.GuestsPerScuff))
                return null;
            _guestsSinceScuff = 0;
            return _dirt.Add(DirtKind.Scuff, DirtSurface.Floor, null, x, z, Amount(Settings.Scuff));
        }

        /// <summary>
        /// A guest was served while pieces stand outside their usual room (decision 2026-11-11: an
        /// open kitchen always makes more mess). Every few guests, each such piece leaves dirt on the
        /// floor beside it: grease by an appliance in the dining room, footprints by a table in the
        /// kitchen, where guests and cooks cross. Returns the spots made (none for most guests).
        /// </summary>
        public System.Collections.Generic.List<DirtSpot> OpenKitchenGuest(System.Collections.Generic.IReadOnlyList<Service.OutOfPlacePiece> pieces)
        {
            var made = new System.Collections.Generic.List<DirtSpot>();
            if (pieces == null || pieces.Count == 0)
                return made;
            _guestsSinceOpenKitchenMess++;
            if (_guestsSinceOpenKitchenMess < System.Math.Max(1, Settings.GuestsPerOpenKitchenMess))
                return made;
            _guestsSinceOpenKitchenMess = 0;
            foreach (var piece in pieces)
            {
                var kind = piece.Kind == Service.OutOfPlaceKind.ApplianceInDiningRoom ? DirtKind.Scorch : DirtKind.Scuff;
                var spot = _dirt.Add(kind, DirtSurface.Floor, null, piece.X, piece.Z, Amount(Settings.OpenKitchenMess));
                if (spot != null)
                    made.Add(spot);
            }
            return made;
        }

        /// <summary>A fire was put out: foam on the floor in front of the hob.</summary>
        public DirtSpot FireOut(float x, float z) =>
            _dirt.Add(DirtKind.Foam, DirtSurface.Floor, null, x, z, Amount(Settings.FireFoam));
    }
}
