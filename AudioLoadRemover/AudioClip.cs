using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;
using System.IO;

namespace AudioLoadRemover
{
    internal class AudioClip
    {
        public AudioClip(string filePath, int sampleRate)
        {
            this.name = Path.GetFileNameWithoutExtension(filePath);

            Trace.WriteLine($"Loading audio clip {this.name} from {filePath}");

            var samples = new List<float>();

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

            var sampleBuffer = new float[sampleRate * NumSecondsToReadPerChunk];
            while (true)
            {
                var numSamples = audioSampleProvider.Read(sampleBuffer, 0, sampleBuffer.Length);

                samples.AddRange(new ReadOnlySpan<float>(sampleBuffer, 0, numSamples));

                if (numSamples < sampleBuffer.Length)
                {
                    break;
                }
            }

            this.samples = samples.ToArray();

            this.silentPrefix = 0;
            for (var i = 0; i < this.samples.Length; ++i)
            {
                if (Math.Abs(this.samples[i]) > MaxSilenceLevel)
                {
                    break;
                }

                ++this.silentPrefix;
            }

            this.silentSuffix = 0;
            for (var i = this.samples.Length - 1; i >= 0; --i)
            {
                if (Math.Abs(this.samples[i]) > MaxSilenceLevel)
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
