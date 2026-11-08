using System;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// Top-down character movement on the floor plane: velocity eases towards the
    /// stick and the character turns to face where it's going.
    /// Coordinates: X is right, Y is forward (world Z). Facing is a yaw in degrees,
    /// 0 = forward, increasing clockwise, matching Unity's Y rotation.
    /// </summary>
    public sealed class PlanarMover
    {
        // Below this stick magnitude there's no intent to move or turn.
        private const float IntentThreshold = 1e-4f;

        public PlanarMover(MovementSettings settings, float facingDegrees = 0f)
        {
            Settings = settings;
            FacingDegrees = NormalizeDegrees(facingDegrees);
        }

        /// <summary>Settings can change between ticks, so tuning applies live.</summary>
        public MovementSettings Settings { get; set; }

        /// <summary>Metres per second on the floor plane.</summary>
        public Vector2 Velocity { get; private set; }

        /// <summary>Yaw in degrees, in [0, 360).</summary>
        public float FacingDegrees { get; private set; }

        /// <param name="deltaTime">Seconds since the last tick.</param>
        /// <param name="stick">Movement input; magnitude above 1 is clamped, so diagonals aren't faster.</param>
        public void Tick(float deltaTime, Vector2 stick)
        {
            if (stick.LengthSquared() > 1f)
                stick = Vector2.Normalize(stick);

            var hasIntent = stick.LengthSquared() > IntentThreshold * IntentThreshold;
            var target = stick * Settings.MaxSpeed;

            // Constant-rate approach: rate = MaxSpeed / time, so going from 0 to full speed
            // takes exactly AccelerationTime regardless of frame rate. Analog stick input
            // gives a proportionally lower target speed.
            var rampTime = hasIntent ? Settings.AccelerationTime : Settings.DecelerationTime;
            Velocity = rampTime <= 0f
                ? target
                : MoveTowards(Velocity, target, Settings.MaxSpeed / rampTime * deltaTime);

            // Turn towards the stick, not the velocity: the character should look where
            // the player is steering even before momentum catches up.
            if (hasIntent)
            {
                var targetYaw = (float)(Math.Atan2(stick.X, stick.Y) * 180.0 / Math.PI);
                FacingDegrees = MoveTowardsAngle(FacingDegrees, targetYaw, Settings.TurnSpeedDegrees * deltaTime);
            }
        }

        /// <summary>Turns towards <paramref name="yawDegrees"/> at the usual turn speed, without moving: for when something other than the stick decides where to look.</summary>
        public void TurnTowards(float yawDegrees, float deltaTime) =>
            FacingDegrees = MoveTowardsAngle(FacingDegrees, yawDegrees, Settings.TurnSpeedDegrees * deltaTime);

        private static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDelta)
        {
            var delta = target - current;
            var distance = delta.Length();
            if (distance <= maxDelta || distance < float.Epsilon)
                return target;
            return current + delta / distance * maxDelta;
        }

        private static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            var delta = DeltaDegrees(current, target);
            if (Math.Abs(delta) <= maxDelta)
                return NormalizeDegrees(target);
            return NormalizeDegrees(current + Math.Sign(delta) * maxDelta);
        }

        /// <summary>Shortest signed difference from <paramref name="from"/> to <paramref name="to"/>, in [-180, 180).</summary>
        public static float DeltaDegrees(float from, float to)
        {
            var delta = NormalizeDegrees(to - from);
            return delta >= 180f ? delta - 360f : delta;
        }

        private static float NormalizeDegrees(float degrees)
        {
            var result = degrees % 360f;
            return result < 0f ? result + 360f : result;
        }
    }
}
