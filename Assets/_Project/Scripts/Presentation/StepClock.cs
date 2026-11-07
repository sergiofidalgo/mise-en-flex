namespace Game.Presentation
{
    /// <summary>
    /// Says when a walker's foot lands. Counts distance rather than time, so steps speed up and slow
    /// down with how fast they're really going, and walking into a wall makes no steps at all.
    /// </summary>
    public sealed class StepClock
    {
        private float _walked;

        /// <param name="start">How far into a stride they are now, in metres.</param>
        public void Stop(float start) => _walked = start;

        /// <returns>True if a foot lands within this distance.</returns>
        public bool Walk(float metres, float stride)
        {
            _walked += metres;
            if (_walked < stride)
                return false;

            // Keep what's left over, so the rhythm doesn't drift with the frame rate.
            _walked %= stride;
            return true;
        }
    }
}
