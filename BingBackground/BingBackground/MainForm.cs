namespace BingBackground;

using System;
using System.IO;
using System.Windows.Forms;

using Microsoft.Extensions.Logging;

public class MainForm : Form
{
    private const string BINGBACKGROUND_EXE = "BingBackground.exe";

    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly Button btnInstall = new() { Text = "Install", AutoSize = true };
    private readonly Button btnUninstall = new() { Text = "Uninstall", AutoSize = true };
    private readonly Button btnSetNow = new() { Text = "Set Wallpaper Now", AutoSize = true };

    public MainForm(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<MainForm>();
        this.Text = "BingBackground";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.AutoSize = true;
        this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this.Padding = new Padding(12);
        this.TopMost = true;

        // Set window icon from embedded resource
        try
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("BingBackground.icon.ico");
            if (stream != null)
            {
                this.Icon = new System.Drawing.Icon(stream);
            }
        }
        catch
        {
            // Non-fatal if icon cannot be loaded; keep default icon
        }

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };

        flow.Controls.Add(this.btnInstall);
        flow.Controls.Add(this.btnUninstall);
        flow.Controls.Add(this.btnSetNow);

        this.Controls.Add(flow);

        this.Load += (_, _) => this.RefreshButtonsState();
        this.btnInstall.Click += async (_, _) => await this.OnInstallAsync();
        this.btnUninstall.Click += (_, _) => this.OnUninstall();
        this.btnSetNow.Click += async (_, _) => await this.OnSetNowAsync();
    }

    private void RefreshButtonsState()
    {
        var exists = TaskSchedulerUtil.TaskExists();
        this.btnInstall.Enabled = !exists;
        this.btnUninstall.Enabled = exists;
    }

    private async System.Threading.Tasks.Task OnInstallAsync()
    {
        try
        {
            // copy current executable to directory
            var sourceFileName = FileHelper.GetExecutingFileName();

            var dirInfo = Directory.CreateDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BingBackground"));
            if (!dirInfo.Exists)
                throw new Exception("Failed to create directory.");

            File.Copy(sourceFileName, Path.Combine(dirInfo.FullName, BINGBACKGROUND_EXE), true);

            this.logger.LogInformation("Copied files");

            TaskSchedulerUtil.CreateScheduledTask(this.loggerFactory, BINGBACKGROUND_EXE, dirInfo.FullName);
            this.RefreshButtonsState();
            await System.Threading.Tasks.Task.CompletedTask;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to install scheduled task.");
            MessageBox.Show(this, $"Failed to install scheduled task.\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnUninstall()
    {
        try
        {
            TaskSchedulerUtil.RemoveScheduledTask(this.loggerFactory);

            var dirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BingBackground");
            if (Directory.Exists(dirPath))
            {
                var executingFileName = FileHelper.GetExecutingFileName();
                var fileToUninstall = Path.Combine(dirPath, BINGBACKGROUND_EXE);

                if (fileToUninstall != executingFileName)
                {
                    Directory.Delete(dirPath, true);

                    this.logger.LogInformation("Removed files");
                }
            }

            this.RefreshButtonsState();
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to remove scheduled task.");
            MessageBox.Show(this, $"Failed to remove scheduled task.\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async System.Threading.Tasks.Task OnSetNowAsync()
    {
        try
        {
            var exit = await RunOnceHelper.UpdateBackground(this.loggerFactory);
            if (exit != 0)
            {
                MessageBox.Show(this, "Background update failed. Check logs for details.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(this, "Background updated!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Background update failed.\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
