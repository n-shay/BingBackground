namespace BingBackground
{
    using System;
    using System.IO;
    using System.Reflection;
    using Microsoft.Win32.TaskScheduler;
    using Serilog;

    using Task = System.Threading.Tasks.Task;

    public class Program
    {
        private const int TimeoutSeconds = 45;
        private const string ScheduledTaskName = "BingBackground Scheduled Task";
        private const string ScheduledTaskDescription = "Updates the desktop wallpaper with Bing Background daily photo.";

        private const int IntervalMinutes = 60;

        private static readonly ILogger Logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BingBackground.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        public static async Task Main(string[] args)
        {
            if (args?.Length == 1)
            {
                switch (args[0].TrimStart('/', '-').ToLower())
                {
                    case "u":
                    case "uninstall":
                        RemoveScheduledTask();
                        Environment.Exit(0);
                        return;
                    case "i":
                    case "install":
                        CreateScheduledTask();
                        Environment.Exit(0);
                        return;
                }
            }

            try
            {
                var updater = new BingBackgroundUpdater(Logger);

                await updater.ExecuteAsync()
                    .WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));


                Logger.Information("Update Completed!");

            }
            catch (TimeoutException)
            {
                Logger.Warning("Operation timed-out!");
            }

            catch (Exception ex)
            {
                Logger.Error($"Failed updating background: {ex.Message}.", ex);
            }

            Environment.Exit(0);
        }

        private static void CreateScheduledTask()
        {
            // Create a new task definition and assign properties
            var td = TaskService.Instance.NewTask();

            td.RegistrationInfo.Description = ScheduledTaskDescription;

            // Create a trigger that will fire the task at this time every other day
            td.Triggers.Add(new DailyTrigger
            {
                StartBoundary = DateTime.Today,
                Repetition = new RepetitionPattern(TimeSpan.FromMinutes(IntervalMinutes),
                    TimeSpan.Zero)
            });
            td.Triggers.Add(new LogonTrigger
            {
                Delay = TimeSpan.FromMinutes(1)
            });

            // Create an action that will launch Notepad whenever the trigger fires
            td.Actions.Add(new ExecAction(Assembly.GetExecutingAssembly().Location));

            td.Principal.RunLevel = TaskRunLevel.Highest;

            // Register the task in the root folder
            TaskService.Instance.RootFolder.RegisterTaskDefinition(ScheduledTaskName, td);
        }

        private static void RemoveScheduledTask()
        {
            TaskService.Instance.RootFolder.DeleteTask(ScheduledTaskName, false);
        }
    }
}
