using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using GithubMarkdownViewer.Services;
using GithubMarkdownViewer.ViewModels;
using GithubMarkdownViewer.Views;

namespace GithubMarkdownViewer;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Register global exception handlers
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            AppLogger.Fatal("AppDomain unhandled exception", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLogger.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            var vm = new MainWindowViewModel();

            // If a file was passed as a command-line argument, set it for opening
            if (desktop.Args is { Length: > 0 } && !string.IsNullOrEmpty(desktop.Args[0]))
            {
                var argPath = desktop.Args[0];
                // Block UNC paths to prevent NTLM hash leaks
                if (!argPath.StartsWith(@"\\", StringComparison.Ordinal))
                    vm.StartupFilePath = argPath;
                else
                    AppLogger.Warn("Blocked UNC path from command-line argument");
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
            };
            AppLogger.Info("Main window created");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}