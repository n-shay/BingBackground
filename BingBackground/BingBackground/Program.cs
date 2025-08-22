using System;

using BingBackground;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "BingBackground Updater Service";
    })
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<BingBackgroundUpdater>();
        services.AddHostedService<WindowsBackgroundService>();

        // See: https://github.com/dotnet/runtime/issues/47303
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(context.HostingEnvironment.IsDevelopment() ? LogLevel.Debug : LogLevel.Warning);

            builder.AddFilter(LogFilter("Microsoft", LogLevel.Information))
                .AddFilter(LogFilter("Microsoft.Hosting.Lifetime", LogLevel.Information))
                .AddFilter(LogFilter("BingBackground", LogLevel.Information));

            builder.AddEventLog(config =>
            {
                config.SourceName = "BingBackground";
            });

            return;

            Func<string, LogLevel, bool> LogFilter(string categoryPrefix, LogLevel minimumLogLevel) =>
                (category, logLevel) => category.StartsWith(categoryPrefix) && logLevel >= minimumLogLevel;
        });
    });

var host = builder.Build();
await host.RunAsync();
