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
            Trace.WriteLine($"Loading audio clip {name} from {filePath}");

            this.name = Path.GetFileNameWithoutExtension(filePath);

            var samples = new List<float>();
            using (var audioStream = new MemoryStream())
            using (var audioMediaReader = new MediaFoundationReader(filePath))
            using (var audioMediaResampler = new MediaFoundationResampler(audioMediaReader, sampleRate))
            {
                var audioSampleProvider = audioMediaResampler.ToSampleProvider();
                if (audioMediaReader.WaveFormat.Channels > 1)
                {
                    audioSampleProvider = new StereoToMonoSampleProvider(audioSampleProvider);
                }

                var sampleBuffer = new float[sampleRate];
                while (true)
                {
                    var numSamples = audioSampleProvider.Read(sampleBuffer, 0, sampleBuffer.Length);

                    samples.AddRange(new ReadOnlySpan<float>(sampleBuffer, 0, numSamples));

                    if (numSamples < sampleBuffer.Length)
                    {
                        break;
                    }
                }
            }

            this.samples = samples.ToArray();
        }

        public string Name
        {
            get { return this.name; }
        }

        public ReadOnlySpan<float> Samples
        {
            get { return this.samples; }
        }

        private readonly string name;
        private readonly float[] samples;
    }
}
