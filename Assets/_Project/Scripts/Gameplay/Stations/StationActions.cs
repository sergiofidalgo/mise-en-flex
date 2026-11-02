using System.Collections.Generic;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The four face buttons, by position (Xbox A/B/X/Y; keyboard X/D/A/W). Storage slots use
    /// the same order, laid out on the unit in the same diamond.
    /// </summary>
    public enum FaceButton
    {
        South, // A, keyboard X: also Interact
        East,  // B, keyboard D
        West,  // X, keyboard A: also Use
        North, // Y, keyboard W
    }

    /// <summary>Controls shown in station prompts. Glyphs depend on the player's device.</summary>
    public enum PromptControl
    {
        South,
        East,
        West,
        North,
        Triggers,
    }

    /// <summary>One line of a station's button prompt, e.g. "A  Steak ×6".</summary>
    public readonly struct PromptLine
    {
        public PromptLine(PromptControl control, string label, bool available = true)
        {
            Control = control;
            Label = label;
            Available = available;
        }

        public PromptControl Control { get; }
        public string Label { get; }

        /// <summary>False greys the line out (e.g. an empty slot, nothing to flip).</summary>
        public bool Available { get; }
    }

    /// <summary>
    /// A station with something to say about its controls while a player faces it. Shown as a
    /// small prompt above the station so controls are discoverable without a manual.
    /// </summary>
    public interface IStationPrompt
    {
        /// <summary>True to lay the four face buttons out as a diamond (storage); otherwise a list.</summary>
        bool PromptAsDiamond { get; }

        void DescribeActions(PlayerInteractor player, List<PromptLine> lines);
    }

    /// <summary>
    /// A station that reacts to specific face buttons when the player's hands are empty, e.g.
    /// storage gives the ingredient in that button's slot.
    /// </summary>
    public interface IFaceButtonTarget
    {
        /// <returns>False if nothing could happen (empty slot), for the "can't" feedback.</returns>
        bool TryPress(FaceButton button, PlayerInteractor player);
    }
}
