using System.Diagnostics;
using System.Numerics;

namespace AudioLoadRemover
{
    internal class AudioClipDetector
    {
        private const int NumFramesPerTask = 1000;

        public record Match(
            string QueryName,
            string SourceName,
            TimeSpan StartTime, 
            TimeSpan EndTime, 
            int SampleRate,
            float Correlation);

        public static List<Match> Detect(AudioClip query, AudioClip source, int sampleRate, int numChannels)
        {
            Trace.WriteLine($"Searching for audio clip {query.Name} within {source.Name}");

            var querySamples = query.Samples;
            var sourceSamples = source.Samples;

            var numFramesToCrossCorr = source.Duration - query.Duration;
            var numChunks = (numFramesToCrossCorr + (NumFramesPerTask - 1)) / NumFramesPerTask;

            var crossCorrChunks =
                Enumerable.Range(0, numChunks)
                    .AsParallel()
                    .Select(chunkIndex =>
                        {
                            var frameIndex = chunkIndex * NumFramesPerTask;
                            var frameEndIndex = Math.Min(frameIndex + NumFramesPerTask, numFramesToCrossCorr);
                            
                            var crossCorrs = new float[frameEndIndex - frameIndex];

                            for (var i = frameIndex; i < frameEndIndex; ++i)
                            {
                                float corr = 0.0f;

                                for (var j = 0;
                                     j < querySamples.Length - Vector<float>.Count; // Don't go off the end of querySamples
                                     j += Vector<float>.Count)
                                {
                                    var v1 = new Vector<float>(querySamples, j);
                                    var v2 = new Vector<float>(sourceSamples, i * numChannels + j);
                                    corr += Vector.Sum(v1 * v2);
                                }

                                crossCorrs[i - frameIndex] = corr;
                            }

                            return crossCorrs;
                        })
                    .ToArray();

            var maxCorrTracker = new MaxValueTracker(querySamples.Length);
            foreach (var chunk in crossCorrChunks)
            {
                foreach (var corr in chunk)
                {
                    maxCorrTracker.Add(corr);
                }
            }

            var matches = new List<Match>();

            maxCorrTracker.SuppressNoise();
            foreach (var maxEntry in maxCorrTracker.MaxEntries)
            {
                matches.Add(
                    new Match(
                        query.Name,
                        source.Name,
                        TimeSpan.FromSeconds((float)maxEntry.Index / sampleRate), 
                        TimeSpan.FromSeconds((float)(maxEntry.Index + query.Duration) / sampleRate), 
                        sampleRate,
                        maxEntry.Value));
            }

            return matches;
        }
    }
}
