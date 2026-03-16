using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace GithubMarkdownViewer.Services;

/// <summary>
/// Simple file-based application logger.
/// Logs are written to a "logs" folder in the user's AppData directory.
/// </summary>
public static class AppLogger
{
    private static readonly object Lock = new();
    private static readonly string LogDirectory;
    private static readonly string LogFilePath;

    static AppLogger()
    {
        LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GithubMarkdownViewer", "logs");
        Directory.CreateDirectory(LogDirectory);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd");
        LogFilePath = Path.Combine(LogDirectory, $"app-{timestamp}.log");
    }

    public static string CurrentLogPath => LogFilePath;

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
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never throw
        }
    }
}
