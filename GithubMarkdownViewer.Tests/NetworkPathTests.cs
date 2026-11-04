using GithubMarkdownViewer.Services;
using Xunit;

namespace GithubMarkdownViewer.Tests;

public class NetworkPathTests
{
    [Theory]
    [InlineData(@"\\fileserver\share\notes.md", true)]
    [InlineData(@"//fileserver/share/notes.md", true)]
    [InlineData(@"\\?\UNC\fileserver\share\notes.md", true)]
    [InlineData(@"\\?\C:\docs\notes.md", false)]
    [InlineData(@"\\.\pipe\something", false)]
    [InlineData(@"C:\docs\notes.md", false)]
    [InlineData(@"docs\notes.md", false)]
    [InlineData(@"\\", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Recognises_network_paths(string? path, bool expected) =>
        Assert.Equal(expected, NetworkPath.IsNetworkPath(path));

    [Theory]
    [InlineData(@"\\fileserver.example.com\share\notes.md", "fileserver.example.com")]
    [InlineData(@"\\?\UNC\fileserver\share\notes.md", "fileserver")]
    [InlineData(@"\\fileserver", "fileserver")]
    [InlineData(@"C:\docs\notes.md", null)]
    public void Server_is_the_first_part_of_a_network_path(string path, string? expected) =>
        Assert.Equal(expected, NetworkPath.ServerOf(path));

    [Theory]
    [InlineData(@"C:\docs\a.md", @"C:\docs\b.md", false)]
    [InlineData(@"\\fileserver\share\a.md", @"\\FILESERVER\other\b.md", false)]
    [InlineData(@"C:\docs\a.md", @"\\fileserver\share\b.md", true)]
    [InlineData(@"\\fileserver\share\a.md", @"\\otherserver\share\b.md", true)]
    [InlineData(null, @"\\fileserver\share\b.md", true)]
    [InlineData(@"\\fileserver\share\a.md", @"C:\docs\b.md", false)]
    public void A_link_needs_confirmation_only_when_it_reaches_a_new_network_server(string? current, string target, bool expected) =>
        Assert.Equal(expected, NetworkPath.LinkNeedsConfirmation(current, target));

    [Fact]
    public void The_confirmation_names_the_server_and_the_file()
    {
        var message = NetworkPath.LinkConfirmationMessage(@"\\fileserver\share\b.md");

        Assert.Contains(@"\\fileserver", message, System.StringComparison.Ordinal);
        Assert.Contains(@"\\fileserver\share\b.md", message, System.StringComparison.Ordinal);
    }
}
