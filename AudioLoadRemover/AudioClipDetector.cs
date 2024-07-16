using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;
using System.IO;

namespace AudioLoadRemover
{
    internal class AudioClipDetector
    {
        private const int NumSecondsPerChunk = 10;

        public static void Detect(AudioClip clip, string videoPath, int sampleRate)
        {
            Trace.WriteLine($"Searching for audio clip {clip.Name} within {videoPath}");

            using var audioStream = new MemoryStream();
            using var audioMediaReader = new MediaFoundationReader(videoPath);
            using var audioMediaResampler = new MediaFoundationResampler(audioMediaReader, sampleRate);
             
            var audioSampleProvider = audioMediaResampler.ToSampleProvider();
            if (audioMediaReader.WaveFormat.Channels > 1)
            {
                audioSampleProvider = new StereoToMonoSampleProvider(audioSampleProvider);
            }

            var numSamplesPerChunk = sampleRate * NumSecondsPerChunk;

            var initialSampleBuffer = new float[clip.Samples.Length + numSamplesPerChunk];
            var numInitialSamples = audioSampleProvider.Read(initialSampleBuffer, 0, initialSampleBuffer.Length);
            if (numInitialSamples < initialSampleBuffer.Length)
            {
                throw new Exception("Video is too short");
            }

            var audioBuffer = new AudioCorrelationBuffer(initialSampleBuffer);
            var sampleBuffer = new float[numSamplesPerChunk];
            var crossCorrelations = new float[numSamplesPerChunk];
            var maxCorrelationTracker = new MaxValueTracker(clip.Samples.Length);
            while (true)
            {
                Trace.Write(".");

                audioBuffer.CrossCorrelation(clip.Samples, crossCorrelations);

                for (var i = 0; i < crossCorrelations.Length; ++i)
                {
                    maxCorrelationTracker.Add(crossCorrelations[i]);
                }

                var numSamples = audioSampleProvider.Read(sampleBuffer, 0, sampleBuffer.Length);
                if (numSamples < sampleBuffer.Length)
                {
                    break;
                }

                audioBuffer.Add(sampleBuffer);
            }

            Trace.WriteLine(clip.Name);

            maxCorrelationTracker.SuppressNoise();
            foreach (var maxEntry in maxCorrelationTracker.MaxEntries)
            {
                var totalSeconds = (float)maxEntry.Index / sampleRate;
                var minutes = (int)(totalSeconds / 60.0f);
                var seconds = totalSeconds - minutes * 60.0f;
                Trace.WriteLine($"{minutes}:{seconds} {maxEntry.Value}");
            }
        }
    }
}
