namespace BingBackground
{
    using System;
    using System.IO;
    using System.Drawing;
    using System.Net;
    using System.Net.Http;
    using Newtonsoft.Json;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    using Microsoft.Win32;

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

        private readonly ILogger _logger;

        public BingBackgroundUpdater(ILogger logger)
        {
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            var (_, url) = await GetBackgroundDataAsync();

            await UpdateBackgroundAsync(url);
        }

        private async Task UpdateBackgroundAsync(string urlBase)
        {
            using var background = await DownloadBackgroundAsync(urlBase + await GetResolutionExtensionAsync(urlBase));

            await SaveBackgroundAsync(background);
            SetBackground(Position);
        }

        private async Task<dynamic> DownloadJsonAsync()
        {
            using var client = new HttpClient();

            _logger.Information("Downloading JSON...");

            var json = await client.GetStringAsync(requestUri: "https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=en-US");
            return JsonConvert.DeserializeObject<dynamic>(json);
        }

        private async Task<(string Title, string Url)> GetBackgroundDataAsync()
        {
            var jsonObject = await DownloadJsonAsync();

            string copyrightText = jsonObject.images[0].copyright;
            var title = copyrightText.Substring(0, copyrightText.IndexOf(" (", StringComparison.Ordinal));

            return (title, "https://www.bing.com" + jsonObject.images[0].urlbase);
        }

        private static async Task<bool> WebsiteExistsAsync(string url)
        {
            try
            {
                using var client = new HttpClient();

                using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                response.EnsureSuccessStatusCode();

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> GetResolutionExtensionAsync(string url)
        {
            DEVMODE devMode = default;
            devMode.dmSize = (short)Marshal.SizeOf(devMode);
            EnumDisplaySettings(null, EnumCurrentSettings, ref devMode);
            var widthByHeight = devMode.dmPelsWidth + "x" + devMode.dmPelsHeight;
            var potentialExtension = $"_{widthByHeight}.jpg";
            if (await WebsiteExistsAsync(url + potentialExtension))
            {
                _logger.Information($"Background for {widthByHeight} found.");
                return potentialExtension;
            }

            _logger.Information($"No background for {widthByHeight} was found. Using 1920x1080 instead.");
            return "_1920x1080.jpg";
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

        private async Task<Image> DownloadBackgroundAsync(string url)
        {
            _logger.Information("Downloading background...");

            SetProxy();

            using var client = new HttpClient();
            await using var stream = await client.GetStreamAsync(url);

            if (stream == null)
                throw new NullReferenceException("DownloadBackground: Response stream is null");

            return Image.FromStream(stream);
        }

        private static string GetBackgroundImagePath()
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Bing Backgrounds",
                DateTime.Now.Year.ToString());
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, DateTime.Now.ToString("M-d-yyyy") + ".bmp");
        }

        private async Task SaveBackgroundAsync(Image background)
        {
            _logger.Information("Saving background...");
            await Task.Run(() => background.Save(GetBackgroundImagePath(), System.Drawing.Imaging.ImageFormat.Bmp));
        }

        private void SetBackground(PicturePosition style)
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

            SystemParametersInfo(SetDesktopBackground, 0, GetBackgroundImagePath(),
                UpdateIniFile | SendWindowsIniChange);
        }
    }
}