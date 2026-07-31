using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Ignyos.LanPortal.Host;

public sealed class MainForm : Form
{
    private const string ApiListenUrl = "http://0.0.0.0:5212";
    private const string WebListenUrl = "http://0.0.0.0:80";
    private const string SetupUrl = "http://localhost:5212/local/setup";
    private const string AdminUrl = "http://localhost:5212/local/admin";
    private const string AccessHistoryUrl = "http://localhost:5212/local/access-history";
    private const string UpdateStatusUrl = "http://localhost:5212/api/local/update/status";
    private const string UpdateCheckNowUrl = "http://localhost:5212/api/local/update/check-now";
    private const string AppTitlePrefix = "Ignyos LAN Portal";

    private readonly WebView2 browser;
    private readonly string appVersion;
    private readonly ToolStripMenuItem checkForUpdatesMenuItem;
    private readonly ToolStripStatusLabel updateStateLabel;
    private readonly ToolStripStatusLabel updateActionLabel;
    private readonly System.Windows.Forms.Timer updatePollTimer;
    private bool isUpdateCheckInProgress;
    private bool isTestChannel;
    private string? availableUpdateUrl;

    public MainForm()
    {
        appVersion = GetAppVersion();
        isTestChannel = appVersion.Contains("-test.", StringComparison.OrdinalIgnoreCase);
        Text = $"{AppTitlePrefix} v{appVersion}";
        Width = 1280;
        Height = 860;
        MinimumSize = new Size(900, 640);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;

        var menuStrip = BuildMenuStrip(out checkForUpdatesMenuItem);
        MainMenuStrip = menuStrip;

        var statusStrip = BuildStatusStrip(appVersion, out updateStateLabel, out updateActionLabel);
        updatePollTimer = BuildUpdatePollTimer();

        browser = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.White,
            CreationProperties = new CoreWebView2CreationProperties
            {
                UserDataFolder = GetWebViewUserDataFolder()
            }
        };

        Controls.Add(browser);
        Controls.Add(menuStrip);
        Controls.Add(statusStrip);

        Shown += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            StartPortalProcess(GetApiExecutablePath(), "Ignyos.LanPortal.Api", new[] { "--urls", ApiListenUrl });
            StartPortalProcess(GetWebExecutablePath(), "Ignyos.LanPortal.Web", new[] { "--urls", WebListenUrl });

            await WaitForApiAsync();

            await browser.EnsureCoreWebView2Async();

            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            browser.CoreWebView2.Settings.IsStatusBarEnabled = false;

