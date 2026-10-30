using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
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
        private WorkspaceTabItem? _activeTab;
        private int _scratchpadCounter = 1;

        private bool _isSidebarOpen = true;
        private bool _isModuleListExpanded = true;
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

            // Mở sẵn tab System Optimizer mặc định khi khởi động
            OpenOrActivateModuleTab("Optimizer");

            // Timer batch update log chống lag giao diện
            _logBatchTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _logBatchTimer.Tick += FlushLogQueue;
            _logBatchTimer.Start();

            bool isAdmin = SystemCore.IsAdministrator();
            Logger.Info("Khởi động CMD BOX GUI hoàn tất.");
            Logger.Info(isAdmin
                ? "Quyền tiến trình: Administrator."
                : "Quyền tiến trình: Standard User (Chế độ lệnh thường).");
        }

        // ================= QUẢN LÝ ĐA TAB & NHẬP FILE =================

        private void OpenOrActivateModuleTab(string moduleId)
        {
            var existing = _openTabs.FirstOrDefault(t => t.Id.Equals(moduleId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                ActivateTab(existing);
                return;
            }

            WorkspaceTabItem newTab = moduleId switch
            {
                "Network" => new WorkspaceTabItem
                {
                    Id = "Network",
                    Icon = "🌐",
                    Title = "Mạng & Bảo Mật",
                    ToolTipText = "Module: Sửa lỗi Internet & Quét mạng LAN",
                    Content = _networkView
                },
                "Utilities" => new WorkspaceTabItem
                {
                    Id = "Utilities",
                    Icon = "⚡",
                    Title = "Tiện Ích & Tự Động",
                    ToolTipText = "Module: Auto Click, Spam Text, Báo cáo Pin & Winget",
                    Content = _utilitiesView
                },
                "Media" => new WorkspaceTabItem
                {
                    Id = "Media",
                    Icon = "🎬",
                    Title = "Xử Lý Media",
                    ToolTipText = "Module: Nén, Đổi đuôi, Làm nét Ảnh/Video, Tách nhạc, GIF",
                    Content = _mediaView
                },
                "Guide" => new WorkspaceTabItem
                {
                    Id = "Guide",
                    Icon = "📖",
                    Title = "Hướng Dẫn Dùng",
                    ToolTipText = "Module: Tài liệu hướng dẫn sử dụng chi tiết",
                    Content = _guideView
                },
                _ => new WorkspaceTabItem
                {
                    Id = "Optimizer",
                    Icon = "🧹",
                    Title = "System Optimizer",
                    ToolTipText = "Module: Dọn rác & Tối ưu hệ thống Windows",
                    Content = _optimizerView
                }
            };

            _openTabs.Add(newTab);
            ActivateTab(newTab);
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
            }
            else
            {
                MainContentHost.Content = null;
                MainContentHost.Visibility = Visibility.Collapsed;
                PnlEmptyWorkspace.Visibility = Visibility.Visible;
            }

            SyncSidebarSelection(tab?.Id);
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
                UpdateTabCountStatus();
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

        // Thu gọn / Mở rộng danh sách nút con bên trong mục MODULES
        private void BtnFoldModuleList_Click(object sender, RoutedEventArgs e)
        {
            _isModuleListExpanded = !_isModuleListExpanded;
            PnlModuleItems.Visibility = _isModuleListExpanded ? Visibility.Visible : Visibility.Collapsed;
            TxtModulesArrow.Text = _isModuleListExpanded ? "▾" : "▸";
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

        private async void MenuQuickDiagCmd_Click(object sender, RoutedEventArgs e)
        {
            if (!_isTerminalVisible) ToggleTerminalVisibility();
            Logger.Info("▶ Đang kiểm tra nhanh thông tin hệ thống Windows...");
            string osInfo = await ProcessRunner.RunCommandAndGetOutputAsync("cmd.exe", "/c ver");
            Logger.Success($"[OS] {osInfo} | Machine: {Environment.MachineName} | Cores: {Environment.ProcessorCount}");
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "CMD BOX GUI — Multi-Tab Workspace Edition\n\n" +
                "• Hỗ trợ mở nhiều Tab công cụ và nhập File trực tiếp (Ctrl+O / Kéo thả)\n" +
                "• Thanh Modules gọn nhẹ đóng/mở linh hoạt (Ctrl+B)\n" +
                "• Menu ngang sổ Dropdown hiện đại\n" +
                "• Mặc định chạy quyền thường an toàn (asInvoker)",
                "Giới thiệu CMD BOX GUI",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
                Logger.Success("🛡️ Đã BẬT cho phép nâng quyền thực thi Lệnh Admin CMD khi cần.");
            }
            else
            {
                Logger.Info("🔒 Đã TẮT Lệnh Admin CMD — Tất cả tác vụ chạy ở chế độ người dùng thường.");
            }
        }

        private void UpdateAdminCmdUIState()
        {
            bool isAllowed = SettingsService.Current.AllowAdminCmd;

            if (TxtAdminCmdIcon != null) TxtAdminCmdIcon.Text = isAllowed ? "🛡️" : "🔒";
            if (TxtAdminCmdLabel != null) TxtAdminCmdLabel.Text = isAllowed ? "Lệnh Admin: BẬT" : "Lệnh Admin: TẮT";

            if (EllAdminCmdDot != null)
            {
                EllAdminCmdDot.HorizontalAlignment = isAllowed ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                string brushKey = isAllowed ? "AccentSuccess" : "AccentDanger";
                if (Application.Current?.Resources[brushKey] is System.Windows.Media.Brush statusBrush)
                {
                    EllAdminCmdDot.Fill = statusBrush;
                }
            }

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
            if (TxtThemeIcon != null) TxtThemeIcon.Text = icon;
            if (TxtTopThemeIcon != null) TxtTopThemeIcon.Text = icon;
            if (TxtThemeLabel != null) TxtThemeLabel.Text = ThemeService.IsDarkMode ? "Giao diện Tối" : "Giao diện Sáng";

            if (EllThemeDot != null)
            {
                EllThemeDot.HorizontalAlignment = ThemeService.IsDarkMode ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                string brushKey = ThemeService.IsDarkMode ? "AccentCyan" : "AccentWarning";
                if (Application.Current?.Resources[brushKey] is System.Windows.Media.Brush themeBrush)
                {
                    EllThemeDot.Fill = themeBrush;
                }
            }

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