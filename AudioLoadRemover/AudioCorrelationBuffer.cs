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
        public void CrossCorrelation(ReadOnlySpan<float> otherSamples, float[] correlations)
        {
            if (otherSamples.Length + correlations.Length > this.samples.Length)
            {
                throw new Exception("otherSamples and correlations does not fit in buffer");
            }

            var samples = this.samples;
            for (var i = 0; i < correlations.Length; ++i)
            {
                var correlation = 0.0f;
                for (var j = 0; j < otherSamples.Length; j += Vector<float>.Count)
                {
                    var v1 = new Vector<float>(otherSamples[j]);
                    var v2 = new Vector<float>(samples[i + j]);
                    correlation += Vector.Sum(v1 * v2);
                }

                correlations[i] = correlation;
            }
        }

        private readonly float[] samples;
    }
}
