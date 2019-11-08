namespace BingBackground
{
    using System;
    using System.IO;
    using System.Drawing;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Win32;

    using Newtonsoft.Json;

    using Serilog;

    public class BingBackgroundUpdater : IDisposable
	{
		internal const int SET_DESKTOP_BACKGROUND = 20;
		internal const int UPDATE_INI_FILE = 1;
		internal const int SEND_WINDOWS_INI_CHANGE = 2;
		internal const int MILLISECONDS_IN_SECOND = 1000;
        private const int TIMEOUT_SECONDS = 45;
        private const PicturePosition POSITION = PicturePosition.Fill;
        private const string PROXY = null; 
        private const int ENUM_CURRENT_SETTINGS = -1;

        private static readonly ILogger logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "BingBackground.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

		private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

	    public void Execute()
	    {
	        try
	        {
	            var succeed = UpdateBackgroundAsync(GetBackgroundUrlBase())
	                .Wait(TIMEOUT_SECONDS * MILLISECONDS_IN_SECOND, this.cancellationTokenSource.Token);

	            if (succeed) logger.Information("Update Completed!");
	            else
                    logger.Warning("Operation timed-out!");
	        }
	        catch (OperationCanceledException)
	        {
                logger.Information("Operation canceled!");
	        }
	        catch (Exception ex)
	        {
                logger.Error($"Failed updating background: {ex.Message}.", ex);
	        }
	    }

		private static async Task UpdateBackgroundAsync(string urlBase)
		{
			using (var background = await DownloadBackgroundAsync(urlBase + await GetResolutionExtensionAsync(urlBase)))
			{
				await SaveBackgroundAsync(background);
				SetBackground(POSITION);
			}
		}

        private static dynamic DownloadJson()
        {
            using (var webClient = new WebClient())
            {
                logger.Information("Downloading JSON...");
                var jsonString =
                    webClient.DownloadString("https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=en-US");

                return JsonConvert.DeserializeObject<dynamic>(jsonString);
            }
        }

		private static string GetBackgroundUrlBase()
		{
			var jsonObject = DownloadJson();
			return "https://www.bing.com" + jsonObject.images[0].urlbase;
		}

		//private static string GetBackgroundTitle()
		//{
		//	var jsonObject = DownloadJson();
		//	string copyrightText = jsonObject.images[0].copyright;
		//	return copyrightText.Substring(0, copyrightText.IndexOf(" (", StringComparison.Ordinal));
		//}

		private static async Task<bool> WebsiteExistsAsync(string url)
		{
			try
			{
				var request = WebRequest.CreateHttp(url);
				request.Timeout = TIMEOUT_SECONDS * MILLISECONDS_IN_SECOND;
				request.Method = "HEAD";
                using (var response = (HttpWebResponse) await request.GetResponseAsync())
                    return response.StatusCode == HttpStatusCode.OK;
            }
			catch
			{
				return false;
			}
		}

		private static async Task<string> GetResolutionExtensionAsync(string url)
        {
            DEVMODE devMode = default;
            devMode.dmSize = (short) Marshal.SizeOf(devMode);
			NativeMethods.EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode);
			var widthByHeight = devMode.dmPelsWidth + "x" + devMode.dmPelsHeight;
			var potentialExtension = $"_{widthByHeight}.jpg";
			if (await WebsiteExistsAsync(url + potentialExtension))
			{
                logger.Information($"Background for {widthByHeight} found.");
				return potentialExtension;
			}

            logger.Information($"No background for {widthByHeight} was found. Using 1920x1080 instead.");
			return "_1920x1080.jpg";
		}

		private static void SetProxy()
		{
			if (string.IsNullOrWhiteSpace(PROXY)) return;

			var webProxy = new WebProxy(PROXY, true)
			{
				Credentials = CredentialCache.DefaultCredentials
			};
			WebRequest.DefaultWebProxy = webProxy;
		}

		private static async Task<Image> DownloadBackgroundAsync(string url)
		{
            logger.Information("Downloading background...");
			SetProxy();
			var request = WebRequest.CreateHttp(url);
			request.Timeout = TIMEOUT_SECONDS * MILLISECONDS_IN_SECOND;

            using (var response = await request.GetResponseAsync())
                using (var stream = response.GetResponseStream())
                {
                    if (stream == null)
                        throw new NullReferenceException("DownloadBackground: Response stream is null");

                    return Image.FromStream(stream);
                }
        }

		private static string GetBackgroundImagePath()
		{
		    var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Bing Backgrounds",
                DateTime.Now.Year.ToString());
			Directory.CreateDirectory(directory);
			return Path.Combine(directory, DateTime.Now.ToString("M-d-yyyy") + ".bmp");
		}

		private static async Task SaveBackgroundAsync(Image background)
		{
            logger.Information("Saving background...");
			await Task.Run(() => background.Save(GetBackgroundImagePath(), System.Drawing.Imaging.ImageFormat.Bmp));
		}
        
		private static void SetBackground(PicturePosition style)
		{
            logger.Information("Setting background...");
			using (var key = Registry.CurrentUser.OpenSubKey(Path.Combine("Control Panel", "Desktop"), true))
			{
				if(key == null)
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

			NativeMethods.SystemParametersInfo(SET_DESKTOP_BACKGROUND, 0, GetBackgroundImagePath(),
				UPDATE_INI_FILE | SEND_WINDOWS_INI_CHANGE);
		}

	    public void Dispose()
	    {
	        this.cancellationTokenSource?.Dispose();
	    }
	}
}