using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace GithubMarkdownViewer.Services;

/// <summary>
/// Simple file-based application logger.
/// Logs are written to a "logs" folder in the user's AppData directory.
/// Automatically rotates when the log file exceeds 10 MB and prunes logs older than 30 days.
/// </summary>
public static class AppLogger
{
    private static readonly object Lock = new();
    private static readonly string LogDirectory;
    private static string _logFilePath;
    private const long MaxLogFileSize = 10 * 1024 * 1024; // 10 MB
    private const int MaxLogAgeDays = 30;
    private static bool _pruneDone;

    static AppLogger()
    {
        LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GithubMarkdownViewer", "logs");
        Directory.CreateDirectory(LogDirectory);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd");
        _logFilePath = Path.Combine(LogDirectory, $"app-{timestamp}.log");
    }

    public static string CurrentLogPath => _logFilePath;

    public static void Info(string message, [CallerMemberName] string? caller = null)
        => Write("INFO", message, caller);

    public static void Warn(string message, [CallerMemberName] string? caller = null)
        => Write("WARN", message, caller);

    public static void Error(string message, Exception? ex = null, [CallerMemberName] string? caller = null)
    {
        var full = ex != null ? $"{message} [{ex.GetType().Name}: {ex.Message}]" : message;
        Write("ERROR", full, caller);
    }

    public static void Fatal(string message, Exception? ex = null, [CallerMemberName] string? caller = null)
    {
        var full = ex != null ? $"{message} [{ex.GetType().Name}: {ex.Message}]" : message;
        Write("FATAL", full, caller);
    }

    private static void Write(string level, string message, string? caller)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var line = $"[{timestamp}] [{level}] [{caller}] {message}";
            lock (Lock)
            {
                RotateIfNeeded();
                File.AppendAllText(_logFilePath, line + Environment.NewLine);

                if (!_pruneDone)
                {
                    _pruneDone = true;
                    PruneOldLogs();
                }
            }
        }
        catch
        {
            // Logging must never throw
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(_logFilePath)) return;
            var info = new FileInfo(_logFilePath);
            if (info.Length < MaxLogFileSize) return;

            // Rename current log with a sequence number
            var rotatedPath = Path.Combine(LogDirectory,
                $"app-{DateTime.Now:yyyy-MM-dd}-{DateTime.Now:HHmmss}.log");
            File.Move(_logFilePath, rotatedPath);
        }
        catch
        {
            // Rotation failure must not prevent logging
        }
    }

    private static void PruneOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-MaxLogAgeDays);
            foreach (var file in Directory.GetFiles(LogDirectory, "app-*.log"))
            {
                var fi = new FileInfo(file);
                if (fi.LastWriteTime < cutoff)
                    fi.Delete();
            }
        }
        catch
        {
            // Pruning failure must not prevent logging
        }
    }
}
