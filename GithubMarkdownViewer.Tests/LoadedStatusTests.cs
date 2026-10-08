using System;
using System.Globalization;
using GithubMarkdownViewer.ViewModels;
using Xunit;

namespace GithubMarkdownViewer.Tests;

public class LoadedStatusTests
{
    [Fact]
    public void ReloadedStatusShowsTheTimeOfTheReload()
    {
        var when = new DateTime(2026, 10, 8, 14, 37, 2);

        Assert.Equal("Reloaded at 2026-10-08 14:37:02", MainWindowViewModel.LoadedStatus("Reloaded", when));
        Assert.Equal("Opened at 2026-10-08 14:37:02", MainWindowViewModel.LoadedStatus("Opened", when));
    }

    [Fact]
    public void TheTimeUsesA24HourClockWhateverTheCulture()
    {
        var when = new DateTime(2026, 1, 2, 3, 4, 5).AddHours(12);
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            Assert.Equal("Reloaded at 2026-01-02 15:04:05", MainWindowViewModel.LoadedStatus("Reloaded", when));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
