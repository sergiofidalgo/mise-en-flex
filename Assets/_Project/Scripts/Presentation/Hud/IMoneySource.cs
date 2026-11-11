namespace Game.Presentation.Hud
{
    /// <summary>Where the money card reads the campaign's balance: a kitchen's day, or the office's campaign.</summary>
    public interface IMoneySource
    {
        /// <summary>
        /// The balance now, or null where there's none to show yet. <paramref name="reset"/> is true
        /// when it's a different campaign from the last read, so the card snaps instead of animating.
        /// </summary>
        float? Read(out bool reset);

        /// <summary>True while a page of this scene sits over the card's corner.</summary>
        bool Covered { get; }

        /// <summary>True once after money was paid out on purpose (a stall purchase), for the card's shake.</summary>
        bool TakePaidOut() => false;
    }
}
