namespace SpiritOfTheSamuraiUltrawide
{
    internal static class ModLogic
    {
        internal static float FullscreenWidthFactor(float left, float right, float pivot, float width)
        {
            if (width <= 0f || pivot <= left || pivot >= right) return 1f;
            // One pixel of overlap avoids a fractional-pixel seam along the screen edges.
            return System.Math.Max(1f, System.Math.Max((pivot + 1f) / (pivot - left), (width + 1f - pivot) / (right - pivot)));
        }
        internal static int NextAvailable(bool[] available, int current, int direction)
        {
            if (available.Length == 0 || direction == 0) return current;
            int candidate = current;
            for (int attempt = 0; attempt < available.Length; attempt++)
            {
                candidate = (candidate + (direction < 0 ? -1 : 1) + available.Length) % available.Length;
                if (available[candidate]) return candidate;
            }
            return current;
        }
        // Native input may run before or after the mod: never advance a newly started/replaced line.
        internal static bool ShouldFallbackContinue(object requested, object current, int subtitleFrame,
            int requestFrame, bool waiting) => requested != null && ReferenceEquals(requested, current) &&
            subtitleFrame < requestFrame && waiting;

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
