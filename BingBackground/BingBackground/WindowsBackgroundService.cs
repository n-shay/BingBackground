namespace BingBackground;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal class WindowsBackgroundService : BackgroundService
{
    private readonly BingBackgroundUpdater updater;
    private readonly ILogger<WindowsBackgroundService> logger;

    public WindowsBackgroundService(BingBackgroundUpdater updater, ILogger<WindowsBackgroundService> logger) =>
        (this.updater, this.logger) = (updater, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await this.updater.ExecuteAsync();

                this.logger.LogInformation("Updated wallpaper successfully");

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
        catch (TaskCanceledException)
        {
            // When the stopping token is canceled, for example, a call made from services.msc,
            // we shouldn't exit with a non-zero exit code. In other words, this is expected...
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{Message}", ex.Message);

            // Terminates this process and returns an exit code to the operating system.
            // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
            // performs one of two scenarios:
            // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
            // 2. When set to "StopHost": will cleanly stop the host, and log errors.
            //
            // In order for the Windows Service Management system to leverage configured
            // recovery options, we need to terminate the process with a non-zero exit code.
            Environment.Exit(1);
        }
    }
}
