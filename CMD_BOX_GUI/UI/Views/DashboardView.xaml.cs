using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CMD_BOX_GUI.Services;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly ChatbotService _chatbot = new();

        public DashboardView()
        {
            InitializeComponent();

            // Tin nhắn chào mừng ban đầu từ Trợ Lý AI Local
            AddBotMessage("👋 Chào bạn! Tôi là **Trợ Lý AI Local** của CMD BOX GUI.\n\n" +
                          "Tôi có thể hỗ trợ giải đáp và hướng dẫn bạn mọi tính năng:\n" +
                          "• 🧹 **Tối ưu & Dọn rác:** Dọn Temp/Prefetch, tắt app khởi động, sửa Windows Update.\n" +
                          "• 🌐 **Mạng & Wi-Fi:** Bóc tách mật khẩu Wi-Fi, khôi phục mạng 8 bước, bật tường lửa.\n" +
                          "• 🎬 **Xử lý Media:** Nén video hàng loạt (CRF), xuất MP3, giấu file bí mật (Stego).\n" +
                          "• ⚡ **Tiện ích:** Auto Clicker, Auto Paste, chẩn đoán độ chai pin laptop.\n\n" +
                          "👉 Bạn có thể bấm các nút gợi ý phía trên hoặc gõ câu hỏi vào ô bên dưới để bắt đầu!");
        }

        // ================= XỬ LÝ HỎI ĐÁP VỚI TRỢ LÝ AI =================
        private async void BtnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            await ProcessSendMessageAsync();
        }

        private async void TxtChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await ProcessSendMessageAsync();
            }
        }

        private async void QuickPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string text)
            {
                string query = text;
                int spaceIndex = text.IndexOf(' ');
                if (spaceIndex >= 0 && spaceIndex < text.Length - 1)
                {
                    query = text[(spaceIndex + 1)..].Trim('?');
                }

                TxtChatInput.Text = query;
                await ProcessSendMessageAsync();
            }
        }

        private async Task ProcessSendMessageAsync()
        {
            string question = TxtChatInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(question)) return;

            TxtChatInput.Text = string.Empty;
            AddUserMessage(question);

            // Xử lý câu trả lời từ AI Local Engine (1 file duy nhất)
            string answer = await _chatbot.AskAssistantAsync(question);
            AddBotMessage(answer);
        }

        private void BtnClearChat_Click(object sender, RoutedEventArgs e)
        {
            PnlChatMessages.Children.Clear();
            AddBotMessage("✨ Đã làm mới cuộc hội thoại! Bạn cần Trợ Lý AI Local hỗ trợ câu hỏi nào tiếp theo?");
        }

        // ================= TẠO BONG BÓNG TIN NHẮN =================
        private void AddUserMessage(string message)
        {
            var bubble = new Border
            {
                Background = (Brush)FindResource("AccentPrimary"),
                CornerRadius = new CornerRadius(14, 14, 2, 14),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(60, 6, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var sp = new StackPanel();
            var tbText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                FontSize = 12.5,
                LineHeight = 19
            };
            var tbTime = new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm"),
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0)
            };

            sp.Children.Add(tbText);
            sp.Children.Add(tbTime);
            bubble.Child = sp;

            PnlChatMessages.Children.Add(bubble);
            ScrollChatToBottom();
        }

        private void AddBotMessage(string message)
        {
            var container = new Grid
            {
                Margin = new Thickness(0, 6, 60, 6),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Avatar Robot
            var avatarBorder = new Border
            {
                Background = (Brush)FindResource("BgCardHover"),
                BorderBrush = (Brush)FindResource("BorderSubtle"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Width = 36,
                Height = 36,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 10, 0)
            };
            avatarBorder.Child = new TextBlock
            {
                Text = "🤖",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(avatarBorder, 0);

            // Message Bubble
            var bubble = new Border
            {
                Background = (Brush)FindResource("BgCardHover"),
                BorderBrush = (Brush)FindResource("BorderSubtle"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14, 14, 14, 2),
                Padding = new Thickness(14, 10, 14, 10)
            };
            Grid.SetColumn(bubble, 1);

            var sp = new StackPanel();
            var tbHeader = new TextBlock
            {
                Text = "Trợ Lý AI Local",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("AccentCyan"),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var tbText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 12,
                LineHeight = 19
            };

            var tbTime = new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm"),
                FontSize = 9.5,
                Foreground = (Brush)FindResource("TextMuted"),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0)
            };

            sp.Children.Add(tbHeader);
            sp.Children.Add(tbText);
            sp.Children.Add(tbTime);
            bubble.Child = sp;

            container.Children.Add(avatarBorder);
            container.Children.Add(bubble);

            PnlChatMessages.Children.Add(container);
            ScrollChatToBottom();
        }

        private void ScrollChatToBottom()
        {
            Dispatcher.InvokeAsync(() =>
            {
                ChatScrollViewer.ScrollToEnd();
            }, DispatcherPriority.Background);
        }
    }
}
