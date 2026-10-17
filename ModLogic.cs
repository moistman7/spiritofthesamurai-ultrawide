namespace SpiritOfTheSamuraiUltrawide
{
    internal static class ModLogic
    {
        // tops includes the final end position; off-screen rows are skipped with a binary search.
        internal static int FirstVisible(System.Collections.Generic.IList<float> tops, float scroll)
        {
            int low = 0, high = tops.Count - 1;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (tops[middle + 1] <= scroll) low = middle + 1;
                else high = middle;
            }
            return low;
        }
    }
    // No Unity dependencies: timing is supplied by the caller and can use unscaled time.
    internal struct RepeatGate
    {
        private int held;
        private float next;
        internal int Step(int direction, float now, float delay = 0.35f, float interval = 0.09f)
        {
            if (direction == 0) { held = 0; return 0; }
            if (direction != held) { held = direction; next = now + delay; return direction; }
            if (now < next) return 0;
            next = now + interval; // No catch-up burst after a slow frame.
            return direction;
        }
    }

    internal struct ChordGate
    {
        private bool held;
        internal bool Step(bool pressed)
        {
            bool fire = pressed && !held;
            held = pressed;
            return fire;
        }
    }
}
