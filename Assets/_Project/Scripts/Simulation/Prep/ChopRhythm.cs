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

    /// <summary>
    /// Scores knife chops against a steady beat (decision 2026-11-02: follow a beat). The first
    /// chop sets the beat; each later chop is graded by how close it lands to the nearest beat.
    /// A long pause restarts the beat, so stopping to look around isn't punished beyond one miss.
    /// </summary>
    public sealed class ChopRhythm
    {
        // After this many beats without a chop, the next chop starts a new beat.
        private const double RestartAfterBeats = 2.5;

        private double? _anchor;
        private double _lastHit;
        private float _total;

        /// <param name="interval">Real seconds between beats.</param>
        /// <param name="goodWindow">± seconds around a beat that count as Good.</param>
        /// <param name="okWindow">± seconds that count as Early/Late; beyond is a Miss.</param>
        public ChopRhythm(float interval, float goodWindow, float okWindow)
        {
            Interval = interval;
            GoodWindow = goodWindow;
            OkWindow = okWindow;
        }

        public float Interval { get; }
        public float GoodWindow { get; }
        public float OkWindow { get; }

        public int Hits { get; private set; }

        public ChopGrade? LastGrade { get; private set; }

        /// <summary>0..1 average of the chops so far (Good 1, Early/Late 0.6, Miss 0.2); 1 before any chop.</summary>
        public float Evenness => Hits == 0 ? 1f : _total / Hits;

        public ChopGrade Hit(double time)
        {
            ChopGrade grade;
            if (_anchor == null || time - _lastHit > RestartAfterBeats * Interval)
            {
                // First chop (or first after a pause) sets the beat. After a pause it counts as a miss.
                grade = _anchor == null ? ChopGrade.Good : ChopGrade.Miss;
                _anchor = time;
            }
            else
            {
                var beats = Math.Round((time - _anchor.Value) / Interval);
                var error = time - (_anchor.Value + beats * Interval);
                var distance = Math.Abs(error);
                grade = distance <= GoodWindow ? ChopGrade.Good
                    : distance <= OkWindow ? (error < 0 ? ChopGrade.Early : ChopGrade.Late)
                    : ChopGrade.Miss;
            }

            _lastHit = time;
            Hits++;
            LastGrade = grade;
            _total += grade switch
            {
                ChopGrade.Good => 1f,
                ChopGrade.Early => 0.6f,
                ChopGrade.Late => 0.6f,
                _ => 0.2f,
            };
            return grade;
        }

        /// <summary>
        /// 0..1 position within the current beat (0 = on the beat), for the pulsing indicator.
        /// Null before the first chop.
        /// </summary>
        public double? BeatPhase(double now)
        {
            if (_anchor == null)
                return null;
            var phase = (now - _anchor.Value) / Interval;
            return phase - Math.Floor(phase);
        }
    }
}
