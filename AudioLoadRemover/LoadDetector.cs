﻿namespace AudioLoadRemover
{
    internal class LoadDetector
    {
        public record Segment(TimeSpan Start, TimeSpan End, string SequenceName);

        public enum Anchor
        {
            Start,
            End,
        }

        public record Offset(Anchor Anchor, TimeSpan TimeSpan);

        public record Sequence(
            string Name,
            string StartClipName,
            string EndClipName,
            Offset StartOffset,
            Offset EndOffset)
        {
            public static readonly TimeSpan MaxTimeBetweenEvents = TimeSpan.FromSeconds(60.0);

            public List<Segment> Match(List<AudioClipDetector.Match> orderedMatches)
            {
                var sequenceMatches = new List<Segment>();
                for (var i = 0; i < orderedMatches.Count - 1; ++i)
                {
                    if ((orderedMatches[i].QueryName == this.StartClipName) &&
                        (orderedMatches[i + 1].QueryName == this.EndClipName) &&
                        (orderedMatches[i + 1].StartTime - orderedMatches[i].EndTime < MaxTimeBetweenEvents))
                    {
                        var loadStartTime =
                            this.StartOffset.Anchor == Anchor.Start
                                ? orderedMatches[i].StartTime + this.StartOffset.TimeSpan
                                : orderedMatches[i].EndTime + this.StartOffset.TimeSpan;

                        var loadEndTime =
                            this.EndOffset.Anchor == Anchor.Start
                                ? orderedMatches[i + 1].StartTime + this.EndOffset.TimeSpan
                                : orderedMatches[i + 1].EndTime + this.EndOffset.TimeSpan;

                        sequenceMatches.Add(new Segment(loadStartTime, loadEndTime, this.Name));
                    }
                }

                return sequenceMatches;
            }
        }

        public static List<Segment> Detect(List<AudioClipDetector.Match> orderedMatches, List<Sequence> config)
        {
            var loadSegments = new List<Segment>();
            foreach (var sequence in config)
            {
                loadSegments.AddRange(sequence.Match(orderedMatches));
            }

            return loadSegments.OrderBy(s => s.Start).ToList();
        }
    }
}
