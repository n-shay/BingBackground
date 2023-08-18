namespace BingBackground
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    using ImageMagick;

    using Microsoft.Win32;

    using Newtonsoft.Json;

    using Serilog;

    using static NativeMethods;

    public class BingBackgroundUpdater
    {
        internal const int SetDesktopBackground = 20;
        internal const int UpdateIniFile = 1;
        internal const int SendWindowsIniChange = 2;
        private const PicturePosition Position = PicturePosition.Fill;
        private const string Proxy = null;
        private const int EnumCurrentSettings = -1;

        private const int DefaultWidth = 1920;
        private const int DefaultHeight = 1080;
        private const int MaxWidth = 3840;
        private const int MaxHeight = 2160;

        private readonly ILogger _logger;

        public BingBackgroundUpdater(ILogger logger)
        {
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            var (title, description, copyright, url) = await GetBackgroundDataAsync();

            await UpdateBackgroundAsync(title, description, copyright, url);
        }

        private async Task UpdateBackgroundAsync(string title, string description, string copyright, string urlBase)
        {
            var (width, height) = GetResolution();
            var url = $"{urlBase}&rf=LaDigue_UHD.jpg&pid=hp&w={width}&h={height}&rs=1&c=4";
            using var background = await DownloadBackgroundAsync(url);

            var imagePath = GetBackgroundImagePath();
            await SaveBackgroundAsync(title, description, copyright, imagePath, background); 
            SetBackground(imagePath, Position);
        }

        private async Task<dynamic> DownloadJsonAsync()
        {
            using var client = new HttpClient();

            _logger.Information("Downloading JSON...");

            var json = await client.GetStringAsync(requestUri: "https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=en-US");
            return JsonConvert.DeserializeObject<dynamic>(json);
        }

        private async Task<(string Title, string Description, string Copyright, string Url)> GetBackgroundDataAsync()
        {
            var jsonObject = await DownloadJsonAsync();

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
            EnumDisplaySettings(null, EnumCurrentSettings, ref devMode);

            _logger.Information($"Resolution is {devMode.dmPelsWidth}x{devMode.dmPelsHeight}.");

            var resolution  = (devMode.dmPelsWidth > DefaultWidth || devMode.dmPelsHeight > DefaultHeight)
                ? (MaxWidth, MaxHeight)
                : (DefaultWidth, DefaultHeight);

            _logger.Information($"Using {resolution.Item1}x{resolution.Item2}.");

            return resolution;
        }

        private static void SetProxy()
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (string.IsNullOrWhiteSpace(Proxy)) return;

            // ReSharper disable once HeuristicUnreachableCode
            var webProxy = new WebProxy(Proxy, true)
            {
                Credentials = CredentialCache.DefaultCredentials
            };
            WebRequest.DefaultWebProxy = webProxy;
        }

        private async Task<MagickImage> DownloadBackgroundAsync(string url)
        {
            _logger.Information("Downloading background...");

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
            AddExifMetadata(background, title, description, copyright);

            _logger.Information("Saving background...");

            await background.WriteAsync(imagePath, MagickFormat.Jpeg);
        }

        private void AddExifMetadata(IMagickImage image, string title, string description, string copyright)
        {
            _logger.Information("Writing EXIF metadata...");
            var profile = new ExifProfile();
            profile.SetValue(ExifTag.XPTitle, Encoding.Unicode.GetBytes(title));
            profile.SetValue(ExifTag.XPSubject, Encoding.Unicode.GetBytes(description));
            if (!string.IsNullOrWhiteSpace(copyright)) profile.SetValue(ExifTag.Copyright, copyright);

            image.SetProfile(profile);
        }


        private void SetBackground(string imagePath, PicturePosition style)
        {
            _logger.Information("Setting background...");

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

            _ = SystemParametersInfo(SetDesktopBackground, 0, imagePath, UpdateIniFile | SendWindowsIniChange);
        }
    }
}