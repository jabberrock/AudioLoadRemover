using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace AudioLoadRemover
{
    public class AudioClip
    {
        public AudioClip(string filePath, int sampleRate, DebugOutput debugOutput)
        {
            this.name = Path.GetFileNameWithoutExtension(filePath);

            debugOutput.Log($"Loading audio clip {this.name} from {filePath}");

            var highPassFilteredSamples = new List<float>();
            var rawSamples = new List<float>();

            using var audioMediaReader = new MediaFoundationReader(filePath);
            using var audioMediaResampler = new MediaFoundationResampler(audioMediaReader, sampleRate);

            var audioSampleProvider = audioMediaResampler.ToSampleProvider();
            if (audioMediaReader.WaveFormat.Channels == 2)
            {
                audioSampleProvider = new StereoToMonoSampleProvider(audioSampleProvider);
            }
            else if (audioMediaReader.WaveFormat.Channels > 2)
            {
                throw new Exception("Audio clip has more than 2 channels");
            }

            this.waveFormat = WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, sampleRate, 1, 2 * sampleRate * 1, 4, 16);

            // The audio clip that we search for usually has high frequency sounds, whereas
            // the background sounds are often low frequency. Apply a high pass filter to
            // remove the background sounds.
            var highBandFilter = BiQuadFilter.HighPassFilter(sampleRate, 200.0f, 2.0f);

            var sampleBuffer = new float[sampleRate * NumSecondsToReadPerChunk];
            while (true)
            {
                var numSamples = audioSampleProvider.Read(sampleBuffer, 0, sampleBuffer.Length);

                rawSamples.AddRange(new ReadOnlySpan<float>(sampleBuffer, 0, numSamples));

                for (var i = 0; i < numSamples; ++i)
                {
                    sampleBuffer[i] = highBandFilter.Transform(sampleBuffer[i]);
                }

                highPassFilteredSamples.AddRange(new ReadOnlySpan<float>(sampleBuffer, 0, numSamples));

                if (numSamples < sampleBuffer.Length)
                {
                    break;
                }
            }

            this.rawSamples = rawSamples.ToArray();
            this.highPassFilteredSamples = highPassFilteredSamples.ToArray();

            // Detect silences using the original samples (without the high band filter), otherwise
            // a lot of background sounds are filtered out
            this.silentPrefix = 0;
            for (var i = 0; i < rawSamples.Count; ++i)
            {
                if (Math.Abs(rawSamples[i]) > MaxSilenceLevel)
                {
                    break;
                }

                ++this.silentPrefix;
            }

            this.silentSuffix = 0;
            for (var i = rawSamples.Count - 1; i >= 0; --i)
            {
                if (Math.Abs(rawSamples[i]) > MaxSilenceLevel)
                {
                    break;
                }

                ++this.silentSuffix;
            }

            using (var waveFileWriter = new WaveFileWriter(Path.Combine(debugOutput.Folder, $"audio-{this.name}.wav"), this.waveFormat))
            {
                var silenceSamples = new float[NumSecPrefixAndSuffix * sampleRate];
                waveFileWriter.WriteSamples(silenceSamples, 0, silenceSamples.Length);

                waveFileWriter.WriteSamples(this.highPassFilteredSamples, 0, this.highPassFilteredSamples.Length);

                waveFileWriter.WriteSamples(silenceSamples, 0, silenceSamples.Length);
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

        public float[] RawSamples
        {
            get { return this.rawSamples; }
        }

        public float[] HighPassFilteredSamples
        {
            get { return this.highPassFilteredSamples; }
        }

        public int Duration
        {
            get { return this.highPassFilteredSamples.Length; }
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
        private const int NumSecPrefixAndSuffix = 5;

        private readonly string name;
        private readonly WaveFormat waveFormat;
        private readonly float[] rawSamples;
        private readonly float[] highPassFilteredSamples;
        private readonly int silentPrefix;
        private readonly int silentSuffix;
    }
}
