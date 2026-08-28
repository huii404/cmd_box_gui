using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace CMD_BOX_GUI.Services
{
    public static class ThemeService
    {
        public static bool IsDarkMode { get; private set; } = true;
        public static event Action<bool>? ThemeChanged;

        private static readonly Dictionary<string, Color> DarkPalette = new()
        {
            { "BgPrimary", (Color)ColorConverter.ConvertFromString("#0B0F19") },
            { "BgSecondary", (Color)ColorConverter.ConvertFromString("#111827") },
            { "BgCard", (Color)ColorConverter.ConvertFromString("#1F2937") },
            { "BgCardHover", (Color)ColorConverter.ConvertFromString("#283548") },
            { "BgInput", (Color)ColorConverter.ConvertFromString("#151E2E") },

            { "BorderSubtle", (Color)ColorConverter.ConvertFromString("#374151") },
            { "BorderActive", (Color)ColorConverter.ConvertFromString("#4B5563") },

            { "TextPrimary", (Color)ColorConverter.ConvertFromString("#F9FAFB") },
            { "TextSecondary", (Color)ColorConverter.ConvertFromString("#9CA3AF") },
            { "TextMuted", (Color)ColorConverter.ConvertFromString("#6B7280") },

            { "AccentPrimary", (Color)ColorConverter.ConvertFromString("#3B82F6") },
            { "AccentPrimaryHover", (Color)ColorConverter.ConvertFromString("#2563EB") },
            { "AccentSuccess", (Color)ColorConverter.ConvertFromString("#10B981") },
            { "AccentWarning", (Color)ColorConverter.ConvertFromString("#F59E0B") },
            { "AccentDanger", (Color)ColorConverter.ConvertFromString("#EF4444") },
            { "AccentCyan", (Color)ColorConverter.ConvertFromString("#06B6D4") },
            { "AccentPurple", (Color)ColorConverter.ConvertFromString("#8B5CF6") },

            { "TerminalBg", (Color)ColorConverter.ConvertFromString("#030712") },
            { "TerminalText", (Color)ColorConverter.ConvertFromString("#10B981") }
        };

        private static readonly Dictionary<string, Color> LightPalette = new()
        {
            { "BgPrimary", (Color)ColorConverter.ConvertFromString("#F1F5F9") },
            { "BgSecondary", (Color)ColorConverter.ConvertFromString("#FFFFFF") },
            { "BgCard", (Color)ColorConverter.ConvertFromString("#FFFFFF") },
            { "BgCardHover", (Color)ColorConverter.ConvertFromString("#F8FAFC") },
            { "BgInput", (Color)ColorConverter.ConvertFromString("#F8FAFC") },

            { "BorderSubtle", (Color)ColorConverter.ConvertFromString("#E2E8F0") },
            { "BorderActive", (Color)ColorConverter.ConvertFromString("#CBD5E1") },

            { "TextPrimary", (Color)ColorConverter.ConvertFromString("#0F172A") },
            { "TextSecondary", (Color)ColorConverter.ConvertFromString("#475569") },
            { "TextMuted", (Color)ColorConverter.ConvertFromString("#94A3B8") },

            { "AccentPrimary", (Color)ColorConverter.ConvertFromString("#2563EB") },
            { "AccentPrimaryHover", (Color)ColorConverter.ConvertFromString("#1D4ED8") },
            { "AccentSuccess", (Color)ColorConverter.ConvertFromString("#059669") },
            { "AccentWarning", (Color)ColorConverter.ConvertFromString("#D97706") },
            { "AccentDanger", (Color)ColorConverter.ConvertFromString("#DC2626") },
            { "AccentCyan", (Color)ColorConverter.ConvertFromString("#0284C7") },
            { "AccentPurple", (Color)ColorConverter.ConvertFromString("#7C3AED") },

            { "TerminalBg", (Color)ColorConverter.ConvertFromString("#E2E8F0") },
            { "TerminalText", (Color)ColorConverter.ConvertFromString("#0F172A") }
        };

        /// <summary>
        /// Khởi tạo đảm bảo các SolidColorBrush trong Application.Current.Resources không bị Freeze
        /// </summary>
        public static void Initialize()
        {
            // Đọc cài đặt đã lưu từ file config.json
            bool savedTheme = SettingsService.Current.IsDarkMode;
            SetTheme(savedTheme, saveSetting: false);
        }

        /// <summary>
        /// Chuyển đổi giữa chế độ Sáng và Tối, tự động cập nhật bảng màu trên toàn bộ ứng dụng
        /// </summary>
        public static void SetTheme(bool darkMode, bool saveSetting = true)
        {
            IsDarkMode = darkMode;
            if (saveSetting)
            {
                SettingsService.UpdateTheme(darkMode);
            }

            var palette = darkMode ? DarkPalette : LightPalette;

            if (Application.Current?.Resources == null) return;

            foreach (var kvp in palette)
            {
                if (Application.Current.Resources[kvp.Key] is SolidColorBrush brush && !brush.IsFrozen)
                {
                    brush.Color = kvp.Value;
                }
                else
                {
                    // Nếu brush chưa tồn tại hoặc bị frozen, gán brush mới có thể chỉnh sửa
                    Application.Current.Resources[kvp.Key] = new SolidColorBrush(kvp.Value);
                }
            }

            ThemeChanged?.Invoke(IsDarkMode);
        }

        public static void ToggleTheme()
        {
            SetTheme(!IsDarkMode, saveSetting: true);
        }
    }
}
