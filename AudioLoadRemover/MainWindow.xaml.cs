using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace AudioLoadRemover
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.OpenVideoButton.Visibility = Visibility.Visible;
            this.VideoLoadedGrid.Visibility = Visibility.Hidden;
        }

        private void OpenVideo_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Audio and Video files|*.wav;*.mp3;*.mp4;*.mkv;*.avi"
            };

            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                var videoPath = dialog.FileName;

                this.OpenVideoButton.Visibility = Visibility.Hidden;

                var mediaTimeline = new MediaTimeline(new Uri(videoPath));
                mediaTimeline.CurrentTimeInvalidated += MediaTimeline_CurrentTimeInvalidated;
                this.VideoPlayer.Clock = mediaTimeline.CreateClock();
                this.VideoPlayer.Clock.Controller.Pause();
                this.VideoPlayer.ScrubbingEnabled = true;

                this.VideoLoadedGrid.Visibility = Visibility.Visible;

                new Thread(() => ProcessVideo(videoPath)).Start();
            }
        }
        private void MediaTimeline_CurrentTimeInvalidated(object? sender, EventArgs e)
        {
            var currentTime = this.VideoPlayer.Clock.CurrentTime;
            if (currentTime.HasValue)
            {
                this.VideoPlayerSlider.Value = currentTime.Value.TotalMilliseconds;

                var isLoading = false;
                foreach (var loadSegment in this.loadSegments)
                {
                    if (currentTime >= loadSegment.Start && currentTime < loadSegment.End)
                    {
                        isLoading = true;
                        break;
                    }
                }
                
                this.LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            this.VideoPlayerSlider.Maximum = this.VideoPlayer.NaturalDuration.TimeSpan.TotalMilliseconds;
        }

        private void VideoPlayerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.VideoPlayer.Clock.Controller.Seek(TimeSpan.FromMilliseconds(e.NewValue), TimeSeekOrigin.BeginTime);
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            this.VideoPlayer.Clock.Controller.Resume();
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            this.VideoPlayer.Clock.Controller.Pause();
        }
        private void PreviousFrameButton_Click(object sender, RoutedEventArgs e)
        {
            var currentTime = this.VideoPlayer.Clock.CurrentTime;
            if (currentTime.HasValue)
            {
                var newTime = currentTime.Value - OneFrameTime();
                this.VideoPlayer.Clock.Controller.Seek(newTime, TimeSeekOrigin.BeginTime);
            }
        }
        private void NextFrameButton_Click(object sender, RoutedEventArgs e)
        {
            var currentTime = this.VideoPlayer.Clock.CurrentTime;
            if (currentTime.HasValue)
            {
                var newTime = currentTime.Value + OneFrameTime();
                this.VideoPlayer.Clock.Controller.Seek(newTime, TimeSeekOrigin.BeginTime);
            }
        }
        private static TimeSpan OneFrameTime()
        {
            // Extract from video?
            return TimeSpan.FromMilliseconds(1000.0 / 60.0);
        }

        private void ProcessVideo(string videoPath)
        {
            var sampleRate = 6000;

            var matches = new List<AudioClipDetector.Match>();

            var video = new AudioClip(videoPath, sampleRate);

            // Detect silences
            var silenceMatches = SilenceDetector.Detect(video, TimeSpan.FromSeconds(0.5), sampleRate, 1, 0.001f);

            Trace.WriteLine("Silence events:");
            foreach (var match in silenceMatches)
            {
                Trace.WriteLine($"{match.StartTime}-{match.EndTime}");
            }

            matches.AddRange(silenceMatches);

            // Detect clips
            foreach (var audioPath in Directory.GetFiles("Riven", "*.wav"))
            {
                var audioClip = new AudioClip(audioPath, sampleRate);

                var clipMatches = AudioClipDetector.Detect(audioClip, video, sampleRate, 1);

                Trace.WriteLine($"{audioClip.Name} events:");
                foreach (var match in clipMatches)
                {
                    Trace.WriteLine($"{match.StartTime}-{match.EndTime} correlation {match.Correlation}");
                }

                matches.AddRange(clipMatches);
            }

            var orderedMatches = matches.OrderBy(m => m.StartTime).ToList();

            Trace.WriteLine("Ordered events:");
            foreach (var match in orderedMatches)
            {
                Trace.WriteLine($"{match.StartTime}-{match.EndTime} detected {match.QueryName}");
            }

            // Detect load segments
            var config = new List<LoadDetector.Sequence>()
            {
                new LoadDetector.Sequence(
                    "Linking",
                    "Linking",
                    "SILENCE",
                    // Loading... screen shows up at different times after the linking sound plays
                    new LoadDetector.Offset(LoadDetector.Anchor.Start, TimeSpan.Zero),
                    new LoadDetector.Offset(LoadDetector.Anchor.End, TimeSpan.Zero)),

                new LoadDetector.Sequence(
                    "Dome Entry",
                    "EnterDome",
                    "EnterInnerDome",
                    new LoadDetector.Offset(LoadDetector.Anchor.End, TimeSpan.Zero),
                    new LoadDetector.Offset(LoadDetector.Anchor.Start, TimeSpan.Zero)),

                new LoadDetector.Sequence(
                    "Dome Exit",
                    "ExitInnerDome",
                    "ExitDome",
                    new LoadDetector.Offset(LoadDetector.Anchor.End, TimeSpan.Zero),
                    new LoadDetector.Offset(LoadDetector.Anchor.Start, TimeSpan.Zero)),

                new LoadDetector.Sequence(
                    "Maglev",
                    "Maglev",
                    "SILENCE",
                    // Fade out hapens at different times after the Maglev takes off
                    new LoadDetector.Offset(LoadDetector.Anchor.Start, TimeSpan.Zero),
                    new LoadDetector.Offset(LoadDetector.Anchor.End, TimeSpan.Zero)),

                new LoadDetector.Sequence(
                    "Woodcart",
                    "Woodcart",
                    "SILENCE",
                    // Fade out happens at different times after the Woodcart takes off
                    new LoadDetector.Offset(LoadDetector.Anchor.Start, TimeSpan.Zero),
                    new LoadDetector.Offset(LoadDetector.Anchor.End, TimeSpan.Zero)),
            };

            var loadSegments = LoadDetector.Detect(orderedMatches, config);

            // Remove initial linking segments which are not part of the timing
            while (loadSegments.Count > 0 && loadSegments[0].SequenceName == "Linking")
            {
                loadSegments.RemoveAt(0);
            }

            Trace.WriteLine("Detected loads:");
            foreach (var loadSegment in loadSegments)
            {
                Trace.WriteLine($"{loadSegment.Start}-{loadSegment.End} due to {loadSegment.SequenceName}");
            }

            var totalLoadTime = TimeSpan.Zero;
            foreach (var loadSegment in loadSegments)
            {
                totalLoadTime += (loadSegment.End - loadSegment.Start);
            }

            Trace.WriteLine($"Total load time removed: {totalLoadTime}");

            Dispatcher.BeginInvoke(new Action(() => this.AddLoadSegments(loadSegments)));
        }

        private void AddLoadSegments(List<LoadDetector.Segment> loadSegments)
        {
            this.loadSegments.AddRange(loadSegments);
        }

        private List<LoadDetector.Segment> loadSegments = new List<LoadDetector.Segment>();
    }
}