﻿using NAudio.Wave;
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
            var sampleRate = 3000;

            var audioClips = new List<AudioClip>();
            foreach (var audioPath in Directory.GetFiles("Riven", "*.wav"))
            {
                audioClips.Add(new AudioClip(audioPath, sampleRate));
            }

            Parallel.ForEach(audioClips, audioClip => AudioClipDetector.Detect(audioClip, videoPath, sampleRate));
        }
    }
}