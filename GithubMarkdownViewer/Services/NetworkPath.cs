using System;
using System.IO;

namespace GithubMarkdownViewer.Services;

/// <summary>
/// Rules for network (UNC) paths.  When Windows opens \\server\share\..., it sends the user's NTLM credentials to that
/// server.  A file the user chooses (command line, Open dialog, recent files, reload) is opened without a question.
/// A link inside a document can point anywhere the document's author chose, so a link to a network server that the
/// current document is not already on needs the user's confirmation.
/// </summary>
public static class NetworkPath
{
    /// <summary>True when the path is a UNC path to a network server (\\server\share or \\?\UNC\server\share).</summary>
    public static bool IsNetworkPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var p = path.Replace('/', '\\');
        if (p.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            return true;

        // \\?\C:\... and \\.\device paths are local.
        if (p.StartsWith(@"\\?\", StringComparison.Ordinal) || p.StartsWith(@"\\.\", StringComparison.Ordinal))
            return false;

        return p.StartsWith(@"\\", StringComparison.Ordinal) && p.Length > 2 && p[2] != '\\';
    }

    /// <summary>The server name of a network path, or null for a local path.</summary>
    public static string? ServerOf(string? path)
    {
        if (!IsNetworkPath(path))
            return null;

        var p = path!.Replace('/', '\\');
        var rest = p.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) ? p[8..] : p[2..];
        var end = rest.IndexOf('\\');
        return end < 0 ? rest : rest[..end];
    }

    /// <summary>
    /// True when following a link from <paramref name="currentFile"/> to <paramref name="target"/> needs the user's
    /// confirmation: the target is on a network server, and the current file is not already on that server.
    /// </summary>
    public static bool LinkNeedsConfirmation(string? currentFile, string target)
    {
        var targetServer = ServerOf(target);
        if (targetServer == null)
            return false;

        return !string.Equals(ServerOf(currentFile), targetServer, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The question shown before following such a link.</summary>
    public static string LinkConfirmationMessage(string target) =>
        $"This link opens a file on the network server \\\\{ServerOf(target)}:{Environment.NewLine}{target}{Environment.NewLine}{Environment.NewLine}"
        + "Windows sends your sign-in details to that server when the file is opened.  Open it?";

    /// <summary>A full path for display, or the path as given when it cannot be resolved.</summary>
    public static string Display(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}
