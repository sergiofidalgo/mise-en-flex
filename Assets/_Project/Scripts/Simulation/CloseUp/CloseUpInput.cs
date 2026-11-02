using System.Numerics;

namespace Game.Simulation.CloseUp
{
    /// <summary>
    /// One frame of close-up input, read from the CloseUp controls by gameplay. Plain data so
    /// technique logic can be tested without Unity's input system.
    /// </summary>
    public readonly struct CloseUpInput
    {
        public CloseUpInput(
            Vector2 aim,
            bool primaryPressed,
            bool secondaryHeld,
            bool tertiaryPressed,
            float heatDown,
            float heatUp)
        {
            Aim = aim;
            PrimaryPressed = primaryPressed;
            SecondaryHeld = secondaryHeld;
            TertiaryPressed = tertiaryPressed;
            HeatDown = heatDown;
            HeatUp = heatUp;
        }

        /// <summary>Left stick / arrow keys, -1..1 on each axis.</summary>
        public Vector2 Aim { get; }

        /// <summary>A (keyboard X) went down this frame.</summary>
        public bool PrimaryPressed { get; }

        /// <summary>X (keyboard A) is held.</summary>
        public bool SecondaryHeld { get; }

        /// <summary>Y (keyboard W) went down this frame.</summary>
        public bool TertiaryPressed { get; }

        /// <summary>Left trigger (keyboard Q), 0..1.</summary>
        public float HeatDown { get; }

        /// <summary>Right trigger (keyboard E), 0..1.</summary>
        public float HeatUp { get; }
    }
}
