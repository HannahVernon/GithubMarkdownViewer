using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace GithubMarkdownViewer.Services;

/// <summary>Puts HTML and plain text on the clipboard together, so rich editors get formatting and others get text.</summary>
public static class HtmlClipboard
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static async Task SetAsync(IClipboard clipboard, string html, string plainText)
    {
        var item = new DataTransferItem();
        item.SetText(plainText);
        item.Set(DataFormat.CreateBytesPlatformFormat(PlatformFormatName), PlatformBytes(html));

        var data = new DataTransfer();
        data.Add(item);
        await clipboard.SetDataAsync(data);
        // Keep the data on the clipboard after the app closes.
        await clipboard.FlushAsync();
    }

    private static string PlatformFormatName =>
        OperatingSystem.IsWindows() ? "HTML Format"
        : OperatingSystem.IsMacOS() ? "public.html"
        : "text/html";

    private static byte[] PlatformBytes(string html) =>
        OperatingSystem.IsWindows() ? ToCfHtml(html) : Utf8.GetBytes(html);

    /// <summary>
    /// Wraps an HTML fragment in the Windows CF_HTML format: a header of byte offsets into the UTF-8 data,
    /// then the HTML with comments marking where the fragment starts and ends.
    /// </summary>
    public static byte[] ToCfHtml(string fragment)
    {
        const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        const string before = "<html><body>\r\n<!--StartFragment-->";
        const string after = "<!--EndFragment-->\r\n</body></html>";

        // Every offset is written with ten digits, so the header length does not depend on the values.
        var startHtml = Utf8.GetByteCount(string.Format(CultureInfo.InvariantCulture, header, 0, 0, 0, 0));
        var startFragment = startHtml + Utf8.GetByteCount(before);
        var endFragment = startFragment + Utf8.GetByteCount(fragment);
        var endHtml = endFragment + Utf8.GetByteCount(after);

        var head = string.Format(CultureInfo.InvariantCulture, header, startHtml, endHtml, startFragment, endFragment);
        return Utf8.GetBytes(head + before + fragment + after);
    }
}
