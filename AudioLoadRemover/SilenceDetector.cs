namespace AudioLoadRemover
{
    internal class SilenceDetector
    {
        public static List<AudioClipDetector.Match> Detect(AudioClip clip, TimeSpan minSilenceTime, float silenceLevel)
        {
            var silences = new List<AudioClipDetector.Match>();


            var samples = clip.RawSamples;
            var numChannels = clip.WaveFormat.Channels;
            var sampleRate = clip.WaveFormat.SampleRate;

            TimeSpan? startTime = null;
            for (var i = 0; i < samples.Length / numChannels; ++i)
            {
                var isSilence = true;
                for (var j = 0; j < numChannels; ++j)
                {
                    if (Math.Abs(samples[i * numChannels + j]) > silenceLevel)
                    {
                        isSilence = false;
                        break;
                    }
                }

                if (isSilence)
                {
                    if (!startTime.HasValue)
                    {
                        startTime = TimeSpan.FromSeconds((float)i / sampleRate);
                    }
                }
                else
                {
                    if (startTime.HasValue)
                    {
                        var endTime = TimeSpan.FromSeconds((float)i / sampleRate);
                        if (endTime - startTime > minSilenceTime)
                        {
                            silences.Add(
                                new AudioClipDetector.Match("SILENCE", clip.Name, startTime.Value, endTime, sampleRate, 0.0f));
                        }

                        startTime = null;
                    }
                }
            }

            return silences;
        }
    }
}
