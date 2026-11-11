using UnityEngine;

namespace Game.Presentation
{
    /// <summary>What the stick cue shows the player to do with the left stick.</summary>
    public enum StickMotion
    {
        /// <summary>Push towards a point (the sink: bring the soap to the centre).</summary>
        Push,

        /// <summary>Spin it round (the cloth and the mop).</summary>
        Spin,

        /// <summary>Wag it from side to side (the extinguisher).</summary>
        Wag,
    }

    /// <summary>One stick cue a view asks for (decision 2026-11-09: the station is the interface).</summary>
    public readonly struct StickHint
    {
        public StickHint(float amount, Vector3 world, Vector3 towards, StickMotion motion)
        {
            Amount = amount;
            World = world;
            Towards = towards;
            Motion = motion;
        }

        /// <summary>0..1: how much it shows.</summary>
        public float Amount { get; }

        /// <summary>Where it goes, in the world.</summary>
        public Vector3 World { get; }

        /// <summary>For a push: a short world vector the way to push.</summary>
        public Vector3 Towards { get; }

        public StickMotion Motion { get; }
    }

    /// <summary>A view that can ask the HUD for stick cues (the sink, cleaning): <see cref="Hud.StickHintHud"/> draws them.</summary>
    public interface IStickHintSource
    {
        int HintCount { get; }

        StickHint HintAt(int index);
    }
}
