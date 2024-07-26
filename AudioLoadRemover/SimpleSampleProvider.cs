using NAudio.Wave;

namespace AudioLoadRemover
{
    public class SimpleSampleProvider : ISampleProvider
    {
        public SimpleSampleProvider(SimpleAudioClip audioClip)
        {
            this.AudioClip = audioClip;
        }

        public SimpleAudioClip AudioClip { get; }

        public WaveFormat WaveFormat => this.AudioClip.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            count = Math.Min(this.AudioClip.Samples.Length - this.offset, count);

            var source = new ReadOnlySpan<float>(this.AudioClip.Samples, this.offset, count);
            var dest = new Span<float>(buffer, offset, count);

            source.CopyTo(dest);

            this.offset += count;

            return count;
        }

        private int offset = 0;
    }
}
