namespace AudioLoadRemover
{
    internal class MaxValueTracker
    {
        public MaxValueTracker(int windowSize)
        {
            this.index = 0;
            this.recentIndex = 0;
            this.recent = new float[windowSize];
        }

        public void Add(float value)
        {
            var isMax = true;
            for (int i = 0; i < this.recent.Length; ++i)
            {
                if (value < this.recent[i])
                {
                    isMax = false;
                    break;
                }
            }

            if (isMax)
            {
                if (this.maxEntries.Count > 0)
                {
                    var lastEntry = this.maxEntries.Last();
                    if ((this.index - lastEntry.Index < this.recent.Length) && (value > lastEntry.Value))
                    {
                        maxEntries.RemoveAt(this.maxEntries.Count - 1);
                    }
                }

                maxEntries.Add(new Entry(this.index, value));
            }

            ++this.index;

            this.recent[this.recentIndex] = value;
            this.recentIndex = (this.recentIndex + 1) % this.recent.Length;
        }

        public void SuppressNoise()
        {
            if (maxEntries.Count == 0)
            {
                return;
            }

            var maxValue = maxEntries.Max(e => e.Value);
            maxEntries.RemoveAll(e => e.Value < 0.8 * maxValue);
        }

        public List<Entry> MaxEntries
        {
            get { return this.maxEntries; }
        }

        public record struct Entry(int Index, float Value);

        private int index;
        private int recentIndex;
        private readonly float[] recent;
        private readonly List<Entry> maxEntries = new List<Entry>();
    }
}
