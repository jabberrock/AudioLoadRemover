using Microsoft.Win32;
using System.Windows;

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
            var dialog = new OpenFileDialog();
            dialog.Filter = "Video files|*.mp4;*.mkv;*.avi";

            var result = dialog.ShowDialog();
            if (result ?? false)
            {
                var videoPath = dialog.FileName;

                this.OpenVideoButton.Visibility = Visibility.Hidden;

                this.VideoElement.Source = new Uri(videoPath);
                this.VideoElement.Visibility = Visibility.Visible;

                this.ProcessVideo(videoPath);
            }
        }

        private void ProcessVideo(string videoPath)
        {
        }
    }
}