            var initialUrl = await ResolveInitialUrlAsync();
            NavigateTo(initialUrl);
            await CheckForUpdatesAsync(isManualCheck: false, forceRefresh: false);
            updatePollTimer.Start();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowFatalError(
                "Microsoft Edge WebView2 Runtime is not installed.",
                "Install the WebView2 Runtime, then launch Ignyos LAN Portal again.");
        }
        catch (Exception ex)
        {
            ShowFatalError("Unable to launch Ignyos LAN Portal host UI.", ex.Message);
        }
    }

    private void NavigateTo(string url)
    {
        if (browser.CoreWebView2 is null)
        {
            return;
        }

        browser.CoreWebView2.Navigate(url);
    }

    private static string GetApiExecutablePath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "api", "Ignyos.LanPortal.Api.exe"));
    }

    private static string GetWebExecutablePath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "web", "Ignyos.LanPortal.Web.exe"));
    }

    private static string GetWebViewUserDataFolder()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userDataFolder = Path.Combine(localAppData, "Ignyos", "LanPortalDev", "WebView2");
        Directory.CreateDirectory(userDataFolder);
        return userDataFolder;
    }

    private static void StartPortalProcess(string executablePath, string processName, IReadOnlyList<string> arguments)
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException($"Missing executable: {executablePath}");
        }

        if (Process.GetProcessesByName(processName).Length > 0)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = string.Join(' ', arguments),
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!
        });
    }

    private static async Task WaitForApiAsync()
    {
        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await http.GetAsync(SetupUrl);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Portal services did not become ready within 60 seconds.");
    }

    private MenuStrip BuildMenuStrip(out ToolStripMenuItem checkUpdatesMenuItem)
    {
        var menuStrip = new MenuStrip
        {
            Dock = DockStyle.Top
        };

        var fileMenu = new ToolStripMenuItem("File");

        var setupMenuItem = new ToolStripMenuItem("Setup");
        setupMenuItem.Click += (_, _) => NavigateTo(SetupUrl);

        var adminMenuItem = new ToolStripMenuItem("Admin");
        adminMenuItem.Click += (_, _) => NavigateTo(AdminUrl);

        var accessHistoryMenuItem = new ToolStripMenuItem("Access History");
        accessHistoryMenuItem.Click += (_, _) => NavigateTo(AccessHistoryUrl);

        var refreshMenuItem = new ToolStripMenuItem("Refresh");
        refreshMenuItem.Click += (_, _) => browser.CoreWebView2?.Reload();

        checkUpdatesMenuItem = new ToolStripMenuItem("Check For Updates");
        checkUpdatesMenuItem.Click += async (_, _) => await CheckForUpdatesAsync(isManualCheck: true, forceRefresh: true);

        fileMenu.DropDownItems.Add(setupMenuItem);
        fileMenu.DropDownItems.Add(adminMenuItem);
        fileMenu.DropDownItems.Add(accessHistoryMenuItem);
        fileMenu.DropDownItems.Add(refreshMenuItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(checkUpdatesMenuItem);

        menuStrip.Items.Add(fileMenu);
        return menuStrip;
    }

    private static StatusStrip BuildStatusStrip(string currentVersion, out ToolStripStatusLabel stateLabel, out ToolStripStatusLabel actionLabel)
    {
        var statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            SizingGrip = false
        };

        var versionLabel = new ToolStripStatusLabel
        {
            Text = $"Version {currentVersion}",
            ForeColor = Color.DimGray
        };

        stateLabel = new ToolStripStatusLabel
        {
            Text = "Checking for updates...",
            Spring = true,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.DimGray
        };

        actionLabel = new ToolStripStatusLabel
        {
            IsLink = true,
            Visible = false,
            ForeColor = Color.FromArgb(20, 92, 148)
        };

        statusStrip.Items.Add(versionLabel);
        statusStrip.Items.Add(stateLabel);
        statusStrip.Items.Add(actionLabel);

        return statusStrip;
    }

    private System.Windows.Forms.Timer BuildUpdatePollTimer()
    {
        var timer = new System.Windows.Forms.Timer
        {
            Interval = (int)TimeSpan.FromHours(1).TotalMilliseconds
        };

        timer.Tick += async (_, _) => await CheckForUpdatesAsync(isManualCheck: false, forceRefresh: false);
        return timer;
    }

    private static string GetAppVersion()
    {
        var informationalVersion = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return "unknown";
        }

        var version = informationalVersion.Split('+', 2)[0].Trim();
        return string.IsNullOrWhiteSpace(version) ? "unknown" : version;
    }

    private static async Task<string> ResolveInitialUrlAsync()
    {
        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        try
        {
            var setupStatus = await http.GetFromJsonAsync<SetupStatus>("http://localhost:5212/api/local/setup/status");
            if (setupStatus?.IsSetupComplete == true)
            {
                return AdminUrl;
            }

            return SetupUrl;
        }
        catch
        {
            return AdminUrl;
        }
    }

    private async Task CheckForUpdatesAsync(bool isManualCheck, bool forceRefresh)
    {
        if (isUpdateCheckInProgress)
        {
            return;
        }

        isUpdateCheckInProgress = true;
        checkForUpdatesMenuItem.Enabled = false;
        updateStateLabel.Text = "Checking for updates...";

        try
        {
            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };

            UpdateStatusResponse? updateStatus;
            if (forceRefresh)
            {
                var response = await http.PostAsJsonAsync(UpdateCheckNowUrl, new UpdateCheckNowRequest(appVersion));
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Update check endpoint returned HTTP {(int)response.StatusCode}.");
                }

                updateStatus = await response.Content.ReadFromJsonAsync<UpdateStatusResponse>();
            }
            else
            {
                var encodedVersion = Uri.EscapeDataString(appVersion);
                updateStatus = await http.GetFromJsonAsync<UpdateStatusResponse>($"{UpdateStatusUrl}?currentVersion={encodedVersion}");
            }

            if (updateStatus is null)
            {
                throw new InvalidOperationException("Update status response was empty.");
            }

            ApplyUpdateStatus(updateStatus, isManualCheck);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[UpdateCheck] Failed: {ex.Message}");

            if (isTestChannel)
            {
                updateStateLabel.Text = "Update check unavailable (test channel).";
                updateStateLabel.ForeColor = Color.DarkGoldenrod;
            }
            else
            {
                updateStateLabel.Text = "";
                updateStateLabel.ForeColor = Color.DimGray;
            }

            if (isManualCheck && isTestChannel)
            {
                MessageBox.Show(
                    "Update check is currently unavailable. See logs for details.",
                    AppTitlePrefix,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            updateActionLabel.Visible = false;
            availableUpdateUrl = null;
        }
        finally
        {
            isUpdateCheckInProgress = false;
            checkForUpdatesMenuItem.Enabled = true;
        }
    }

    private void ApplyUpdateStatus(UpdateStatusResponse updateStatus, bool isManualCheck)
    {
        isTestChannel = updateStatus.IsTestChannel;

        if (!string.IsNullOrWhiteSpace(updateStatus.Error))
        {
            Trace.WriteLine($"[UpdateCheck] {updateStatus.Error}");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.Error) && updateStatus.IsTestChannel)
        {
            updateStateLabel.Text = "Update check unavailable (test channel).";
            updateStateLabel.ForeColor = Color.DarkGoldenrod;
        }
        else if (updateStatus.RequiredUpdate)
        {
            updateStateLabel.Text = $"Update required: {updateStatus.LatestVersion}";
            updateStateLabel.ForeColor = Color.DarkOrange;
        }
        else if (updateStatus.UpdateAvailable)
        {
            updateStateLabel.Text = $"New version available: {updateStatus.LatestVersion}";
            updateStateLabel.ForeColor = Color.FromArgb(20, 92, 148);
        }
        else
        {
            updateStateLabel.Text = "Up to date";
            updateStateLabel.ForeColor = Color.DimGray;
        }

        if (updateStatus.UpdateAvailable && !string.IsNullOrWhiteSpace(updateStatus.DownloadUrl))
        {
            availableUpdateUrl = updateStatus.DownloadUrl;
            updateActionLabel.Text = updateStatus.RequiredUpdate ? "Update Required" : "New Version Available";
            updateActionLabel.Visible = true;
            updateActionLabel.Click -= OpenAvailableUpdate;
            updateActionLabel.Click += OpenAvailableUpdate;
        }
        else
        {
            availableUpdateUrl = null;
            updateActionLabel.Visible = false;
            updateActionLabel.Click -= OpenAvailableUpdate;
        }

        if (isManualCheck && !updateStatus.UpdateAvailable && string.IsNullOrWhiteSpace(updateStatus.Error))
        {
            updateStateLabel.Text = "No newer version is currently available.";
            updateStateLabel.ForeColor = Color.DimGray;
        }
    }

    private void OpenAvailableUpdate(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(availableUpdateUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = availableUpdateUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[UpdateCheck] Could not open update URL: {ex.Message}");
            if (isTestChannel)
            {
                MessageBox.Show(
                    "Could not open the update download link. See logs for details.",
                    AppTitlePrefix,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
    }

    private void ShowFatalError(string title, string detail)
    {
        MessageBox.Show($"{title}\n\n{detail}", AppTitlePrefix, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private sealed record SetupStatus([property: JsonPropertyName("isSetupComplete")] bool IsSetupComplete);

    private sealed record UpdateCheckNowRequest(string CurrentVersion);

    private sealed record UpdateStatusResponse(
        [property: JsonPropertyName("currentVersion")] string CurrentVersion,
        [property: JsonPropertyName("latestVersion")] string? LatestVersion,
        [property: JsonPropertyName("minSupportedVersion")] string? MinSupportedVersion,
        [property: JsonPropertyName("downloadUrl")] string? DownloadUrl,
        [property: JsonPropertyName("updateAvailable")] bool UpdateAvailable,
        [property: JsonPropertyName("requiredUpdate")] bool RequiredUpdate,
        [property: JsonPropertyName("channel")] string Channel,
        [property: JsonPropertyName("isTestChannel")] bool IsTestChannel,
        [property: JsonPropertyName("manifestUrl")] string ManifestUrl,
        [property: JsonPropertyName("checkedAtUtc")] DateTimeOffset CheckedAtUtc,
        [property: JsonPropertyName("isStale")] bool IsStale,
        [property: JsonPropertyName("error")] string? Error);
}
