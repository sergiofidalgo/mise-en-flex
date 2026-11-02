using System;

namespace Game.Simulation.Prep
{
    public enum ChopGrade
    {
        Good,
        Early,
        Late,
        Miss,
    }

    /// <summary>How a cut's grade changes the needle's speed (decision 2026-11-02).</summary>
    public readonly struct ChopPacing
    {
        public ChopPacing(float afterGood, float afterOk, float afterMiss, float minMultiplier, float maxMultiplier)
        {
            AfterGood = afterGood;
            AfterOk = afterOk;
            AfterMiss = afterMiss;
            MinMultiplier = minMultiplier;
            MaxMultiplier = maxMultiplier;
        }

        /// <summary>Speed multiplier after a Good cut: above 1, so a streak builds momentum.</summary>
        public float AfterGood { get; }

        /// <summary>After an Early/Late cut: slightly below 1.</summary>
        public float AfterOk { get; }

        /// <summary>After a Miss: well below 1, which also makes it easier to recover.</summary>
        public float AfterMiss { get; }

        /// <summary>Limits on the needle's speed relative to the ingredient's base speed.</summary>
        public float MinMultiplier { get; }
        public float MaxMultiplier { get; }

        public static ChopPacing Default => new ChopPacing(1.15f, 0.95f, 0.75f, 0.5f, 2f);
    }

    /// <summary>
    /// The cutting check (decision 2026-11-02): a needle sweeps across a half arc from its right
    /// end to its left end, then starts again from the right, and the chef presses X as it passes
    /// through the ingredient's sweet spot. Each ingredient places the spot at its own position and
    /// width. Inside the core of the spot is Good, its edges Early (not there yet) or Late (just
    /// past), anywhere else a Miss. A miss still cuts, just less evenly.
    ///
    /// The needle's speed reacts to each cut: Good speeds it up progressively, Early/Late slow it
    /// subtly, a Miss slows it significantly. Positions are degrees along the sweep from its right end.
    /// </summary>
    public sealed class ChopDial
    {
        private readonly float _baseSpeed;
        private readonly ChopPacing _pacing;
        private double? _anchorTime;
        private double _anchorPosition;
        private float _speed;
        private float _total;

        /// <param name="secondsPerSweep">Real seconds for one sweep, right to left, at the ingredient's base speed.</param>
        /// <param name="sweepDegrees">How wide the arc is (≈150–180).</param>
        /// <param name="sweetSpot">Centre of the sweet spot, degrees along the sweep from the right end.</param>
        /// <param name="goodHalfWidth">Degrees either side of the centre that count as Good.</param>
        /// <param name="okHalfWidth">Degrees either side that count as Early/Late; beyond is a Miss.</param>
        public ChopDial(float secondsPerSweep, float sweepDegrees, float sweetSpot, float goodHalfWidth, float okHalfWidth, ChopPacing? pacing = null)
        {
            SweepDegrees = Math.Max(10f, sweepDegrees);
            _baseSpeed = SweepDegrees / Math.Max(0.05f, secondsPerSweep);
            _speed = _baseSpeed;
            _pacing = pacing ?? ChopPacing.Default;
            SweetSpot = Math.Clamp(sweetSpot, 0f, SweepDegrees);
            GoodHalfWidth = goodHalfWidth;
            OkHalfWidth = Math.Max(goodHalfWidth, okHalfWidth);
        }

        public float SweepDegrees { get; }
        public float SweetSpot { get; }
        public float GoodHalfWidth { get; }
        public float OkHalfWidth { get; }

        /// <summary>Current speed relative to the ingredient's base speed.</summary>
        public float SpeedMultiplier => _speed / _baseSpeed;

        public int Hits { get; private set; }

        public ChopGrade? LastGrade { get; private set; }

        /// <summary>0..1 average of the chops so far (Good 1, Early/Late 0.6, Miss 0.2); 1 before any chop.</summary>
        public float Evenness => Hits == 0 ? 1f : _total / Hits;

        /// <summary>Starts the needle at the right end at <paramref name="time"/>, unless it's already moving.</summary>
        public void Start(double time)
        {
            if (_anchorTime == null)
            {
                _anchorTime = time;
                _anchorPosition = 0.0;
            }
        }

        /// <summary>Where the needle is at real time <paramref name="time"/>: degrees from the right end, in [0, sweep). Starts it if needed.</summary>
        public float NeedlePosition(double time)
        {
            Start(time);
            var travelled = _anchorPosition + (time - _anchorTime.Value) * _speed;
            var position = travelled % SweepDegrees;
            return (float)(position < 0 ? position + SweepDegrees : position);
        }

        /// <summary>Real seconds from <paramref name="time"/> until the needle next reaches <paramref name="position"/>.</summary>
        public double SecondsUntil(float position, double time)
        {
            var ahead = (position - NeedlePosition(time)) % SweepDegrees;
            if (ahead < 0f)
                ahead += SweepDegrees;
            return ahead / _speed;
        }

        /// <summary>The grade a press at <paramref name="time"/> would get, without recording it.</summary>
        public ChopGrade GradeAt(double time)
        {
            // Signed distance from the spot's centre; negative = the needle hasn't reached it yet.
            var delta = NeedlePosition(time) - SweetSpot;
            var distance = Math.Abs(delta);
            if (distance <= GoodHalfWidth)
                return ChopGrade.Good;
            if (distance <= OkHalfWidth)
                return delta < 0f ? ChopGrade.Early : ChopGrade.Late;
            return ChopGrade.Miss;
        }

        public ChopGrade Hit(double time)
        {
            var grade = GradeAt(time);
            Hits++;
            LastGrade = grade;
            _total += grade switch
            {
                ChopGrade.Good => 1f,
                ChopGrade.Early => 0.6f,
                ChopGrade.Late => 0.6f,
                _ => 0.2f,
            };

            // Keep the needle where it is and change only its speed from here on.
            _anchorPosition = NeedlePosition(time);
            _anchorTime = time;
            var factor = grade switch
            {
                ChopGrade.Good => _pacing.AfterGood,
                ChopGrade.Miss => _pacing.AfterMiss,
                _ => _pacing.AfterOk,
            };
            _speed = Math.Clamp(_speed * factor, _baseSpeed * _pacing.MinMultiplier, _baseSpeed * _pacing.MaxMultiplier);
            return grade;
        }
    }
}
