using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.UI.Views;

namespace CMD_BOX_GUI
{
    public partial class MainWindow : Window
    {
        private readonly DashboardView _dashboardView = new();
        private readonly OptimizerView _optimizerView = new();
        private readonly NetworkView _networkView = new();
        private readonly UtilitiesView _utilitiesView = new();
        private readonly MediaView _mediaView = new();

        private readonly StringBuilder _logHistory = new();
        private readonly DispatcherTimer _logBatchTimer;
        private const int MaxLogLines = 500;
        private int _lineCount = 0;

        public MainWindow()
        {
            InitializeComponent();

            // Load View mặc định
            MainContentHost.Content = _dashboardView;

            // Timer batch update log chống lag giao diện
            _logBatchTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _logBatchTimer.Tick += FlushLogQueue;
            _logBatchTimer.Start();

            Logger.Info("Khởi động CMD BOX GUI hoàn tất.");
            Logger.Info(SystemCore.IsAdministrator() 
                ? "Quyền: Administrator." 
                : "Quyền: Standard User (Một số lệnh cần quyền Admin).");
        }

        private void FlushLogQueue(object? sender, EventArgs e)
        {
            var batch = Logger.DequeueAll();
            if (batch.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var entry in batch)
            {
                string line = entry.ToString();
                sb.AppendLine(line);
                _logHistory.AppendLine(line);
                _lineCount++;
            }

            // Chống tràn bộ nhớ & lag UI: Giới hạn 500 dòng
            if (_lineCount > MaxLogLines)
            {
                string currentText = TxtTerminal.Text;
                int newlineIndex = currentText.IndexOf('\n', currentText.Length / 2);
                if (newlineIndex > 0)
                {
                    TxtTerminal.Text = currentText.Substring(newlineIndex + 1);
                    _lineCount = MaxLogLines / 2;
                }
            }

            TxtTerminal.AppendText(sb.ToString());
            TxtTerminal.ScrollToEnd();
        }

        private void NavButton_Checked(object sender, RoutedEventArgs e)
        {
            if (MainContentHost == null) return;

            if (NavDashboard.IsChecked == true)
            {
                MainContentHost.Content = _dashboardView;
                _ = _dashboardView.RefreshDashboardDataAsync();
            }
            else if (NavOptimizer.IsChecked == true)
            {
                MainContentHost.Content = _optimizerView;
            }
            else if (NavNetwork.IsChecked == true)
            {
                MainContentHost.Content = _networkView;
            }
            else if (NavUtilities.IsChecked == true)
            {
                MainContentHost.Content = _utilitiesView;
            }
            else if (NavMedia.IsChecked == true)
            {
                MainContentHost.Content = _mediaView;
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            _logHistory.Clear();
            TxtTerminal.Clear();
            _lineCount = 0;
        }

        private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(_logHistory.ToString());
            Logger.Info("Đã sao chép Log!");
        }

        protected override void OnClosed(EventArgs e)
        {
            _logBatchTimer.Stop();
            base.OnClosed(e);
        }
    }
}