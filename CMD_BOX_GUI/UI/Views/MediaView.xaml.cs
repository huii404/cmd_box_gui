using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CMD_BOX_GUI.Services;
using Microsoft.Win32;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class MediaView : UserControl
    {
        private readonly MediaService _media = new();

        public MediaView()
        {
            InitializeComponent();
        }

        private void BtnBrowseMedia_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp Media",
                Filter = "Media Files (*.mp4;*.mkv;*.avi;*.mov;*.mp3;*.jpg;*.png)|*.mp4;*.mkv;*.avi;*.mov;*.mp3;*.jpg;*.png|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true) TxtSourceMedia.Text = dlg.FileName;
        }

        private void BtnBrowseSecret_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp bí mật cần giấu",
                Filter = "All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true) TxtSecretFile.Text = dlg.FileName;
        }

        private async void BtnCompressVideo_Click(object sender, RoutedEventArgs e)
        {
            string input = TxtSourceMedia.Text;
            if (!File.Exists(input)) return;

            string outPath = Path.Combine(Path.GetDirectoryName(input)!, $"{Path.GetFileNameWithoutExtension(input)}_compressed.mp4");
            SetRunning(true, "Đang nén Video...");
            await _media.CompressVideoAsync(input, outPath);
            SetRunning(false, "Nén Video xong!");
        }

        private async void BtnEnhanceMedia_Click(object sender, RoutedEventArgs e)
        {
            string input = TxtSourceMedia.Text;
            if (!File.Exists(input)) return;

            string outPath = Path.Combine(Path.GetDirectoryName(input)!, $"{Path.GetFileNameWithoutExtension(input)}_enhanced.mp4");
            SetRunning(true, "Đang làm nét & khử nhiễu...");
            await _media.EnhanceMediaAsync(input, outPath);
            SetRunning(false, "Làm nét xong!");
        }

        private async void BtnExtractMp3_Click(object sender, RoutedEventArgs e)
        {
            string input = TxtSourceMedia.Text;
            if (!File.Exists(input)) return;

            string outPath = Path.Combine(Path.GetDirectoryName(input)!, $"{Path.GetFileNameWithoutExtension(input)}.mp3");
            SetRunning(true, "Đang trích xuất MP3...");
            await _media.ExtractAudioMp3Async(input, outPath);
            SetRunning(false, "Trích xuất MP3 xong!");
        }

        private async void BtnSpeed15_Click(object sender, RoutedEventArgs e)
        {
            await ChangeSpeed(1.5);
        }

        private async void BtnSpeed05_Click(object sender, RoutedEventArgs e)
        {
            await ChangeSpeed(0.5);
        }

        private async System.Threading.Tasks.Task ChangeSpeed(double speed)
        {
            string input = TxtSourceMedia.Text;
            if (!File.Exists(input)) return;

            string outPath = Path.Combine(Path.GetDirectoryName(input)!, $"{Path.GetFileNameWithoutExtension(input)}_{speed}x.mp4");
            SetRunning(true, $"Đang đổi tốc độ sang {speed}x...");
            await _media.ChangeVideoSpeedAsync(input, outPath, speed);
            SetRunning(false, "Đổi tốc độ xong!");
        }

        private void BtnNormalizeNames_Click(object sender, RoutedEventArgs e)
        {
            string input = TxtSourceMedia.Text;
            string dir = File.Exists(input) ? Path.GetDirectoryName(input)! : (Directory.Exists(input) ? input : "");
            if (string.IsNullOrEmpty(dir)) return;

            _media.NormalizeFilenamesInDirectory(dir);
        }

        private async void BtnHideFile_Click(object sender, RoutedEventArgs e)
        {
            string container = TxtSourceMedia.Text;
            string secret = TxtSecretFile.Text;
            if (!File.Exists(container) || !File.Exists(secret)) return;

            string ext = Path.GetExtension(container);
            string outPath = Path.Combine(Path.GetDirectoryName(container)!, $"{Path.GetFileNameWithoutExtension(container)}_hidden{ext}");

            SetRunning(true, "Đang giấu tệp vào Media...");
            await _media.HideFileInMediaAsync(container, secret, outPath);
            SetRunning(false, $"Giấu tệp xong: {outPath}");
        }

        private async void BtnExtractSecret_Click(object sender, RoutedEventArgs e)
        {
            string container = TxtSourceMedia.Text;
            if (!File.Exists(container)) return;

            string outDir = Path.GetDirectoryName(container)!;
            SetRunning(true, "Đang trích xuất tệp ẩn...");
            await _media.ExtractHiddenFileAsync(container, outDir);
            SetRunning(false, "Trích xuất xong!");
        }

        private void SetRunning(bool running, string statusText)
        {
            PbMedia.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            TxtStatus.Visibility = Visibility.Visible;
            TxtStatus.Text = statusText;
            BtnCompressVideo.IsEnabled = !running;
            BtnEnhanceMedia.IsEnabled = !running;
            BtnExtractMp3.IsEnabled = !running;
            BtnSpeed15.IsEnabled = !running;
            BtnSpeed05.IsEnabled = !running;
            BtnNormalizeNames.IsEnabled = !running;
            BtnHideFile.IsEnabled = !running;
            BtnExtractSecret.IsEnabled = !running;
        }
    }
}
