using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Models;
using CMD_BOX_GUI.Services;
using CMD_BOX_GUI.UI.Views;
using Microsoft.Win32;

namespace CMD_BOX_GUI
{
    public partial class MainWindow : Window
    {
        private readonly OptimizerView _optimizerView = new();
        private readonly NetworkView _networkView = new();
        private readonly UtilitiesView _utilitiesView = new();
        private readonly MediaView _mediaView = new();
        private readonly GuideView _guideView = new();

        private readonly ObservableCollection<WorkspaceTabItem> _openTabs = new();
        private string _currentModule = "Optimizer";
        private WorkspaceTabItem? _activeTab;
        private int _scratchpadCounter = 1;

        private bool _isSidebarOpen = true;
        private bool _isTerminalVisible = true;
        private bool _isTerminalExpanded = false;

        private readonly StringBuilder _logHistory = new();
        private readonly DispatcherTimer _logBatchTimer;
        private const int MaxLogLines = 500;
        private int _lineCount = 0;

        public MainWindow()
        {
            InitializeComponent();

            TabStripItems.ItemsSource = _openTabs;

            UpdateThemeUIState();
            UpdateAdminCmdUIState();

            // Hiển thị module chính mặc định (System Optimizer) trực tiếp, không tạo thẻ tab
            SwitchToModule("Optimizer");

            // Timer batch update log chống lag giao diện
            _logBatchTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _logBatchTimer.Tick += FlushLogQueue;
            _logBatchTimer.Start();

            bool isAdmin = SystemCore.IsAdministrator();
            Logger.Info(isAdmin
                ? "CMD BOX GUI khởi động (Admin)."
                : "CMD BOX GUI khởi động (Standard User).");
        }

        // ================= ĐIỀU HƯỚNG MODULE CHÍNH & TAB FILE NHÁP =================

        private void SwitchToModule(string moduleId)
        {
            _currentModule = moduleId;
            _activeTab = null;

            // Bỏ chọn tất cả các tab file nếu có
            foreach (var t in _openTabs) t.IsSelected = false;

            // Chuyển view trực tiếp vào MainContentHost
            MainContentHost.Content = moduleId switch
            {
                "Network" => _networkView,
                "Utilities" => _utilitiesView,
                "Media" => _mediaView,
                "Guide" => _guideView,
                _ => _optimizerView
            };

            MainContentHost.Visibility = Visibility.Visible;
            PnlEmptyWorkspace.Visibility = Visibility.Collapsed;

            // Đồng bộ trạng thái RadioButton trên Sidebar
            SyncSidebarSelection(moduleId);
            UpdateTabVisibility();
        }

        private void OpenOrActivateModuleTab(string moduleId)
        {
            SwitchToModule(moduleId);
        }

