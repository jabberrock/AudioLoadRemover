using System.Numerics;

namespace AudioLoadRemover
{
    internal class AudioCorrelationBuffer
    {
        public AudioCorrelationBuffer(ReadOnlySpan<float> initialSamples)
        {
            this.samples = initialSamples.ToArray();
        }

        /// <summary>
        /// Appends the samples to the end of the buffer, discarding earlier samples.
        /// </summary>
        public void Add(ReadOnlySpan<float> newSamples)
        {
            var samples = this.samples;
            if (newSamples.Length < samples.Length)
            {
                var numRemaining = samples.Length - newSamples.Length;

                var remaining = new ReadOnlySpan<float>(samples, newSamples.Length, numRemaining);
                remaining.CopyTo(samples);

                newSamples.CopyTo(new Span<float>(samples, numRemaining, newSamples.Length));
            }
            else if (newSamples.Length == samples.Length)
            {
                newSamples.CopyTo(samples);
            }
            else
            {
                throw new Exception("newSamples has too many samples to fit in buffer");
            }
        }

        /// <summary>
        /// Calculates the cross correlation between otherSamples, and the samples within this buffer.
        /// </summary>
        public List<float> CrossCorrelation(float[] otherSamples, int numCrossCorrs)
        {
            if (otherSamples.Length + numCrossCorrs > this.samples.Length)
            {
                throw new Exception("otherSamples and correlations does not fit in buffer");
            }

            var samples = this.samples;
            var crossCorrsChunks =
                Enumerable.Range(0, numCrossCorrs / NumCrossCorrsPerThread)
                    .AsParallel()
                    .Select(chunkIndex =>
                        {
                            var crossCorrs = new List<float>();

                            for (var i = chunkIndex * NumCrossCorrsPerThread; i < (chunkIndex + 1) * NumCrossCorrsPerThread && i < numCrossCorrs; ++i)
                            {
                                var correlation = 0.0f;
                                for (var j = 0; j < otherSamples.Length; j += Vector<float>.Count)
                                {
                                    var v1 = new Vector<float>(otherSamples[j]);
                                    var v2 = new Vector<float>(samples[i + j]);
                                    correlation += Vector.Sum(v1 * v2);
                                }

                                crossCorrs.Add(correlation);
                            }

                            return crossCorrs;
                        })
                    .ToArray();

            var crossCorrs = new List<float>();
            foreach (var chunk in crossCorrsChunks)
            {
                crossCorrs.AddRange(chunk);
            }

            return crossCorrs;
        }

        private const int NumCrossCorrsPerThread = 1000;

        private readonly float[] samples;
    }
}
