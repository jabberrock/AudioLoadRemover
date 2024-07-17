namespace AudioLoadRemover
{
    internal class LoadDetector
    {
        public record Segment(TimeSpan Start, TimeSpan End);

        public record Sequence(string StartClipName, string EndClipName, TimeSpan StartOffset, TimeSpan EndOffset)
        {
            public static readonly TimeSpan MaxTimeBetweenEvents = TimeSpan.FromSeconds(20.0);

            public List<Segment> Match(List<AudioClipDetector.Match> orderedMatches)
            {
                var sequenceMatches = new List<Segment>();
                for (var i = 0; i < orderedMatches.Count - 1; ++i)
                {
                    if ((orderedMatches[i].Query.Name == this.StartClipName) &&
                        (orderedMatches[i + 1].Query.Name == this.EndClipName) &&
                        (orderedMatches[i + 1].StartTime - orderedMatches[i].EndTime < MaxTimeBetweenEvents))
                    {
                        sequenceMatches.Add(new Segment(orderedMatches[i].StartTime, orderedMatches[i + 1].EndTime));
                    }
                }

                return sequenceMatches;
            }
        }

        public static List<Segment> Detect(List<AudioClipDetector.Match> matches, List<Sequence> config)
        {
            var orderedMatches = matches.OrderBy(m => m.StartTime).ToList();

            var loadSegments = new List<Segment>();
            foreach (var sequence in config)
            {
                loadSegments.AddRange(sequence.Match(orderedMatches));
            }

            return loadSegments.OrderBy(s => s.Start).ToList();
        }
    }
}