        public void OpenFileInTab(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

            string fullPath = Path.GetFullPath(filePath);
            string tabId = $"file::{fullPath}";

            var existing = _openTabs.FirstOrDefault(t => t.Id.Equals(tabId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                ActivateTab(existing);
                return;
            }

            string ext = Path.GetExtension(fullPath).ToLowerInvariant();
            string icon = MediaService.IsImageFile(fullPath) ? "🖼️"
                        : MediaService.IsVideoFile(fullPath) ? "🎬"
                        : (ext is ".bat" or ".cmd" or ".ps1") ? "⚡"
                        : "📄";

            WorkspaceTabItem? tabRef = null;
            var fileView = new FileTabView(
                fullPath,
                onSendToMedia: path =>
                {
                    _mediaView.ImportExternalFiles(new[] { path });
                    OpenOrActivateModuleTab("Media");
                    Logger.Success($"🎬 Đã chuyển tệp [{Path.GetFileName(path)}] sang công cụ Xử Lý Media.");
                },
                onTitleChanged: newName =>
                {
                    if (tabRef != null) tabRef.Title = newName;
                });

            tabRef = new WorkspaceTabItem
            {
                Id = tabId,
                Icon = icon,
                Title = Path.GetFileName(fullPath),
                ToolTipText = fullPath,
                FilePath = fullPath,
                Content = fileView
            };

            _openTabs.Add(tabRef);
            ActivateTab(tabRef);
            Logger.Info($"📂 Đã mở tệp vào Tab: {fullPath}");
        }

        private void CreateNewScratchpadTab()
        {
            string defaultName = $"Note_{_scratchpadCounter++}.txt";
            string tabId = $"note::{Guid.NewGuid():N}";

            WorkspaceTabItem? tabRef = null;
            var noteView = new FileTabView(
                defaultName,
                onTitleChanged: savedName =>
                {
                    if (tabRef != null) tabRef.Title = savedName;
                });

            tabRef = new WorkspaceTabItem
            {
                Id = tabId,
                Icon = "📝",
                Title = defaultName,
                ToolTipText = "Tab ghi chú / kịch bản nháp",
                Content = noteView
            };

            _openTabs.Add(tabRef);
            ActivateTab(tabRef);
        }

        private void ActivateTab(WorkspaceTabItem? tab)
        {
            _activeTab = tab;
            foreach (var t in _openTabs)
            {
                t.IsSelected = ReferenceEquals(t, tab);
            }

            if (tab != null)
            {
                MainContentHost.Content = tab.Content;
                MainContentHost.Visibility = Visibility.Visible;
                PnlEmptyWorkspace.Visibility = Visibility.Collapsed;
                // Bỏ chọn nút RadioButton trên Sidebar vì đang xem tab file riêng biệt
                SyncSidebarSelection(null);
            }
            else
            {
                // Khi không còn tab file nào được chọn, tự động quay về Module chính đang chọn
                SwitchToModule(_currentModule);
            }

            UpdateTabVisibility();
            UpdateTabCountStatus();
        }

        private void CloseTab(WorkspaceTabItem? tab)
        {
            if (tab == null) return;
            int idx = _openTabs.IndexOf(tab);
            if (idx < 0) return;

            bool wasActive = ReferenceEquals(tab, _activeTab);
            _openTabs.RemoveAt(idx);

            if (wasActive)
            {
                if (_openTabs.Count > 0)
                {
                    int nextIdx = Math.Min(idx, _openTabs.Count - 1);
                    ActivateTab(_openTabs[nextIdx]);
                }
                else
                {
                    ActivateTab(null);
                }
            }
            else
            {
                UpdateTabVisibility();
                UpdateTabCountStatus();
            }
        }

        private void UpdateTabVisibility()
        {
            if (TabStripBorder != null)
            {
                // Thanh Tab Strip chỉ hiển thị khi có ít nhất 1 file/nháp được mở
                TabStripBorder.Visibility = _openTabs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SyncSidebarSelection(string? activeId)
        {
            if (NavOptimizer != null) NavOptimizer.IsChecked = string.Equals(activeId, "Optimizer", StringComparison.OrdinalIgnoreCase);
            if (NavNetwork != null) NavNetwork.IsChecked = string.Equals(activeId, "Network", StringComparison.OrdinalIgnoreCase);
            if (NavUtilities != null) NavUtilities.IsChecked = string.Equals(activeId, "Utilities", StringComparison.OrdinalIgnoreCase);
            if (NavMedia != null) NavMedia.IsChecked = string.Equals(activeId, "Media", StringComparison.OrdinalIgnoreCase);
            if (NavGuide != null) NavGuide.IsChecked = string.Equals(activeId, "Guide", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateTabCountStatus()
        {
            if (TxtStatusTabCount != null)
            {
                TxtStatusTabCount.Text = $"{_openTabs.Count} Tab";
            }
        }

        // ================= SỰ KIỆN TAB BAR & SIDEBAR =================

        private void SidebarModule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is string moduleId)
            {
                OpenOrActivateModuleTab(moduleId);
            }
        }

        private void TabItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is WorkspaceTabItem tab)
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    CloseTab(tab);
                }
                else
                {
                    ActivateTab(tab);
                }
            }
        }

