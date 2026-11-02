using Game.Gameplay.Stations;
using Game.Simulation.CloseUp;
using UnityEngine;

namespace Game.Gameplay.CloseUp
{
    /// <summary>What a technique produced, for scoring. Techniques with no end (the hob) never complete.</summary>
    public readonly struct TechniqueResult
    {
        public TechniqueResult(bool isComplete, float quality)
        {
            IsComplete = isComplete;
            Quality = quality;
        }

        public bool IsComplete { get; }

        /// <summary>0..1, meaningful once complete.</summary>
        public float Quality { get; }
    }

    /// <summary>
    /// A hands-on technique performed in a station's close-up. Each technique is a prefab with
    /// its own camera; the framework (<see cref="CloseUpStation"/>, <see cref="PlayerCloseUp"/>)
    /// drives it through this interface, so new techniques plug in without framework changes.
    /// </summary>
    public interface ICloseUpTechnique
    {
        /// <summary>Called once when the station is bound to the scene's services.</summary>
        void Setup(Interactable station, KitchenServices services);

        /// <summary>A player opened the close-up: start rendering into <paramref name="target"/>.</summary>
        void Begin(RenderTexture target);

        /// <summary>One frame of the player's input. Real (unscaled) seconds.</summary>
        void HandleInput(CloseUpInput input, float deltaTime);

        /// <summary>Per-frame update while open, after input. Real seconds.</summary>
        void Tick(float deltaTime);

        /// <summary>The player left: stop rendering.</summary>
        void Finish();

        TechniqueResult Result { get; }
    }
}
