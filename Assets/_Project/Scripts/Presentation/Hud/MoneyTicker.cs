using System;

namespace Game.Presentation.Hud
{
    /// <summary>Which way the money last moved on the card.</summary>
    public enum MoneyChange
    {
        None,
        Up,
        Down,
    }

    /// <summary>The money card's animation timings (decision 2026-11-10), tuned in HudStyle.</summary>
    [Serializable]
    public struct MoneyTickerSettings
    {
        /// <summary>Seconds the number rolls up to a higher balance.</summary>
        public float RollSeconds;
        /// <summary>Seconds it stays fully green once it's there.</summary>
        public float HoldSeconds;
        /// <summary>Seconds the green or red fades back to the normal colour.</summary>
        public float FadeSeconds;
        /// <summary>Seconds the card shakes after the balance drops.</summary>
        public float ShakeSeconds;
        /// <summary>Canvas pixels of the first, widest wiggle.</summary>
        public float ShakeDistance;
        /// <summary>Full side-to-side wiggles in one shake.</summary>
        public float ShakeWiggles;

        public static MoneyTickerSettings Default => new MoneyTickerSettings
        {
            RollSeconds = 0.6f,
            HoldSeconds = 0.8f,
            FadeSeconds = 0.4f,
            ShakeSeconds = 0.4f,
            ShakeDistance = 6f,
            ShakeWiggles = 3f,
        };
    }

    /// <summary>
    /// What the money card shows from moment to moment (decision 2026-11-10): money coming in rolls
    /// the number up in green, money going out changes it at once with a short red shake, and either
    /// settles back to the normal colour. Plain C#, advanced by <see cref="Tick"/>, so it's tested
    /// without Play mode. Only changes that alter the whole amount shown animate: a few cents
    /// don't flash the card.
    /// </summary>
    public sealed class MoneyTicker
    {
        private MoneyTickerSettings _settings;
        private float _from;
        private float _elapsed;
        private bool _started;

        public MoneyTicker(MoneyTickerSettings settings)
        {
            _settings = settings;
        }

        /// <summary>The balance the card is heading to.</summary>
        public float Target { get; private set; }

        /// <summary>The amount to write on the card now: mid-roll while going up.</summary>
        public float Shown { get; private set; }

        /// <summary>The latest change's direction, None until the first one (or after a snap).</summary>
        public MoneyChange Direction { get; private set; }

        /// <summary>How much of the change colour to show, 1 fully green or red, 0 the normal colour.</summary>
        public float Tint { get; private set; }

        /// <summary>Canvas pixels to move the card sideways for the shake; 0 when still.</summary>
        public float Offset { get; private set; }

        /// <summary>True while anything is moving or tinted.</summary>
        public bool Animating => Tint > 0f || Offset != 0f || Shown != Target;

        public void Retune(MoneyTickerSettings settings) => _settings = settings;

        /// <summary>
        /// How the card writes an amount: whole money in the player's currency (Locale), and below
        /// zero with a minus sign, the user's call for money owed to the boss (2026-11-10).
        /// </summary>
        public static string Format(float amount) => Game.Data.Locale.Money(amount, Game.Data.MoneyStyle.Whole);

        /// <summary>Shows <paramref name="amount"/> at once, with no animation: on load, or a different campaign.</summary>
        public void Snap(float amount)
        {
            _started = true;
            Target = Shown = _from = amount;
            Direction = MoneyChange.None;
            Tint = 0f;
            Offset = 0f;
        }

        /// <summary>
        /// The balance is now <paramref name="amount"/>. The first value snaps; after that a change
        /// in the whole amount starts a roll up or a shake down from what's on the card.
        /// </summary>
        /// <param name="paidOut">Money was just paid out on purpose, such as a portion bought at a
        /// stall: it shakes red even if only the cents changed (decision 2026-11-11), so every
        /// purchase is felt.</param>
        public void Set(float amount, bool paidOut = false)
        {
            if (!_started)
            {
                Snap(amount);
                return;
            }
            if (Math.Abs(amount - Target) < 0.0001f)
                return;
            if (Whole(amount) == Whole(Target) && !(paidOut && amount < Target))
            {
                // Cents only: follow quietly, without restarting what's playing.
                var rolling = Direction == MoneyChange.Up && Shown != Target;
                Target = amount;
                if (!rolling)
                    Shown = amount;
                return;
            }
            _from = Shown;
            Target = amount;
            Direction = amount > _from ? MoneyChange.Up : MoneyChange.Down;
            _elapsed = 0f;
            Tick(0f);
        }

        public void Tick(float deltaTime)
        {
            if (Direction == MoneyChange.None)
            {
                Shown = Target;
                return;
            }
            _elapsed += Math.Max(0f, deltaTime);
            var s = _settings;
            if (Direction == MoneyChange.Up)
            {
                var roll = Fraction(_elapsed, s.RollSeconds);
                // Ease out (cubic): it races at first and slows into the new figure, so the last
                // digits are readable as it lands.
                var eased = 1f - (1f - roll) * (1f - roll) * (1f - roll);
                Shown = roll >= 1f ? Target : _from + (Target - _from) * eased;
                Offset = 0f;
                Tint = FadeAfter(_elapsed - s.RollSeconds - s.HoldSeconds, s.FadeSeconds);
            }
            else
            {
                Shown = Target;
                // A sine wiggle shrinking to nothing: the first swing is ShakeDistance, and it ends
                // centred, since the sine is zero at the end of whole wiggles and the envelope is zero.
                var shake = Fraction(_elapsed, s.ShakeSeconds);
                Offset = shake >= 1f ? 0f : (float)Math.Sin(2.0 * Math.PI * s.ShakeWiggles * shake) * s.ShakeDistance * (1f - shake);
                Tint = FadeAfter(_elapsed - s.ShakeSeconds, s.FadeSeconds);
            }
        }

        private static float Fraction(float elapsed, float seconds) => seconds <= 0f ? 1f : Math.Min(1f, elapsed / seconds);

        // 1 until the fade starts (time < 0), then down to 0 over the fade.
        private static float FadeAfter(float time, float seconds)
        {
            if (time < 0f)
                return 1f;
            return seconds <= 0f ? 0f : Math.Max(0f, 1f - time / seconds);
        }

        // The whole amount as the card writes it (MoneyStyle.Whole rounds to the nearest).
        private static long Whole(float amount) => (long)Math.Round(amount, MidpointRounding.AwayFromZero);
    }
}
