namespace BingBackground;

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

public class RunOnceHelper
{
    private const int TIMEOUT_SECONDS = 45;

    public static async Task<int> UpdateBackground(ILoggerFactory loggerFactory)
    {
        var updater = new BingBackgroundUpdater(loggerFactory.CreateLogger<BingBackgroundUpdater>());
        var logger = loggerFactory.CreateLogger<RunOnceHelper>();
        try
        {
            await updater.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(TIMEOUT_SECONDS));
            logger.LogInformation("Background updated!");
        }
        catch (TimeoutException)
        {
            logger.LogWarning("Operation timed-out!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background update failed: {Error}", ex.Message);
            return 1;
        }
        return 0;
    }
}
