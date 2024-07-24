using NAudio.Wave;
using System.IO;
using System.Numerics;

namespace AudioLoadRemover
{
    public class AudioClipDetector
    {
        private const int NumFramesPerTask = 1000;

        public record Match(
            string QueryName,
            string SourceName,
            TimeSpan StartTime, 
            TimeSpan EndTime, 
            int SampleRate,
            float Correlation);

        // TODO: Remove numChannels since we're dealing with mono audio clips
        public static List<Match> Detect(AudioClip query, AudioClip source, int sampleRate, int numChannels, DebugOutput debugOutput)
        {
            debugOutput.Log($"Searching for audio clip {query.Name} within {source.Name}");

            var querySamples = query.HighPassFilteredSamples;
            var sourceSamples = source.HighPassFilteredSamples;

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
                                var corr = 0.0f;

                                var startIndex = query.SilentPrefix * numChannels;
                                var endIndex = querySamples.Length - (query.SilentSuffix * numChannels) - Vector<float>.Count; // Don't go off the end of querySamples
                                var step = Vector<float>.Count;

                                for (var j = startIndex; j < endIndex; j += step)
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

            maxCorrTracker.SuppressNoise(SuppressMaxThreshold);
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

                using (var waveFileWriter = new WaveFileWriter(Path.Combine(debugOutput.Folder, $"match-{query.Name}-{matches.Count}.wav"), source.WaveFormat))
                {
                    var startSampleIndex = Math.Max(0, maxEntry.Index - (NumSecPrefixAndSuffix * sampleRate));
                    var endSampleIndex = Math.Min(maxEntry.Index + query.Duration + (NumSecPrefixAndSuffix * sampleRate), source.Duration);

                    var matchSamples = new ReadOnlySpan<float>(sourceSamples, startSampleIndex, endSampleIndex - startSampleIndex).ToArray();
                    waveFileWriter.WriteSamples(matchSamples, 0, matchSamples.Length);
                }
            }

            return matches;
        }

        private const int NumSecPrefixAndSuffix = 5;
        private const float SuppressMaxThreshold = 0.8f;
    }
}
