using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;
using System.IO;

namespace AudioLoadRemover
{
    internal class AudioClipDetector
    {
        private const int NumSecondsPerChunk = 10;

        public static void Detect(List<AudioClip> clips, string videoPath, int sampleRate)
        {
            var maxClipLength = clips.Max(ac => ac.Samples.Length);

            Trace.WriteLine($"Searching for audio clips within {videoPath}");

            using var audioStream = new MemoryStream();
            using var audioMediaReader = new MediaFoundationReader(videoPath);
            using var audioMediaResampler = new MediaFoundationResampler(audioMediaReader, sampleRate);
             
            var audioSampleProvider = audioMediaResampler.ToSampleProvider();
            if (audioMediaReader.WaveFormat.Channels > 1)
            {
                audioSampleProvider = new StereoToMonoSampleProvider(audioSampleProvider);
            }

            var numSamplesPerChunk = sampleRate * NumSecondsPerChunk;

            var initialSampleBuffer = new float[maxClipLength + numSamplesPerChunk];
            var numInitialSamples = audioSampleProvider.Read(initialSampleBuffer, 0, initialSampleBuffer.Length);
            if (numInitialSamples < maxClipLength)
            {
                throw new Exception("Video is too short");
            }

            var audioBuffer = new AudioCorrelationBuffer(initialSampleBuffer);

            var numChunks = 0;
            var sampleBuffer = new float[numSamplesPerChunk];
            var crossCorrelations = new float[numSamplesPerChunk];
            var maxCorrelation = 0.0f;
            while (true)
            {
                Trace.Write(".");

                foreach (var audioClip in clips)
                {
                    audioBuffer.CrossCorrelation(audioClip.Samples, crossCorrelations);

                    for (var i = 0; i < crossCorrelations.Length; ++i)
                    {
                        var correlation = crossCorrelations[i];

                        if (correlation > maxCorrelation)
                        {
                            maxCorrelation = correlation;

                            var totalSeconds = (numChunks * NumSecondsPerChunk) + ((float)i / sampleRate);
                            var minutes = (int)(totalSeconds / 60.0f);
                            var seconds = totalSeconds - minutes * 60.0f;
                            Trace.WriteLine($"{minutes}:{seconds} New max correlation {correlation}");
                        }
                    }
                }

                var numSamples = audioSampleProvider.Read(sampleBuffer, 0, sampleBuffer.Length);
                if (numSamples < sampleBuffer.Length)
                {
                    break;
                }

                audioBuffer.Add(sampleBuffer);

                ++numChunks;
            }
        }
    }
}
