using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CMD_BOX_GUI.Core
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogLevel Level { get; set; } = LogLevel.Info;
        public string Message { get; set; } = string.Empty;

        public string FormattedTime => Timestamp.ToString("HH:mm:ss");

        public string Prefix => Level switch
        {
            LogLevel.Success => "[OK]",
            LogLevel.Warning => "[!]",
            LogLevel.Error => "[ERR]",
            _ => "[*]"
        };

        public override string ToString() => $"{FormattedTime} {Prefix} {Message}";
    }

    public static class Logger
    {
        public static event Action<LogEntry>? OnLog;

        private static readonly ConcurrentQueue<LogEntry> _queue = new();

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            };

            _queue.Enqueue(entry);
            OnLog?.Invoke(entry);
        }

        public static List<LogEntry> DequeueAll()
        {
            var list = new List<LogEntry>();
            while (_queue.TryDequeue(out var item))
            {
                list.Add(item);
            }
            return list;
        }

        public static void Info(string message) => Log(message, LogLevel.Info);
        public static void Success(string message) => Log(message, LogLevel.Success);
        public static void Warning(string message) => Log(message, LogLevel.Warning);
        public static void Error(string message) => Log(message, LogLevel.Error);
    }
}
