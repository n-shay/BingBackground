namespace BingBackground;

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using Microsoft.Extensions.Logging;
using Microsoft.Win32.TaskScheduler;

public class TaskSchedulerUtil
{
    private const string SCHEDULED_TASK_NAME = "BingBackground Scheduled Task";
    private const string SCHEDULED_TASK_DESCRIPTION = "Updates the desktop wallpaper with Bing Background daily photo.";

    private const int INTERVAL_MINUTES = 60;

    public static void CreateScheduledTask(ILoggerFactory loggerFactory, string fileName, string workingDirectory)
    {
        var logger = loggerFactory.CreateLogger<TaskSchedulerUtil>();
        try
        {
            var td = TaskService.Instance.NewTask();
            td.RegistrationInfo.Description = SCHEDULED_TASK_DESCRIPTION;
            td.Triggers.Add(new DailyTrigger
            {
                StartBoundary = DateTime.Today,
                Repetition = new RepetitionPattern(TimeSpan.FromMinutes(INTERVAL_MINUTES), TimeSpan.Zero)
            });
            td.Triggers.Add(new LogonTrigger { Delay = TimeSpan.FromMinutes(1) });
            td.Actions.Add(new ExecAction(fileName, workingDirectory: workingDirectory));
            td.Principal.RunLevel = TaskRunLevel.Highest;
            logger.LogDebug("Task Definition: {XML}", td.XmlText);
            TaskService.Instance.RootFolder.RegisterTaskDefinition(SCHEDULED_TASK_NAME, td);
            logger.LogInformation("Scheduled task installed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled task installation failed: {Error}", ex.Message);
        }
    }

    public static void RemoveScheduledTask(ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<TaskSchedulerUtil>();
        try
        {
            TaskService.Instance.RootFolder.DeleteTask(SCHEDULED_TASK_NAME, false);
            logger.LogInformation("Scheduled task removed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled task removal failed: {Error}", ex.Message);
        }
    }

    public static bool TaskExists()
    {
        try
        {
            using var task = TaskService.Instance.GetTask(SCHEDULED_TASK_NAME);
            return task != null;
        }
        catch
        {
            return false;
        }
    }
}
