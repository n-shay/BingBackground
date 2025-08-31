namespace BingBackground;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ImageMagick;

using Microsoft.Extensions.Logging;
using Microsoft.Win32;

using Newtonsoft.Json;

using static NativeMethods;

public class BingBackgroundUpdater(ILogger<BingBackgroundUpdater> logger)
{
    internal const int SET_DESKTOP_BACKGROUND = 20;
    internal const int UPDATE_INI_FILE = 1;
    internal const int SEND_WINDOWS_INI_CHANGE = 2;
    private const PicturePosition POSITION = PicturePosition.Fill;
    private const string PROXY = null;
    private const int ENUM_CURRENT_SETTINGS = -1;

    private const int DEFAULT_WIDTH = 1920;
    private const int DEFAULT_HEIGHT = 1080;
    private const int MAX_WIDTH = 3840;
    private const int MAX_HEIGHT = 2160;

    private readonly ILogger<BingBackgroundUpdater> logger = logger;

    public async Task ExecuteAsync()
    {
        var (title, description, copyright, url) = await this.GetBackgroundDataAsync();

        await this.UpdateBackgroundAsync(title, description, copyright, url);
    }

    #region Private Methods

    private async Task UpdateBackgroundAsync(string title, string description, string copyright, string urlBase)
    {
        var (width, height) = this.GetResolution();
        var url = $"{urlBase}&rf=LaDigue_UHD.jpg&pid=hp&w={width}&h={height}&rs=1&c=4";
        using var background = await this.DownloadBackgroundAsync(url);

        var imagePath = GetBackgroundImagePath();
        await this.SaveBackgroundAsync(title, description, copyright, imagePath, background);
        this.SetBackground(imagePath, POSITION);
    }

    private async Task<dynamic> DownloadJsonAsync()
    {
        using var client = new HttpClient();

        this.logger.LogInformation("Downloading JSON...");

        var json = await client.GetStringAsync(requestUri: "https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=en-US");
        return JsonConvert.DeserializeObject<dynamic>(json);
    }

    private async Task<(string Title, string Description, string Copyright, string Url)> GetBackgroundDataAsync()
    {
        var jsonObject = await this.DownloadJsonAsync();

        string copyrightText = jsonObject.images[0].copyright;
        string title = jsonObject.images[0].title;
        var i = copyrightText.IndexOf(" (", StringComparison.Ordinal);
        var description = copyrightText[..i];
        i += 2;
        var copyright = i < copyrightText.Length ? copyrightText[i..].TrimEnd(')') : null;

        return (title, description, copyright, $"https://www.bing.com{jsonObject.images[0].urlbase}_UHD.jpg");
    }

    private static async Task<bool> WebsiteExistsAsync(HttpClient client, string url)
    {
        try
        {
            using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            response.EnsureSuccessStatusCode();

            return true;
        }
        catch
        {
            return false;
        }
    }

    private (int, int) GetResolution()
    {
        DEVMODE devMode = default;
        devMode.dmSize = (short)Marshal.SizeOf(devMode);
        EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode);

        this.logger.LogInformation("Resolution is {Width}x{Height}", devMode.dmPelsWidth, devMode.dmPelsHeight);

        var resolution = devMode.dmPelsWidth > DEFAULT_WIDTH || devMode.dmPelsHeight > DEFAULT_HEIGHT
            ? (MAX_WIDTH, MAX_HEIGHT)
            : (DEFAULT_WIDTH, DEFAULT_HEIGHT);

        this.logger.LogInformation("Using {Width}x{Height}", resolution.Item1,resolution.Item2);

        return resolution;
    }

    private static void SetProxy()
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (string.IsNullOrWhiteSpace(PROXY)) return;

        // ReSharper disable once HeuristicUnreachableCode
        var webProxy = new WebProxy(PROXY, true)
        {
            Credentials = CredentialCache.DefaultCredentials
        };
        WebRequest.DefaultWebProxy = webProxy;
    }

    private async Task<MagickImage> DownloadBackgroundAsync(string url)
    {
        this.logger.LogInformation("Downloading background...");

        SetProxy();

        using var client = new HttpClient();

        if (!await WebsiteExistsAsync(client, url))
            throw new InvalidOperationException($"Background image not found ({url})");

        await using var stream = await client.GetStreamAsync(url);

        return stream == null
            ? throw new NullReferenceException("DownloadBackground: Response stream is null")
            : new MagickImage(stream);
    }

    private static string GetBackgroundImagePath()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Bing Backgrounds",
            DateTime.Now.Year.ToString());
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, DateTime.Now.ToString("M-d-yyyy") + ".jpeg");
    }

    private async Task SaveBackgroundAsync(string title, string description, string copyright, string imagePath, MagickImage background)
    {
        this.AddExifMetadata(background, title, description, copyright);

        this.logger.LogInformation("Saving background...");

        await background.WriteAsync(imagePath, MagickFormat.Jpeg);
    }

    private void AddExifMetadata(MagickImage image, string title, string description, string copyright)
    {
        this.logger.LogInformation("Writing EXIF metadata...");
        var profile = new ExifProfile();
        profile.SetValue(ExifTag.XPTitle, Encoding.Unicode.GetBytes(title));
        profile.SetValue(ExifTag.XPSubject, Encoding.Unicode.GetBytes(description));
        if (!string.IsNullOrWhiteSpace(copyright)) profile.SetValue(ExifTag.Copyright, copyright);

        image.SetProfile(profile);
    }


    private void SetBackground(string imagePath, PicturePosition style)
    {
        this.logger.LogInformation("Setting background...");

        using (var key = Registry.CurrentUser.OpenSubKey(Path.Combine("Control Panel", "Desktop"), true))
        {
            if (key == null)
                throw new NullReferenceException("Could not open registry key for writing.");
            switch (style)
            {
                case PicturePosition.Tile:
                    key.SetValue("PicturePosition", "0");
                    key.SetValue("TileWallpaper", "1");
                    break;
                case PicturePosition.Center:
                    key.SetValue("PicturePosition", "0");
                    key.SetValue("TileWallpaper", "0");
                    break;
                case PicturePosition.Stretch:
                    key.SetValue("PicturePosition", "2");
                    key.SetValue("TileWallpaper", "0");
                    break;
                case PicturePosition.Fit:
                    key.SetValue("PicturePosition", "6");
                    key.SetValue("TileWallpaper", "0");
                    break;
                case PicturePosition.Fill:
                    key.SetValue("PicturePosition", "10");
                    key.SetValue("TileWallpaper", "0");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, null);
            }
        }

        _ = SystemParametersInfo(SET_DESKTOP_BACKGROUND, 0, imagePath, UPDATE_INI_FILE | SEND_WINDOWS_INI_CHANGE);
    }

    #endregion
}
