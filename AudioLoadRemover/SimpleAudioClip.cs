using NAudio.Wave;

namespace AudioLoadRemover
{
    public class SimpleAudioClip
    {
        public SimpleAudioClip(WaveFormat waveFormat, float[] samples)
        {
            this.WaveFormat = waveFormat;
            this.Samples = samples;
        }

        public WaveFormat WaveFormat { get; }

        public float[] Samples { get; }

        public ISampleProvider ToSampleProvider()
        {
            return new SimpleSampleProvider(this);
        }
    }
}
