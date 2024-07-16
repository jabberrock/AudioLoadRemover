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
            if (audioMediaReader.WaveFormat.Channels == 1)
            {
                audioSampleProvider = new MonoToStereoSampleProvider(audioSampleProvider);
            }
            else if (audioMediaReader.WaveFormat.Channels != 2)
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

        private const int NumSecondsToReadPerChunk = 60;

        private readonly string name;
        private readonly WaveFormat waveFormat;
        private readonly float[] samples;
    }
}
