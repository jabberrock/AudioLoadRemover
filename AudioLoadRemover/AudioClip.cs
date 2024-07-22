using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;
using System.IO;

namespace AudioLoadRemover
{
    public class AudioClip
    {
        public AudioClip(string filePath, int sampleRate)
        {
            this.name = Path.GetFileNameWithoutExtension(filePath);

            Trace.WriteLine($"Loading audio clip {this.name} from {filePath}");

            var samples = new List<float>();
            var originalSamples = new List<float>();

            using var audioStream = new MemoryStream();
            using var audioMediaReader = new MediaFoundationReader(filePath);
            using var audioMediaResampler = new MediaFoundationResampler(audioMediaReader, sampleRate);

            this.waveFormat = audioMediaReader.WaveFormat;

            var audioSampleProvider = audioMediaResampler.ToSampleProvider();
            if (audioMediaReader.WaveFormat.Channels == 2)
            {
                audioSampleProvider = new StereoToMonoSampleProvider(audioSampleProvider);
            }
            else if (audioMediaReader.WaveFormat.Channels > 2)
            {
                throw new Exception("Audio clip has more than 2 channels");
            }

            // The audio clip that we search for usually has high frequency sounds, whereas
            // the background sounds are often low frequency. Apply a high pass filter to
            // remove the background sounds.
            var highBandFilter = BiQuadFilter.HighPassFilter(sampleRate, 200.0f, 2.0f);

            var sampleBuffer = new float[sampleRate * NumSecondsToReadPerChunk];
            while (true)
            {
                var numSamples = audioSampleProvider.Read(sampleBuffer, 0, sampleBuffer.Length);

                originalSamples.AddRange(new ReadOnlySpan<float>(sampleBuffer, 0, numSamples));

                for (var i = 0; i < numSamples; ++i)
                {
                    sampleBuffer[i] = highBandFilter.Transform(sampleBuffer[i]);
                }

                samples.AddRange(new ReadOnlySpan<float>(sampleBuffer, 0, numSamples));

                if (numSamples < sampleBuffer.Length)
                {
                    break;
                }
            }

            this.samples = samples.ToArray();

            // Detect silences using the original samples (without the high band filter), otherwise
            // a lot of background sounds are filtered out
            this.silentPrefix = 0;
            for (var i = 0; i < originalSamples.Count; ++i)
            {
                if (Math.Abs(originalSamples[i]) > MaxSilenceLevel)
                {
                    break;
                }

                ++this.silentPrefix;
            }

            this.silentSuffix = 0;
            for (var i = originalSamples.Count - 1; i >= 0; --i)
            {
                if (Math.Abs(originalSamples[i]) > MaxSilenceLevel)
                {
                    break;
                }

                ++this.silentSuffix;
            }
        }

        public string Name
        {
            get { return this.name; }
        }

        public WaveFormat WaveFormat
        {
            get { return this.waveFormat; }
        }

        public float[] Samples
        {
            get { return this.samples; }
        }

        public int Duration
        {
            get { return this.samples.Length; }
        }

        public int SilentPrefix
        {
            get { return this.silentPrefix; }
        }

        public int SilentSuffix
        {
            get { return this.silentSuffix; }
        }

        private const int NumSecondsToReadPerChunk = 60;
        private const float MaxSilenceLevel = 0.001f;

        private readonly string name;
        private readonly WaveFormat waveFormat;
        private readonly float[] samples;
        private readonly int silentPrefix;
        private readonly int silentSuffix;
    }
}
