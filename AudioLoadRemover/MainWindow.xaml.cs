using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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
                Filter = "Video files|*.mp4;*.mkv;*.avi"
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
            var sampleRate = 6000;

            var audioClips = new List<AudioClip>
            {
                new AudioClip("SW_RivenLink", @"Riven\SW_RivenLink.wav", sampleRate)
            };

            AudioClipDetector.Detect(audioClips, videoPath, sampleRate);
        }
    }
}