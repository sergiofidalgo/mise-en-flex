namespace Game.Simulation.Movement
{
    /// <summary>
    /// Tuning for <see cref="PlanarMover"/>. Times are how long it takes to go from
    /// standstill to full speed (and back); zero or less means instant.
    /// </summary>
    public readonly struct MovementSettings
    {
        public MovementSettings(float maxSpeed, float accelerationTime, float decelerationTime, float turnSpeedDegrees)
        {
            MaxSpeed = maxSpeed;
            AccelerationTime = accelerationTime;
            DecelerationTime = decelerationTime;
            TurnSpeedDegrees = turnSpeedDegrees;
        }

        /// <summary>Metres per second at full stick.</summary>
        public float MaxSpeed { get; }

        /// <summary>Seconds from standstill to full speed.</summary>
        public float AccelerationTime { get; }

        /// <summary>Seconds from full speed to standstill after the stick is released.</summary>
        public float DecelerationTime { get; }

        /// <summary>Degrees per second the character turns towards the stick direction.</summary>
        public float TurnSpeedDegrees { get; }
    }
}
