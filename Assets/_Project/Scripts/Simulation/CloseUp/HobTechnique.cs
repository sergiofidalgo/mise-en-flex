using System;
using Game.Simulation.Cooking;

namespace Game.Simulation.CloseUp
{
    /// <summary>
    /// What a player does in a hob's close-up: the triggers turn the heat smoothly, the stick
    /// picks which piece of food in the pan, and A flips it. Basting, resting and slicing come
    /// with the full steak technique (Phase 1, milestone 4).
    /// </summary>
    public sealed class HobTechnique
    {
        // A flick past this much stick moves the selection; it must return near centre before the next.
        private const float FlickThreshold = 0.5f;
        private const float CentreThreshold = 0.25f;

        private readonly Hob _hob;
        private bool _stickCentred = true;

        /// <param name="dialSpeed">Dial change per real second at full trigger (1 = off to full in a second).</param>
        public HobTechnique(Hob hob, float dialSpeed)
        {
            _hob = hob;
            DialSpeed = dialSpeed;
        }

        /// <summary>Can be changed between frames so tuning applies live.</summary>
        public float DialSpeed { get; set; }

        /// <summary>Index into the pan's contents of the food the player is pointing at, or -1.</summary>
        public int SelectedIndex { get; private set; } = -1;

        public Ingredient Selected =>
            _hob.Pan != null && SelectedIndex >= 0 && SelectedIndex < _hob.Pan.Contents.Count
                ? _hob.Pan.Contents[SelectedIndex]
                : null;

        /// <param name="realDeltaTime">Real seconds, not game time: input should feel the same at any time scale.</param>
        public void HandleInput(CloseUpInput input, float realDeltaTime)
        {
            _hob.Dial += (input.HeatUp - input.HeatDown) * DialSpeed * realDeltaTime;

            var count = _hob.Pan?.Contents.Count ?? 0;
            if (count == 0)
            {
                SelectedIndex = -1;
                return;
            }

            // Keep the selection valid as food comes and goes; default to the first piece.
            SelectedIndex = SelectedIndex < 0 ? 0 : Math.Min(SelectedIndex, count - 1);

            var x = input.Aim.X;
            if (_stickCentred && Math.Abs(x) >= FlickThreshold)
            {
                SelectedIndex = (SelectedIndex + (x > 0f ? 1 : count - 1)) % count;
                _stickCentred = false;
            }
            else if (Math.Abs(x) <= CentreThreshold)
            {
                _stickCentred = true;
            }

            if (input.PrimaryPressed)
                _hob.Pan.Contents[SelectedIndex].Flip();
        }
    }
}
