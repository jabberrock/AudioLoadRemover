using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace AudioLoadRemover
{
    public class AudioClip
    {
        public AudioClip(string filePath, int sampleRate, DebugOutput debugOutput, bool debugWriteAudio)
        {
            this.Name = Path.GetFileNameWithoutExtension(filePath);

            debugOutput.Log($"Loading audio clip {this.Name} from {filePath}");

            this.RawAudio = ReadMonoAudioFromFile(filePath);
            
            // Remove low frequenices that usually correspond to background noise
            // Before resampling, we have to remove higher frequencies otherwise there will be aliasing
            this.ProcessedAudio = Resample(ApplyBandPassFilter(this.RawAudio, 200.0f, sampleRate / 2), sampleRate);

            if (debugWriteAudio)
            {
                DebugWriteAudio(this.RawAudio, $"{this.Name}-raw", debugOutput);
                DebugWriteAudio(this.ProcessedAudio, $"{this.Name}-processed", debugOutput);
            }

            this.SilentPrefix = 0;
            for (var i = 0; i < this.RawAudio.Samples.Length; ++i)
            {
                if (Math.Abs(this.RawAudio.Samples[i]) > MaxSilenceLevel)
                {
                    break;
                }

                ++this.SilentPrefix;
            }

            this.SilentSuffix = 0;
            for (var i = this.RawAudio.Samples.Length - 1; i >= 0; --i)
            {
                if (Math.Abs(this.RawAudio.Samples[i]) > MaxSilenceLevel)
                {
                    break;
                }

                ++this.SilentSuffix;
            }
        }

        public string Name { get; }

        public SimpleAudioClip RawAudio { get; }

        public SimpleAudioClip ProcessedAudio { get; }

        public int SilentPrefix { get; }

        public int SilentSuffix { get; }

        private static SimpleAudioClip ReadMonoAudioFromFile(string filePath)
        {
            using var waveReader = new MediaFoundationReader(filePath);

            var sampleProvider = waveReader.ToSampleProvider();
            if (waveReader.WaveFormat.Channels == 2)
            {
                sampleProvider = new StereoToMonoSampleProvider(waveReader.ToSampleProvider());
            }
            else
            {
                throw new Exception("Audio clip has more than 2 channels");
            }

            return ReadAll(sampleProvider);
        }

        private static SimpleAudioClip ApplyBandPassFilter(SimpleAudioClip rawAudio, float lowerCutoffFreq, float upperCutoffFreq)
        {
            var lowPassFilter = BiQuadFilter.LowPassFilter(rawAudio.WaveFormat.SampleRate, upperCutoffFreq, BandPassQFactor);
            var highPassFilter = BiQuadFilter.HighPassFilter(rawAudio.WaveFormat.SampleRate, lowerCutoffFreq, BandPassQFactor);

            var rawSamples = rawAudio.Samples;
            var filteredSamples = new float[rawAudio.Samples.Length];
            for (var i = 0; i < filteredSamples.Length; ++i)
            {
                filteredSamples[i] = highPassFilter.Transform(lowPassFilter.Transform(rawSamples[i]));
            }

            return new SimpleAudioClip(rawAudio.WaveFormat, filteredSamples);
        }

        private static SimpleAudioClip Resample(SimpleAudioClip rawAudio, int newSampleRate)
        {
            var resampler = new WdlResamplingSampleProvider(rawAudio.ToSampleProvider(), newSampleRate);
            return ReadAll(resampler);
        }

        private static SimpleAudioClip ReadAll(ISampleProvider sampleProvider)
        {
            var samples = new List<float>();
            var sampleBuffer = new float[SampleBufferLength];
            while (true)
            {
                var numSamplesRead = sampleProvider.Read(sampleBuffer, 0, sampleBuffer.Length);
                if (numSamplesRead == 0)
                {
                    break;
                }

                samples.AddRange(new ReadOnlySpan<float>(sampleBuffer, 0, numSamplesRead));
            }

            return new SimpleAudioClip(sampleProvider.WaveFormat, samples.ToArray());
        }

        private static void DebugWriteAudio(SimpleAudioClip audio, string fileName, DebugOutput debugOutput)
        {
            using var waveFileWriter = new WaveFileWriter(Path.Combine(debugOutput.Folder, $"audio-{fileName}.wav"), audio.WaveFormat);
                
            var silenceSamples = new float[DebugAddSilenceSec * audio.WaveFormat.SampleRate * audio.WaveFormat.Channels];
            waveFileWriter.WriteSamples(silenceSamples, 0, silenceSamples.Length);

            waveFileWriter.WriteSamples(audio.Samples, 0, audio.Samples.Length);

            waveFileWriter.WriteSamples(silenceSamples, 0, silenceSamples.Length);
        }

        private const int SampleBufferLength = 1024 * 1024;
        private const float MaxSilenceLevel = 0.001f;
        private const float BandPassQFactor = 2.0f;
        private const int DebugAddSilenceSec = 5;
    }
}
