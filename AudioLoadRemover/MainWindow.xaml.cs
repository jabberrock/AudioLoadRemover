using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.Windows;
using System.Windows.Forms;

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

                this.VideoElement.Source = new Uri(videoPath);
                this.VideoElement.Visibility = Visibility.Visible;

                new Thread(() => ProcessVideo(videoPath)).Start();
            }
        }

        private static void ProcessVideo(string videoPath)
        {
            var sampleRate = 3000;

            var matches = new List<AudioClipDetector.Match>();

            var video = new AudioClip(videoPath, sampleRate);

            // Detect silences
            var silenceMatches = SilenceDetector.Detect(video, TimeSpan.FromSeconds(0.5), sampleRate, 0.001f);

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
                var clipMatches = AudioClipDetector.Detect(audioClip, video, sampleRate, 2);

                Trace.WriteLine($"{audioClip.Name} events:");
                foreach (var match in clipMatches)
                {
                    Trace.WriteLine($"{match.StartTime}-{match.EndTime}");
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
                new LoadDetector.Sequence("Linking", "SW_RivenLink", "SILENCE", TimeSpan.Zero, TimeSpan.Zero),
                new LoadDetector.Sequence("Dome Entry", "SW_FMD_Inside_Close", "SW_FMD_InnerShell_Open", TimeSpan.Zero, TimeSpan.Zero),
                new LoadDetector.Sequence("Dome Exit", "SW_FMD_InnerShell_Close", "SW_FMD_Inside_Open", TimeSpan.Zero, TimeSpan.Zero),
                new LoadDetector.Sequence("Maglev", "SW_Maglev", "SILENCE", TimeSpan.Zero, TimeSpan.Zero),
                new LoadDetector.Sequence("Woodcart", "SW_Woodcart", "SILENCE", TimeSpan.Zero, TimeSpan.Zero),
            };

            var loadSegments = LoadDetector.Detect(orderedMatches, config);

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
        }
    }
}