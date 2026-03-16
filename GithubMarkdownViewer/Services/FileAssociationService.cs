using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GithubMarkdownViewer.Services;

/// <summary>
/// Checks and sets file extension associations.
/// Currently supports Windows only; other platforms are a graceful no-op.
/// </summary>
public static class FileAssociationService
{
    private const string ProgId = "GithubMarkdownViewer.md";
    private const string FileExtension = ".md";
    private const string FileDescription = "Markdown Document";

    /// <summary>
    /// Returns true if .md files are currently associated with this application.
    /// On non-Windows platforms, always returns true (no-op).
    /// </summary>
    public static bool IsAssociated()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return true;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{FileExtension}");
            if (key == null) return false;

            var progId = key.GetValue(null) as string;
            return string.Equals(progId, ProgId, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to check file association", ex);
            return true; // assume associated on error to avoid nagging
        }
    }

    /// <summary>
    /// Associates .md files with this application for the current user.
    /// On non-Windows platforms, this is a no-op.
    /// </summary>
    public static bool Associate()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            var exePath = Environment.ProcessPath
                          ?? Path.Combine(AppContext.BaseDirectory, "GithubMarkdownViewer.exe");

            // Create ProgId entry
            using (var progKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
            {
                progKey.SetValue(null, FileDescription);

                using var iconKey = progKey.CreateSubKey("DefaultIcon");
                iconKey.SetValue(null, $"\"{exePath}\",0");

                using var cmdKey = progKey.CreateSubKey(@"shell\open\command");
                cmdKey.SetValue(null, $"\"{exePath}\" \"%1\"");
            }

            // Point .md extension to our ProgId
            using (var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{FileExtension}"))
            {
                extKey.SetValue(null, ProgId);
            }

            // Notify the shell that associations changed
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

            AppLogger.Info("File association set for .md files");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to set file association", ex);
            return false;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
