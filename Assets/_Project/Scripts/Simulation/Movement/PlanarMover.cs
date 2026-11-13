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

        /// <summary>True while there's no stick intent and the character has (all but) stopped: where the facing assist may act.</summary>
        public bool IsSettled { get; private set; }

        // Below this speed (m/s) a character counts as stopped, for the facing assist.
        private const float SettledSpeed = 0.05f;

        /// <param name="deltaTime">Seconds since the last tick.</param>
        /// <param name="stick">Movement input; magnitude above 1 is clamped, so diagonals aren't faster.</param>
        /// <param name="turnOnly">
        /// Planted (holding X to clean, decision 2026-11-11): the stick turns the character on the
        /// spot and never walks them; any speed left dies away as when the stick is let go.
        /// </param>
        /// <param name="speedScale">Share of full speed allowed (floor dirt slows a chef, decision 2026-11-11).</param>
        public void Tick(float deltaTime, Vector2 stick, bool turnOnly = false, float speedScale = 1f)
        {
            if (stick.LengthSquared() > 1f)
                stick = Vector2.Normalize(stick);

            var hasIntent = stick.LengthSquared() > IntentThreshold * IntentThreshold;
            var target = turnOnly ? Vector2.Zero : stick * (Settings.MaxSpeed * Math.Clamp(speedScale, 0f, 1f));

            // Constant-rate approach: rate = MaxSpeed / time, so going from 0 to full speed
            // takes exactly AccelerationTime regardless of frame rate. Analog stick input
            // gives a proportionally lower target speed.
            var rampTime = hasIntent && !turnOnly ? Settings.AccelerationTime : Settings.DecelerationTime;
            Velocity = rampTime <= 0f
                ? target
                : MoveTowards(Velocity, target, Settings.MaxSpeed / rampTime * deltaTime);

            // Turn towards the stick, not the velocity: the character should look where
            // the player is steering even before momentum catches up. The lighter the push, the
            // gentler the turn (decision 2026-11-11): a nudge to line up at a counter doesn't
            // swing the chef away from it.
            if (hasIntent)
            {
                var targetYaw = (float)(Math.Atan2(stick.X, stick.Y) * 180.0 / Math.PI);
                FacingDegrees = MoveTowardsAngle(FacingDegrees, targetYaw, TurnSpeedFor(stick.Length()) * deltaTime);
            }
            IsSettled = !hasIntent && Velocity.Length() < SettledSpeed;
        }

        /// <summary>Degrees a second the stick turns the character at this push (0..1).</summary>
        public float TurnSpeedFor(float push)
        {
            var share = Math.Clamp(Settings.LightPushTurnShare, 0f, 1f);
            return Settings.TurnSpeedDegrees * (share + (1f - share) * Math.Clamp(push, 0f, 1f));
        }

        /// <summary>
        /// The facing assist (decision 2026-11-11): a character who has stopped, standing at
        /// <paramref name="position"/>, turns gently to face <paramref name="station"/> if it's within
        /// <see cref="MovementSettings.AssistRange"/> and roughly in front of them
        /// (<see cref="MovementSettings.AssistHalfAngleDegrees"/>). Does nothing while moving or steering.
        /// </summary>
        /// <returns>True if it turned (or was already facing it).</returns>
        public bool Assist(float deltaTime, Vector2 position, Vector2 station)
        {
            if (!IsSettled || !WouldAssist(FacingDegrees, position, station, Settings))
                return false;
            var toward = station - position;
            var yaw = (float)(Math.Atan2(toward.X, toward.Y) * 180.0 / Math.PI);
            FacingDegrees = MoveTowardsAngle(FacingDegrees, yaw, Settings.AssistTurnSpeedDegrees * deltaTime);
            return true;
        }

        /// <summary>Whether a station at <paramref name="station"/> is close enough, and enough in front, for the assist.</summary>
        public static bool WouldAssist(float facingDegrees, Vector2 position, Vector2 station, MovementSettings settings)
        {
            var toward = station - position;
            var distance = toward.Length();
            if (settings.AssistRange <= 0f || distance > settings.AssistRange || distance < 1e-3f)
                return false;
            var yaw = (float)(Math.Atan2(toward.X, toward.Y) * 180.0 / Math.PI);
            return Math.Abs(DeltaDegrees(facingDegrees, yaw)) <= settings.AssistHalfAngleDegrees;
        }

        /// <summary>Faces a given way at once (a station turning the chef to its work, a test placing them).</summary>
        public void Face(float degrees) => FacingDegrees = NormalizeDegrees(degrees);

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
