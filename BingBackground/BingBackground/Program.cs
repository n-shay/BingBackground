namespace BingBackground;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

public class Program
{

    [STAThread]
    public static async Task Main(string[] args)
    {
        var silent = args.Contains("--silent");
        var loggerFactory = LoggerFactory.Create(builder => SetupLogging(builder, silent));

        if (args.Contains("--uninstall"))
        {
            TaskSchedulerUtil.RemoveScheduledTask(loggerFactory);
            Environment.Exit(0);
        }
        else if (args.Contains("--install"))
        {
            var fileName = Environment.ProcessPath;

            TaskSchedulerUtil.CreateScheduledTask(loggerFactory, Path.GetFileName(fileName), AppContext.BaseDirectory);
            Environment.Exit(0);
        }
        else if (silent)
        {
            var exit = await RunOnceHelper.UpdateBackground(loggerFactory);
            Environment.Exit(exit);
        }
        else
        {
            // If no arguments provided, open a small UI window.
            try
            {
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                System.Windows.Forms.Application.Run(new MainForm(loggerFactory));
            }
            catch (Exception ex)
            {
                var logger = loggerFactory.CreateLogger<Program>();
                logger.LogError(ex, "Unhandled exception: {Error}", ex.Message);
                Environment.Exit(1);
            }
        }
    }

    private static void SetupLogging(ILoggingBuilder builder, bool silent)
    {
#if DEBUG
        const LogLevel MINIMAL_LEVEL = LogLevel.Debug;
#else
        const LogLevel MINIMAL_LEVEL = LogLevel.Information;
#endif
        builder.SetMinimumLevel(MINIMAL_LEVEL);
        builder.AddFilter(LogFilter("Microsoft", LogLevel.Information))
               .AddFilter(LogFilter("BingBackground", LogLevel.Information));
        if (silent)
        {
            builder.AddEventLog(config => { config.SourceName = "BingBackground"; });
        }
        else
        {
            builder.AddConsole();
        }

        return;

        static Func<string, LogLevel, bool> LogFilter(string categoryPrefix, LogLevel minimumLogLevel) =>
            (category, logLevel) => category.StartsWith(categoryPrefix) && logLevel >= minimumLogLevel;
    }
}
