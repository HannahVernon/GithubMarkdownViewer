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
    /// Describes the current state of the .md file association.
    /// </summary>
    public record AssociationStatus(
        bool IsOurs,
        string? CurrentHandler,
        string? CurrentExePath);

    /// <summary>
    /// Returns detailed information about the current .md file association.
    /// On non-Windows platforms, returns IsOurs = true (no-op).
    /// </summary>
    public static AssociationStatus GetStatus()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new AssociationStatus(true, null, null);

        try
        {
            var ourExePath = Environment.ProcessPath
                             ?? Path.Combine(AppContext.BaseDirectory, "GithubMarkdownViewer.exe");

            // UserChoice takes highest priority on Windows 8+
            var userChoiceProgId = GetUserChoiceProgId();
            if (!string.IsNullOrEmpty(userChoiceProgId))
            {
                var exePath = GetExePathForProgId(userChoiceProgId);
                var isOurs = IsOurExe(exePath, ourExePath);
                return new AssociationStatus(isOurs, userChoiceProgId, exePath);
            }

            // Fall back to HKCU\Software\Classes\.md
            using var hkcuKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{FileExtension}");
            var hkcuProgId = hkcuKey?.GetValue(null) as string;
            if (!string.IsNullOrEmpty(hkcuProgId))
            {
                var exePath = GetExePathForProgId(hkcuProgId);
                var isOurs = IsOurExe(exePath, ourExePath);
                return new AssociationStatus(isOurs, hkcuProgId, exePath);
            }

            // Fall back to HKLM\Software\Classes\.md
            using var hklmKey = Registry.LocalMachine.OpenSubKey($@"Software\Classes\{FileExtension}");
            var hklmProgId = hklmKey?.GetValue(null) as string;
            if (!string.IsNullOrEmpty(hklmProgId))
            {
                var exePath = GetExePathForProgId(hklmProgId);
                var isOurs = IsOurExe(exePath, ourExePath);
                return new AssociationStatus(isOurs, hklmProgId, exePath);
            }

            return new AssociationStatus(false, null, null);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to check file association", ex);
            return new AssociationStatus(true, null, null);
        }
    }

    /// <summary>
    /// Returns true if .md files are currently associated with this application.
    /// On non-Windows platforms, always returns true (no-op).
    /// </summary>
    public static bool IsAssociated() => GetStatus().IsOurs;

    /// <summary>
    /// Associates .md files with this application for the current user.
    /// Registers our ProgId and opens the Windows "Open With" dialog
    /// so the user can confirm the default app selection (required on
    /// Windows 10+ where UserChoice is hash-protected).
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

            // Register our ProgId so it appears in the "Open With" list
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

            // Register in OpenWithProgids so we appear in the "Open With" list
            using (var openWithKey = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{FileExtension}\OpenWithProgids"))
            {
                openWithKey.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
            }

            // Notify the shell that associations changed
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

            AppLogger.Info("File association registered for .md files");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to set file association", ex);
            return false;
        }
    }

    /// <summary>
    /// Opens the Windows "Open With" dialog for .md files so the user
    /// can select the default application. This is the only reliable way
    /// to set UserChoice on Windows 10+ (the hash is protected).
    /// </summary>
    public static void OpenSystemDefaultAppSettings()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            // Opens Settings > Default Apps for .md
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:defaultapps",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to open default app settings", ex);
        }
    }

    private static string? GetUserChoiceProgId()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{FileExtension}\UserChoice");
        return key?.GetValue("ProgId") as string;
    }

    private static string? GetExePathForProgId(string progId)
    {
        // Check HKCU first, then HKLM
        var exePath = GetExePathFromRoot(Registry.CurrentUser, progId);
        if (exePath != null) return exePath;

        return GetExePathFromRoot(Registry.ClassesRoot, progId);
    }

    private static string? GetExePathFromRoot(RegistryKey root, string progId)
    {
        using var cmdKey = root.OpenSubKey($@"Software\Classes\{progId}\shell\open\command")
                           ?? root.OpenSubKey($@"{progId}\shell\open\command");
        var command = cmdKey?.GetValue(null) as string;
        if (string.IsNullOrEmpty(command)) return null;

        // Parse exe path from command like: "C:\path\to\app.exe" "%1"
        if (command.StartsWith('"'))
        {
            var endQuote = command.IndexOf('"', 1);
            return endQuote > 1 ? command.Substring(1, endQuote - 1) : null;
        }

        var spaceIdx = command.IndexOf(' ');
        return spaceIdx > 0 ? command.Substring(0, spaceIdx) : command;
    }

    private static bool IsOurExe(string? exePath, string ourExePath)
    {
        if (string.IsNullOrEmpty(exePath)) return false;
        return string.Equals(
            Path.GetFullPath(exePath),
            Path.GetFullPath(ourExePath),
            StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
