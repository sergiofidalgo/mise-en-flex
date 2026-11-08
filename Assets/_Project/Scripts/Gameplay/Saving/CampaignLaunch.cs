namespace Game.Gameplay.Saving
{
    /// <summary>
    /// What the players chose on the title card, kept across the scene reload that loads a save.
    /// A deliberate small static, like the one exception noted for rejoining (decisions.md): it's the
    /// only thing that has to outlive the scene. The boss's office replaces the card in Phase 6.
    /// </summary>
    public static class CampaignLaunch
    {
        /// <summary>True once Continue or New campaign has been chosen this session.</summary>
        public static bool Decided { get; private set; }

        /// <summary>True if the scene should load the saved campaign as it starts.</summary>
        public static bool WantsSave { get; private set; }

        public static void Continue()
        {
            Decided = true;
            WantsSave = true;
        }

        public static void StartNew()
        {
            Decided = true;
            WantsSave = false;
        }

        /// <summary>Back to a fresh launch: also as play mode starts, in case statics survive between runs.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Decided = false;
            WantsSave = false;
        }
    }
}
