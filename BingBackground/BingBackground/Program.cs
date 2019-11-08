[assembly: System.Resources.NeutralResourcesLanguage("en")]

namespace BingBackground
{
    using System;
    using System.Reflection;

    using Microsoft.Win32.TaskScheduler;

    public class Program
    {
        private const string SCHEDULED_TASK_NAME = "BingBackground Scheduled Task";
        private const string SCHEDULED_TASK_DESCRIPTION = "Updates the desktop wallpaper with Bing Background daily photo.";

        private const int INTERVAL_MINUTES = 60;

        public static void Main(string[] args)
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

            using (var updater = new BingBackgroundUpdater())
            {
                updater.Execute();
            }

            Environment.Exit(0);
        }

        private static void CreateScheduledTask()
        {
            // Create a new task definition and assign properties
            var td = TaskService.Instance.NewTask();

            td.RegistrationInfo.Description = SCHEDULED_TASK_DESCRIPTION;

            // Create a trigger that will fire the task at this time every other day
            td.Triggers.Add(new DailyTrigger
            {
                StartBoundary = DateTime.Today,
                Repetition = new RepetitionPattern(TimeSpan.FromMinutes(INTERVAL_MINUTES),
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
            TaskService.Instance.RootFolder.RegisterTaskDefinition(SCHEDULED_TASK_NAME, td);
        }

        private static void RemoveScheduledTask()
        {
            TaskService.Instance.RootFolder.DeleteTask(SCHEDULED_TASK_NAME, false);
        }
    }
}
