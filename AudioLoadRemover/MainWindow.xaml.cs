using System.Diagnostics;
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
            foreach (var audioPath in Directory.GetFiles("Riven", "*.wav"))
            {
                var audioClip = new AudioClip(audioPath, sampleRate);
                var clipMatches = AudioClipDetector.Detect(audioClip, video, sampleRate, 2);
                matches.AddRange(clipMatches);
            }

            var config = new List<LoadDetector.Sequence>()
            {
                //new LoadDetector.Sequence("SW_RivenLink", "SILENCE", TimeSpan.Zero, TimeSpan.Zero),
                new LoadDetector.Sequence("SW_FMD_Inside_Close", "SW_FMD_InnerShell_Open", TimeSpan.Zero, TimeSpan.Zero),
                new LoadDetector.Sequence("SW_FMD_InnerShell_Close", "SW_FMD_Inside_Open", TimeSpan.Zero, TimeSpan.Zero),
                //new LoadDetector.Sequence("SW_Maglev", "SILENCE", TimeSpan.Zero, TimeSpan.Zero),
                //new LoadDetector.Sequence("SW_Woodcart", "SILENCE", TimeSpan.Zero, TimeSpan.Zero),
            };

            var loadSegments = LoadDetector.Detect(matches, config);

            Trace.WriteLine("Detected loads:");
            foreach (var loadSegment in loadSegments)
            {
                Trace.WriteLine($"{loadSegment.Start} - {loadSegment.End}");
            }
        }
    }
}