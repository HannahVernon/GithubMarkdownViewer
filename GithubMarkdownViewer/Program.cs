using Avalonia;
using System;
using GithubMarkdownViewer.Services;

namespace GithubMarkdownViewer;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppLogger.Info("Application starting");
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppLogger.Fatal("Unhandled exception in Main", ex);
            throw;
        }
        finally
        {
            AppLogger.Info("Application exiting");
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
