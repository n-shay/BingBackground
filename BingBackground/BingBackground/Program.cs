using System;
using System.Reflection;
using Microsoft.Win32.TaskScheduler;

namespace BingBackground
{
    public static class Program
    {
        private const string ScheduledTaskName = "BingBackground Scheduled Task";
        private const string ScheduledTaskDescription = "Updates the desktop wallpaper with Bing Background daily photo.";

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

            td.RegistrationInfo.Description = ScheduledTaskDescription;
            
            // Create a trigger that will fire the task at this time every other day
            td.Triggers.Add(new DailyTrigger
            {
                StartBoundary = DateTime.Today,
                Repetition = new RepetitionPattern(TimeSpan.FromMinutes(Properties.Settings.Default.IntervalMinutes),
                    TimeSpan.Zero)
            });
            td.Triggers.Add(new LogonTrigger
            {
                Delay = TimeSpan.FromMinutes(1)
            });

            // Create an action that will launch Notepad whenever the trigger fires
            td.Actions.Add(new ExecAction(Assembly.GetExecutingAssembly().Location));

            // Register the task in the root folder
            TaskService.Instance.RootFolder.RegisterTaskDefinition(ScheduledTaskName, td);
        }

        private static void RemoveScheduledTask()
        {
            TaskService.Instance.RootFolder.DeleteTask(ScheduledTaskName, false);
        }
    }
}