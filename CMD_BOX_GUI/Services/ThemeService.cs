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
            { "BgPrimary", (Color)ColorConverter.ConvertFromString("#1E1E1E") },
            { "BgSecondary", (Color)ColorConverter.ConvertFromString("#252526") },
            { "ActivityBarBg", (Color)ColorConverter.ConvertFromString("#333333") },
            { "ActivityBarFg", (Color)ColorConverter.ConvertFromString("#858585") },
            { "ActivityBarActiveFg", (Color)ColorConverter.ConvertFromString("#FFFFFF") },
            { "ActivityBarActiveBorder", (Color)ColorConverter.ConvertFromString("#007ACC") },

            { "EditorTabBg", (Color)ColorConverter.ConvertFromString("#2D2D2D") },
            { "EditorTabActiveBg", (Color)ColorConverter.ConvertFromString("#1E1E1E") },
            { "EditorTabBorder", (Color)ColorConverter.ConvertFromString("#252526") },
            { "BreadcrumbsBg", (Color)ColorConverter.ConvertFromString("#1E1E1E") },

            { "StatusBarBg", (Color)ColorConverter.ConvertFromString("#007ACC") },
            { "StatusBarFg", (Color)ColorConverter.ConvertFromString("#FFFFFF") },
            { "StatusBarSubtle", (Color)ColorConverter.ConvertFromString("#005A9E") },

            { "BgCard", (Color)ColorConverter.ConvertFromString("#252526") },
            { "BgCardHover", (Color)ColorConverter.ConvertFromString("#2A2D2E") },
            { "BgInput", (Color)ColorConverter.ConvertFromString("#3C3C3C") },

            { "BorderSubtle", (Color)ColorConverter.ConvertFromString("#3C3C3C") },
            { "BorderActive", (Color)ColorConverter.ConvertFromString("#007ACC") },

            { "TextPrimary", (Color)ColorConverter.ConvertFromString("#CCCCCC") },
            { "TextSecondary", (Color)ColorConverter.ConvertFromString("#969696") },
            { "TextMuted", (Color)ColorConverter.ConvertFromString("#6A737D") },

            { "AccentPrimary", (Color)ColorConverter.ConvertFromString("#0E639C") },
            { "AccentPrimaryHover", (Color)ColorConverter.ConvertFromString("#1177BB") },
            { "AccentSuccess", (Color)ColorConverter.ConvertFromString("#89D185") },
            { "AccentWarning", (Color)ColorConverter.ConvertFromString("#CCA700") },
            { "AccentDanger", (Color)ColorConverter.ConvertFromString("#F14C4C") },
            { "AccentCyan", (Color)ColorConverter.ConvertFromString("#4FC1FF") },
            { "AccentPurple", (Color)ColorConverter.ConvertFromString("#C586C0") },

            { "TerminalBg", (Color)ColorConverter.ConvertFromString("#181818") },
            { "TerminalText", (Color)ColorConverter.ConvertFromString("#4EC9B0") }
        };

        private static readonly Dictionary<string, Color> LightPalette = new()
        {
            // Màu nền chính: Tone trắng dịu mắt #F4F6F8 (Soft warm-grey canvas, chống chói, êm mắt)
            { "BgPrimary", (Color)ColorConverter.ConvertFromString("#F4F6F8") },
            // Sidebar và Header: Tone xám bạc êm ái #E9ECEF
            { "BgSecondary", (Color)ColorConverter.ConvertFromString("#E9ECEF") },
            { "ActivityBarBg", (Color)ColorConverter.ConvertFromString("#2C2C2C") },
            { "ActivityBarFg", (Color)ColorConverter.ConvertFromString("#999999") },
            { "ActivityBarActiveFg", (Color)ColorConverter.ConvertFromString("#FFFFFF") },
            { "ActivityBarActiveBorder", (Color)ColorConverter.ConvertFromString("#0969DA") },

            { "EditorTabBg", (Color)ColorConverter.ConvertFromString("#DFE3E8") },
            { "EditorTabActiveBg", (Color)ColorConverter.ConvertFromString("#FFFFFF") },
            { "EditorTabBorder", (Color)ColorConverter.ConvertFromString("#D0D5DD") },
            { "BreadcrumbsBg", (Color)ColorConverter.ConvertFromString("#F4F6F8") },

            { "StatusBarBg", (Color)ColorConverter.ConvertFromString("#0969DA") },
            { "StatusBarFg", (Color)ColorConverter.ConvertFromString("#FFFFFF") },
            { "StatusBarSubtle", (Color)ColorConverter.ConvertFromString("#0858B8") },

            // Khối Cards & Panels: Nổi nhẹ trên nền xám với viền mềm #D5D9E0
            { "BgCard", (Color)ColorConverter.ConvertFromString("#FFFFFF") },
            { "BgCardHover", (Color)ColorConverter.ConvertFromString("#EDF1F5") },
            { "BgInput", (Color)ColorConverter.ConvertFromString("#EDF1F5") },

            { "BorderSubtle", (Color)ColorConverter.ConvertFromString("#D5D9E0") },
            { "BorderActive", (Color)ColorConverter.ConvertFromString("#0969DA") },

            // Chữ: Đen than chì tự nhiên #1F2328 dễ đọc, không gây mỏi mắt
            { "TextPrimary", (Color)ColorConverter.ConvertFromString("#1F2328") },
            { "TextSecondary", (Color)ColorConverter.ConvertFromString("#57606A") },
            { "TextMuted", (Color)ColorConverter.ConvertFromString("#6E7781") },

            { "AccentPrimary", (Color)ColorConverter.ConvertFromString("#0969DA") },
            { "AccentPrimaryHover", (Color)ColorConverter.ConvertFromString("#0858B8") },
            { "AccentSuccess", (Color)ColorConverter.ConvertFromString("#1A7F37") },
            { "AccentWarning", (Color)ColorConverter.ConvertFromString("#9A6700") },
            { "AccentDanger", (Color)ColorConverter.ConvertFromString("#CF222E") },
            { "AccentCyan", (Color)ColorConverter.ConvertFromString("#0969DA") },
            { "AccentPurple", (Color)ColorConverter.ConvertFromString("#8250DF") },

            { "TerminalBg", (Color)ColorConverter.ConvertFromString("#E9ECEF") },
            { "TerminalText", (Color)ColorConverter.ConvertFromString("#1F2328") }
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
