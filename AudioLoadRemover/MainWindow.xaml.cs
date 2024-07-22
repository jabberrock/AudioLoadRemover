using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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

            this.LoadSegmentsListView.ItemsSource = this.LoadSegments;
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

                this.Title = this.Title + " - " + Path.GetFileNameWithoutExtension(videoPath);

                new Thread(() => ProcessVideo(videoPath)).Start();
            }
        }
        private void MediaTimeline_CurrentTimeInvalidated(object? sender, EventArgs e)
        {
            var currentTime = this.VideoPlayer.Clock.CurrentTime;
            if (currentTime.HasValue)
            {
                // Prevent a loop where we update the slider, which causes the media to seek, causing stuttering
                this.seekWhenSliderValueChanged = false;
                this.VideoPlayerSlider.Value = currentTime.Value.TotalMilliseconds;
                this.seekWhenSliderValueChanged = true;

                var isLoading = false;
                foreach (var loadSegment in this.LoadSegments)
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
            if (this.seekWhenSliderValueChanged)
            {
                this.VideoPlayer.Clock.Controller.Seek(TimeSpan.FromMilliseconds(e.NewValue), TimeSeekOrigin.BeginTime);
            }
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

        private void LoadSegmentsListView_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var selectedItem = this.LoadSegmentsListView.SelectedItem as LoadDetector.Segment;
            if (selectedItem != null)
            {
                var startTime = selectedItem.Start - TimeSpan.FromSeconds(2.0);
                if (startTime < TimeSpan.Zero)
                {
                    startTime = TimeSpan.Zero;
                }

                this.VideoPlayer.Clock.Controller.Seek(startTime, TimeSeekOrigin.BeginTime);
            }
        }

        private void StartManualLoadButton_Click(object sender, RoutedEventArgs e)
        {
            this.manualStartTime = this.VideoPlayer.Clock.CurrentTime;
        }

        private void EndManualLoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.manualStartTime.HasValue)
            {
                var currentTime = this.VideoPlayer.Clock.CurrentTime;
                if (currentTime.HasValue && currentTime.Value > manualStartTime.Value)
                {
                    var newSegment = new LoadDetector.Segment(this.manualStartTime.Value, currentTime.Value, ManualSequenceName);

                    var overlaps = this.LoadSegments.Any(s => s.Overlaps(newSegment));
                    if (!overlaps)
                    {
                        var added = false;
                        for (var i = 0; i < this.LoadSegments.Count; ++i)
                        {
                            if (this.LoadSegments[i].Start > newSegment.Start)
                            {
                                this.LoadSegments.Insert(i, newSegment);
                                added = true;
                                break;
                            }
                        }

                        if (!added)
                        {
                            this.LoadSegments.Add(newSegment);
                        }

                        this.UpdateTotalLoadTime();
                    }
                }
            }
        }

        private void DeleteLoadSegmentButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = this.LoadSegmentsListView.SelectedItem as LoadDetector.Segment;
            if (selectedItem != null)
            {
                this.LoadSegments.Remove(selectedItem);
                this.UpdateTotalLoadTime();
            }
        }

        private static TimeSpan OneFrameTime()
        {
            // Extract from video?
            return TimeSpan.FromMilliseconds(1000.0 / 60.0);
        }

        private void ProcessVideo(string videoPath)
        {
            var sampleRate = 3000;

            var matches = new List<AudioClipDetector.Match>();

            var video = new AudioClip(videoPath, sampleRate);

            // Detect silences
            // TODO: Calculate median loudness of video and normalize it
            var silenceMatches = SilenceDetector.Detect(video, TimeSpan.FromSeconds(0.2), sampleRate, 1, 0.0001f);

            Trace.WriteLine("Silence events:");
            foreach (var match in silenceMatches)
            {
                Trace.WriteLine($"{match.StartTime}-{match.EndTime}");
            }

            matches.AddRange(silenceMatches);

            // Detect clips
            foreach (var audioPath in Directory.GetFiles(@"Riven\Clips", "*.wav"))
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
                    "SW_RivenLink",
                    "SILENCE",
                    new LoadDetector.Offset(LoadDetector.Anchor.Start, TimeSpan.Zero),
                    new LoadDetector.Offset(LoadDetector.Anchor.End, TimeSpan.Zero)),

                new LoadDetector.Sequence(
                    "Dome Entry",
                    "SW_FMD_Inside_Close",
                    "SW_FMD_InnerShell_Open",
                    // Outside dome goes "clunk" as it slams shut
                    new LoadDetector.Offset(LoadDetector.Anchor.Start, TimeSpan.FromSeconds(6.446)),
                    // Inside door starts to open (gas hissing)
                    new LoadDetector.Offset(LoadDetector.Anchor.Start, TimeSpan.Zero)),

                new LoadDetector.Sequence(
                    "Dome Exit",
                    "SW_FMD_InnerShell_Close",
                    "SW_FMD_Inside_Open",
                    // Inside door starts to close (chains)
                    new LoadDetector.Offset(LoadDetector.Anchor.Start, TimeSpan.FromSeconds(0.721)),
                    // Outside dome goes "clunk" as it opens fully
                    new LoadDetector.Offset(LoadDetector.Anchor.Start, TimeSpan.FromSeconds(6.565))),

                new LoadDetector.Sequence(
                    "Maglev",
                    "SW_Maglev_TPLDeparture",
                    "SILENCE",
                    new LoadDetector.Offset(LoadDetector.Anchor.Start, TimeSpan.Zero),
                    new LoadDetector.Offset(LoadDetector.Anchor.End, TimeSpan.Zero)),

                new LoadDetector.Sequence(
                    "Woodcart",
                    "SW_Woodcart_JNGToBLR_Start",
                    "SILENCE",
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

            Dispatcher.BeginInvoke(new Action(() =>
            {
                this.SetLoadSegments(loadSegments);
                this.UpdateTotalLoadTime();
            }));
        }

        private void SetLoadSegments(List<LoadDetector.Segment> loadSegments)
        {
            this.LoadSegments.Clear();

            var totalTime = TimeSpan.Zero;
            foreach (var loadSegment in loadSegments)
            {
                this.LoadSegments.Add(loadSegment);
                totalTime += loadSegment.Duration;
            }

        }

        private void UpdateTotalLoadTime()
        {
            var hasManual = false;
            var totalTime = TimeSpan.Zero;
            foreach (var loadSegment in this.LoadSegments)
            {
                totalTime += loadSegment.Duration;
                if (loadSegment.SequenceName == ManualSequenceName)
                {
                    hasManual = true;
                }
            }

            this.TotalLoadSegmentTimeText.Text = $"Total Load Time: {(hasManual ? ManualTimeMarker : "")}{TimeSpanFormatter.ToShortString(totalTime)}";
        }

        private const string ManualSequenceName = "**** MANUAL ****";
        private const string ManualTimeMarker = "****";

        public ObservableCollection<LoadDetector.Segment> LoadSegments { get; } = new ObservableCollection<LoadDetector.Segment>();

        private bool seekWhenSliderValueChanged = true;
        private TimeSpan? manualStartTime = null;
    }
}