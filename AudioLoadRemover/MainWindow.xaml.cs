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

            var video = new AudioClip(videoPath, sampleRate);

            foreach (var audioPath in Directory.GetFiles("Riven", "*.wav"))
            {
                var audioClip = new AudioClip(audioPath, sampleRate);
                AudioClipDetector.Detect(audioClip, video, sampleRate, 2);
            }
        }
    }
}