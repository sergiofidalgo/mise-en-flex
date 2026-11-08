namespace Game.Editor
{
    /// <summary>
    /// How big food and kitchenware show, by where they are (the user's choice, 2026-11-08): big on
    /// counters and the board, where they sit to be worked on and must read at a glance; a little
    /// big while carried or on the grill; their own size where they must fit (fridge, shelves,
    /// plates, a pan).
    /// Set as the scale of the anchors that hold them; an item eases between sizes as it moves.
    /// Only looks: where things are for play doesn't change.
    /// </summary>
    public static class ItemSizes
    {
        /// <summary>Sitting on a counter or the cutting board.</summary>
        public const float OnCounter = 1.4f;

        /// <summary>In a chef's hands, or the knife at the hip.</summary>
        public const float Carried = 1.3f;

        /// <summary>On the grill. Not in a pan, which the steak is made to fill.</summary>
        public const float Cooking = 1.15f;
    }
}