        private void BtnCloseTab_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is Button btn && btn.Tag is WorkspaceTabItem tab)
            {
                CloseTab(tab);
            }
        }

        // Đóng / Mở toàn bộ thanh Sidebar Modules
        private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _isSidebarOpen = !_isSidebarOpen;
            SidebarPanel.Visibility = _isSidebarOpen ? Visibility.Visible : Visibility.Collapsed;
            ColSidebar.Width = _isSidebarOpen ? new GridLength(215) : new GridLength(0);

            if (TxtStatusSidebar != null)
            {
                TxtStatusSidebar.Text = _isSidebarOpen ? "◧ Modules: Mở" : "◧ Modules: Đóng";
            }
        }

        // ================= SỰ KIỆN TOP DROPDOWN MENU & KÉO THẢ FILE =================

        private void MenuOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp để mở vào Tab mới",
                Filter = "Tất cả tệp (*.*)|*.*|Văn bản & Script (*.txt;*.log;*.bat;*.cmd;*.ps1;*.json;*.md)|*.txt;*.log;*.bat;*.cmd;*.ps1;*.json;*.md|Ảnh & Video (*.png;*.jpg;*.webp;*.mp4;*.mkv;*.mov)|*.png;*.jpg;*.jpeg;*.webp;*.mp4;*.mkv;*.mov",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                foreach (var file in dlg.FileNames)
                {
                    OpenFileInTab(file);
                }
            }
        }

        private void MenuImportToMedia_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn các tệp Ảnh / Video đưa vào Xử Lý Media",
                Filter = "Media Files|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
            {
                _mediaView.ImportExternalFiles(dlg.FileNames);
                OpenOrActivateModuleTab("Media");
                Logger.Success($"🎬 Đã nhập {dlg.FileNames.Length} tệp vào bảng Xử Lý Media.");
            }
        }

        private void MenuNewScratchpad_Click(object sender, RoutedEventArgs e)
        {
            CreateNewScratchpadTab();
        }

        private void MenuCloseCurrentTab_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab != null)
            {
                CloseTab(_activeTab);
            }
        }

        private void MenuCloseAllTabs_Click(object sender, RoutedEventArgs e)
        {
            _openTabs.Clear();
            ActivateTab(null);
        }

        private void MenuOpenModule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is string moduleId)
            {
                OpenOrActivateModuleTab(moduleId);
            }
        }

        private void MenuCopyActiveTab_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab?.Content is FileTabView fileView)
            {
                try
                {
                    Clipboard.SetDataObject(fileView.GetEditorText(), false);
                    Logger.Info($"📋 Đã sao chép nội dung từ tab [{_activeTab.Title}].");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Không thể sao chép: {ex.Message}");
                }
            }
            else
            {
                Logger.Info("Hãy chọn một Tab tệp văn bản / ghi chú để sao chép nội dung.");
            }
        }

        private void MenuRunActiveTab_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab?.Content is FileTabView fileView && fileView.CanRunScript)
            {
                fileView.ExecuteScriptDirectly();
            }
            else
            {
                MenuCmdSystemInfo_Click(sender, e);
            }
        }

        private void MenuExportLog_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Title = "Xuất Log Terminal ra File",
                Filter = "Log File (*.log)|*.log|Text File (*.txt)|*.txt",
                FileName = $"cmd_box_log_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, _logHistory.ToString(), Encoding.UTF8);
                    Logger.Success($"Đã xuất log ra: {sfd.FileName}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Không thể lưu log: {ex.Message}");
                }
            }
        }

        private void MenuOpenAppFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Không thể mở thư mục app: {ex.Message}");
            }
        }

        private void MenuOpenTempFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.GetTempPath(),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Không thể mở thư mục %temp%: {ex.Message}");
            }
        }

        private void MenuLaunchTaskManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "taskmgr.exe", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Không thể mở Task Manager: {ex.Message}");
            }
        }

        private void MenuLaunchDeviceManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "devmgmt.msc", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Không thể mở Device Manager: {ex.Message}");
            }
        }

        private void MenuLaunchNetworkConnections_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "ncpa.cpl", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Không thể mở Network Connections: {ex.Message}");
            }
        }

        private void MenuLaunchServices_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "services.msc", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Không thể mở Services: {ex.Message}");
            }
        }

        private void MenuLaunchRegedit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "regedit.exe", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Không thể mở Registry Editor: {ex.Message}");
            }
        }

        private async void MenuCmdSystemInfo_Click(object sender, RoutedEventArgs e)
        {
            if (!_isTerminalVisible) ToggleTerminalVisibility();
            Logger.Info("▶ Đang truy xuất thông tin hệ thống Windows (systeminfo)...");
            await ProcessRunner.RunProcessAsync("cmd.exe", "/c systeminfo",
                line => { if (!string.IsNullOrWhiteSpace(line)) Logger.Info(line); },
                err => { if (!string.IsNullOrWhiteSpace(err)) Logger.Warning(err); });
        }

        private void MenuCmdPingTest_Click(object sender, RoutedEventArgs e)
        {
            if (!_isTerminalVisible) ToggleTerminalVisibility();
            Logger.Info("▶ Bật tiến trình kiểm tra Ping mạng liên tục (8.8.8.8)...");
            _ = Task.Run(async () =>
            {
                await ProcessRunner.RunProcessAsync("cmd.exe", "/c ping 8.8.8.8 -t",
                    line => { if (!string.IsNullOrWhiteSpace(line)) Logger.Info(line); },
                    err => { if (!string.IsNullOrWhiteSpace(err)) Logger.Warning(err); });
            });
        }

        private void MenuLaunchExternalCmd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Không thể mở CMD ngoài: {ex.Message}");
            }
        }

        private void MenuLaunchExternalPowerShell_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Không thể mở PowerShell ngoài: {ex.Message}");
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Workspace_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void Workspace_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    OpenFileInTab(path);
                }
                else if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path).Take(10);
                    foreach (var f in files) OpenFileInTab(f);
                }
            }
            e.Handled = true;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.B:
                        BtnToggleSidebar_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.O:
                        MenuOpenFile_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.N:
                        CreateNewScratchpadTab();
                        e.Handled = true;
                        break;
                    case Key.W:
                        if (_activeTab != null) CloseTab(_activeTab);
                        e.Handled = true;
                        break;
                    case Key.J:
                        ToggleTerminalVisibility();
                        e.Handled = true;
                        break;
                }
            }
        }

        // ================= QUẢN LÝ TERMINAL & THEME =================

        private void MenuToggleTerminal_Click(object sender, RoutedEventArgs e)
        {
            ToggleTerminalVisibility();
        }

        private void ToggleTerminalVisibility()
        {
            _isTerminalVisible = !_isTerminalVisible;
            if (_isTerminalVisible)
            {
                TerminalContainer.Visibility = Visibility.Visible;
                TerminalSplitter.Visibility = Visibility.Visible;
                RowSplitter.Height = new GridLength(2);
                RowTerminal.MinHeight = 85;
                RowTerminal.Height = new GridLength(_isTerminalExpanded ? 320 : 175);
            }
            else
            {
                TerminalContainer.Visibility = Visibility.Collapsed;
                TerminalSplitter.Visibility = Visibility.Collapsed;
                RowSplitter.Height = new GridLength(0);
                RowTerminal.MinHeight = 0;
                RowTerminal.Height = new GridLength(0);
            }

            if (TxtStatusTerminal != null)
            {
                TxtStatusTerminal.Text = _isTerminalVisible ? "⬓ Terminal: Hiện" : "⬓ Terminal: Ẩn";
            }
        }

        private void BtnToggleTerminalHeight_Click(object sender, RoutedEventArgs e)
        {
            if (!_isTerminalVisible) ToggleTerminalVisibility();
            _isTerminalExpanded = !_isTerminalExpanded;
            RowTerminal.Height = new GridLength(_isTerminalExpanded ? 320 : 175);
            BtnToggleTerminalHeight.Content = _isTerminalExpanded ? "🗗" : "🗖";
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

            // Chống tràn bộ nhớ & lag UI: Giới hạn dòng cho cả TextBox và bộ đệm lịch sử
            if (_lineCount > MaxLogLines)
            {
                string currentText = TxtTerminal.Text;
                int newlineIndex = currentText.Length > 0 ? currentText.IndexOf('\n', currentText.Length / 2) : -1;
                if (newlineIndex >= 0)
                {
                    string trimmed = currentText.Substring(newlineIndex + 1);
                    TxtTerminal.Text = trimmed;
                    _logHistory.Clear();
                    _logHistory.Append(trimmed);
                    _lineCount = trimmed.Count(c => c == '\n');
                }
                else
                {
                    TxtTerminal.Clear();
                    _logHistory.Clear();
                    _lineCount = 0;
                }
            }

            TxtTerminal.AppendText(sb.ToString());
            TxtTerminal.ScrollToEnd();
        }

        private void BtnToggleAdminCmd_Click(object sender, RoutedEventArgs e)
        {
            bool nextState = !SettingsService.Current.AllowAdminCmd;
            SettingsService.UpdateAllowAdminCmd(nextState);
            UpdateAdminCmdUIState();

            if (nextState)
            {
                Logger.Success("Admin CMD: BẬT.");
            }
            else
            {
                Logger.Info("Admin CMD: TẮT.");
            }
        }

        private void UpdateAdminCmdUIState()
        {
            bool isAllowed = SettingsService.Current.AllowAdminCmd;

            if (TxtStatusAdmin != null)
            {
                TxtStatusAdmin.Text = isAllowed ? "🛡️ Admin: BẬT" : "🔒 Admin: TẮT";
            }
        }

        private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            ThemeService.ToggleTheme();
            UpdateThemeUIState();
            Logger.Info(ThemeService.IsDarkMode ? "Đã chuyển sang Giao diện Tối." : "Đã chuyển sang Giao diện Sáng.");
        }

        private void UpdateThemeUIState()
        {
            string icon = ThemeService.IsDarkMode ? "🌙" : "☀️";
            if (TxtTopThemeIcon != null) TxtTopThemeIcon.Text = icon;

            if (TxtStatusTheme != null)
            {
                TxtStatusTheme.Text = ThemeService.IsDarkMode ? "🌙 Dark+" : "☀️ Light+";
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
            try
            {
                Clipboard.SetDataObject(_logHistory.ToString(), false);
                Logger.Info("📋 Đã sao chép Log vào Clipboard!");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Không thể truy cập Clipboard: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _logBatchTimer.Stop();
            base.OnClosed(e);
        }
    }
}