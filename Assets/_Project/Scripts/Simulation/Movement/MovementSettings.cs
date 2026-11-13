namespace Game.Simulation.Movement
{
    /// <summary>
    /// Tuning for <see cref="PlanarMover"/>. Times are how long it takes to go from
    /// standstill to full speed (and back); zero or less means instant.
    /// </summary>
    public readonly struct MovementSettings
    {
        public MovementSettings(float maxSpeed, float accelerationTime, float decelerationTime, float turnSpeedDegrees,
            float lightPushTurnShare = 1f, float assistRange = 0f, float assistHalfAngleDegrees = 0f, float assistTurnSpeedDegrees = 0f)
        {
            MaxSpeed = maxSpeed;
            AccelerationTime = accelerationTime;
            DecelerationTime = decelerationTime;
            TurnSpeedDegrees = turnSpeedDegrees;
            LightPushTurnShare = lightPushTurnShare;
            AssistRange = assistRange;
            AssistHalfAngleDegrees = assistHalfAngleDegrees;
            AssistTurnSpeedDegrees = assistTurnSpeedDegrees;
        }

        /// <summary>Metres per second at full stick.</summary>
        public float MaxSpeed { get; }

        /// <summary>Seconds from standstill to full speed.</summary>
        public float AccelerationTime { get; }

        /// <summary>Seconds from full speed to standstill after the stick is released.</summary>
        public float DecelerationTime { get; }

        /// <summary>Degrees per second the character turns towards the stick direction at full push.</summary>
        public float TurnSpeedDegrees { get; }

        /// <summary>
        /// Share of <see cref="TurnSpeedDegrees"/> at the lightest push, rising straight to all of it
        /// at full push (decision 2026-11-11, playtest round): a small nudge of the stick turns the
        /// chef gently instead of swinging them off the counter. 1 turns at full speed however lightly.
        /// </summary>
        public float LightPushTurnShare { get; }

        /// <summary>Metres to a station's centre within which a chef who has stopped turns to face it. 0 turns the assist off.</summary>
        public float AssistRange { get; }

        /// <summary>Degrees either side of facing within which the assist turns the chef to a station.</summary>
        public float AssistHalfAngleDegrees { get; }

        /// <summary>Degrees per second of the assist's turn: gentle, so it reads as settling, not snapping.</summary>
        public float AssistTurnSpeedDegrees { get; }
    }
}
