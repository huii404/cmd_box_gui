using System;
using System.IO;
using System.Text.Json;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Models;

namespace CMD_BOX_GUI.Services
{
    public static class SettingsService
    {
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private static readonly object _lock = new();

        public static AppSettings Current { get; private set; } = new();

        static SettingsService()
        {
            LoadSettings();
        }

        public static void LoadSettings()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(ConfigFilePath))
                    {
                        string json = File.ReadAllText(ConfigFilePath);
                        var settings = JsonSerializer.Deserialize<AppSettings>(json);
                        if (settings != null)
                        {
                            Current = settings;
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Không thể đọc config.json, khôi phục cài đặt mặc định: {ex.Message}");
                }

                // Nếu không có file hoặc file bị lỗi, tự động reset về mặc định
                Current = new AppSettings();
                SaveSettings();
            }
        }

        public static void SaveSettings()
        {
            lock (_lock)
            {
                try
                {
                    Current.LastSavedTime = DateTime.Now;
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(Current, options);
                    File.WriteAllText(ConfigFilePath, json);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Lỗi khi ghi tệp config.json: {ex.Message}");
                }
            }
        }

        public static void UpdateTheme(bool isDark)
        {
            Current.IsDarkMode = isDark;
            SaveSettings();
        }

        public static void UpdateAllowAdminCmd(bool allow)
        {
            Current.AllowAdminCmd = allow;
            SaveSettings();
        }

        public static void ResetToDefaults()
        {
            lock (_lock)
            {
                Current = new AppSettings();
                SaveSettings();
                Logger.Info("Đã đặt lại cấu hình mặc định.");
            }
        }
    }
}
