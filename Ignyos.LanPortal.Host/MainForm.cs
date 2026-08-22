using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Ignyos.LanPortal.Host;

public sealed class MainForm : Form
{
    private static readonly Color HostBackground = Color.FromArgb(245, 239, 227);
    private static readonly Color HostSurface = Color.FromArgb(255, 249, 237);
    private static readonly Color HostSurfaceAlt = Color.FromArgb(249, 242, 226);
    private static readonly Color HostInk = Color.FromArgb(17, 34, 45);
    private static readonly Color HostMuted = Color.FromArgb(66, 90, 102);
    private static readonly Color HostLine = Color.FromArgb(216, 200, 170);
    private static readonly Color HostAccent = Color.FromArgb(10, 143, 143);
    private static readonly Color HostAccentDark = Color.FromArgb(11, 112, 112);
    private static readonly Color HostAccentSoft = Color.FromArgb(143, 217, 217);
    private static readonly Color HostAccent2 = Color.FromArgb(240, 138, 36);
    private static readonly Color HostDanger = Color.FromArgb(163, 31, 31);

    private const string ApiListenUrl = "http://0.0.0.0:5212";
    private const string WebListenUrlProd = "http://0.0.0.0:80";
    private const string WebListenUrlDev = "http://0.0.0.0:5014";
    private const string SetupUrl = "http://localhost:5212/local/setup";
    private const string AdvancedUrl = "http://localhost:5212/local/advanced";
    private const string AdminUrl = "http://localhost:5212/local/admin";
    private const string AccessHistoryUrl = "http://localhost:5212/local/access-history";
    private const string UpdateStatusUrl = "http://localhost:5212/api/local/update/status";
    private const string UpdateCheckNowUrl = "http://localhost:5212/api/local/update/check-now";
    private const string AppTitlePrefix = "Ignyos LAN Portal";
    private const bool EnableUpdateOrchestrationForTestChannel = true;
    private const string FailureCodeMissingExpectedSha = "MISSING_EXPECTED_SHA";
    private const string FailureCodeDownloadFailed = "DOWNLOAD_FAILED";
    private const string FailureCodeChecksumMismatch = "CHECKSUM_MISMATCH";
    private const string FailureCodeOrchestrationFailed = "ORCHESTRATION_FAILED";
    private const string FailureCodeInstallerLaunchFailed = "INSTALLER_LAUNCH_FAILED";
    private const string FailureCodeUnknown = "UNKNOWN";
    private const string FaultNone = "NONE";
    private const string FaultDownload = "DOWNLOAD";
    private const string FaultChecksum = "CHECKSUM";
    private const string FaultOrchestration = "ORCHESTRATION";
    private const string FaultLaunch = "LAUNCH";
    private const string HostMenuDismissMessage = "HOST_CLOSE_OPEN_MENUS";

    private readonly WebView2 browser;
    private readonly string appVersionFull;
    private readonly string appVersionDisplay;
    private readonly bool isDevInstaller;
    private readonly ToolStripMenuItem checkForUpdatesMenuItem;
    private readonly ToolStripStatusLabel updateStateLabel;
    private readonly ToolStripStatusLabel updateActionLabel;
    private readonly System.Windows.Forms.Timer updatePollTimer;
    private bool isUpdateCheckInProgress;
    private bool isTestChannel;
    private string? availableUpdateUrl;
    private string? availableUpdateSha256;
    private string? availableUpdateVersion;
    private Process? managedApiProcess;
    private Process? managedWebProcess;

    private static string WebListenUrl => IsDevelopmentEnvironment() ? WebListenUrlDev : WebListenUrlProd;

    public MainForm()
    {
        appVersionFull = GetAppVersion();
        isDevInstaller = IsDeveloperVersionByFourthNode(appVersionFull);
        appVersionDisplay = GetDisplayVersionForInstaller(appVersionFull, isDevInstaller);
        isTestChannel = isDevInstaller;
        Text = isDevInstaller
            ? $"{AppTitlePrefix} (Dev) v{appVersionDisplay}"
            : $"{AppTitlePrefix} v{appVersionDisplay}";
        BackColor = HostBackground;
        ForeColor = HostInk;
        Width = 1280;
        Height = 860;
        MinimumSize = new Size(900, 640);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;

        var menuStrip = BuildMenuStrip(out checkForUpdatesMenuItem);
        MainMenuStrip = menuStrip;

        var statusStrip = BuildStatusStrip(appVersionDisplay, out updateStateLabel, out updateActionLabel);
        updatePollTimer = BuildUpdatePollTimer();

        browser = new WebView2
        {
            Dock = DockStyle.Fill,
            BackColor = HostBackground,
            DefaultBackgroundColor = HostBackground,
            CreationProperties = new CoreWebView2CreationProperties
            {
                UserDataFolder = GetWebViewUserDataFolder()
            }
        };

        Controls.Add(browser);
        Controls.Add(menuStrip);
        Controls.Add(statusStrip);

        Deactivate += (_, _) => CloseOpenMenus();
        browser.GotFocus += (_, _) => CloseOpenMenus();

        Shown += async (_, _) => await InitializeAsync();
    }

    private void CloseOpenMenus()
    {
        if (MainMenuStrip is null)
        {
            return;
        }

        foreach (ToolStripItem item in MainMenuStrip.Items)
        {
            if (item is ToolStripMenuItem menuItem && menuItem.DropDown.Visible)
            {
                menuItem.HideDropDown();
            }
        }
    }

    private async Task ConfigureWebViewMenuDismissBridgeAsync()
    {
        if (browser.CoreWebView2 is null)
        {
            return;
        }

        browser.CoreWebView2.WebMessageReceived -= OnBrowserWebMessageReceived;
        browser.CoreWebView2.WebMessageReceived += OnBrowserWebMessageReceived;

        await browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
            """
            (() => {
                const hostMessage = 'HOST_CLOSE_OPEN_MENUS';
                const notifyHost = () => {
                    try {
                        chrome.webview.postMessage(hostMessage);
                    } catch {
                    }
                };

                window.addEventListener('pointerdown', notifyHost, true);
                window.addEventListener('mousedown', notifyHost, true);
            })();
            """);
    }

    private void OnBrowserWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string? message = null;

        try
        {
            message = e.TryGetWebMessageAsString();
        }
        catch
        {
        }

        if (!string.Equals(message, HostMenuDismissMessage, StringComparison.Ordinal))
        {
            return;
        }

        CloseOpenMenus();
    }

    private async Task InitializeAsync()
    {
        try
        {
            managedApiProcess = StartPortalProcess(GetApiExecutablePath(), "Ignyos.LanPortal.Api", new[] { "--urls", ApiListenUrl });
            managedWebProcess = StartPortalProcess(GetWebExecutablePath(), "Ignyos.LanPortal.Web", new[] { "--urls", WebListenUrl });

            await WaitForApiAsync();

            await browser.EnsureCoreWebView2Async();
            await ConfigureWebViewMenuDismissBridgeAsync();

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
        var packagedPath = Path.GetFullPath(Path.Combine(baseDir, "..", "api", "Ignyos.LanPortal.Api.exe"));
        if (File.Exists(packagedPath))
        {
            return packagedPath;
        }

        var devPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Ignyos.LanPortal.Api", "bin", "Debug", "net9.0", "Ignyos.LanPortal.Api.exe"));
        if (File.Exists(devPath))
        {
            return devPath;
        }

        return packagedPath;
    }

    private static string GetWebExecutablePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var packagedPath = Path.GetFullPath(Path.Combine(baseDir, "..", "web", "Ignyos.LanPortal.Web.exe"));
        if (File.Exists(packagedPath))
        {
            return packagedPath;
        }

        var devPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Ignyos.LanPortal.Web", "bin", "Debug", "net9.0", "Ignyos.LanPortal.Web.exe"));
        if (File.Exists(devPath))
        {
            return devPath;
        }

        return packagedPath;
    }

    private static string GetWebViewUserDataFolder()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userDataFolder = Path.Combine(localAppData, "Ignyos", "LanPortalDev", "WebView2");
        Directory.CreateDirectory(userDataFolder);
        return userDataFolder;
    }

    private static Process? StartPortalProcess(string executablePath, string processName, IReadOnlyList<string> arguments)
    {
        if (Process.GetProcessesByName(processName).Length > 0)
        {
            return null;
        }

        if (!File.Exists(executablePath))
        {
            // In local debug compounds, API/Web may be started externally and appear a moment later.
            if (WaitForExistingProcess(processName, TimeSpan.FromSeconds(15)))
            {
                return null;
            }

            throw new FileNotFoundException($"Missing executable: {executablePath}");
        }

        return Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = string.Join(' ', arguments),
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!
        });
    }

    private static bool WaitForExistingProcess(string processName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (Process.GetProcessesByName(processName).Length > 0)
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return Process.GetProcessesByName(processName).Length > 0;
    }

    private static bool IsDevelopmentEnvironment()
    {
        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
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
            Dock = DockStyle.Top,
            BackColor = HostBackground,
            ForeColor = HostInk,
            Renderer = new ToolStripProfessionalRenderer(new HostColorTable())
        };

        var fileMenu = new ToolStripMenuItem("Portal")
        {
            ForeColor = HostInk,
            BackColor = HostBackground
        };

        var setupMenuItem = new ToolStripMenuItem("File Sharing")
        {
            ForeColor = HostInk
        };
        setupMenuItem.Click += (_, _) => NavigateTo(SetupUrl);

        var adminMenuItem = new ToolStripMenuItem("Admin")
        {
            ForeColor = HostInk
        };
        adminMenuItem.Click += (_, _) => NavigateTo(AdminUrl);

        var accessHistoryMenuItem = new ToolStripMenuItem("Access History")
        {
            ForeColor = HostInk
        };
        accessHistoryMenuItem.Click += (_, _) => NavigateTo(AccessHistoryUrl);

        var advancedMenuItem = new ToolStripMenuItem("Advanced")
        {
            ForeColor = HostInk
        };
        advancedMenuItem.Click += (_, _) => NavigateTo(AdvancedUrl);

        checkUpdatesMenuItem = new ToolStripMenuItem("Check For Updates")
        {
            ForeColor = HostInk,
            Visible = false
        };
        checkUpdatesMenuItem.Click += async (_, _) => await CheckForUpdatesAsync(isManualCheck: true, forceRefresh: true);

        fileMenu.DropDownItems.Add(setupMenuItem);
        fileMenu.DropDownItems.Add(adminMenuItem);
        fileMenu.DropDownItems.Add(accessHistoryMenuItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(advancedMenuItem);

        menuStrip.Items.Add(fileMenu);
        return menuStrip;
    }

    private static StatusStrip BuildStatusStrip(string currentVersion, out ToolStripStatusLabel stateLabel, out ToolStripStatusLabel actionLabel)
    {
        var statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            SizingGrip = false,
            BackColor = HostBackground,
            ForeColor = HostInk,
            Renderer = new ToolStripProfessionalRenderer(new HostColorTable())
        };

        var versionLabel = new ToolStripStatusLabel
        {
            Text = $"Version {currentVersion}",
            ForeColor = HostMuted
        };

        stateLabel = new ToolStripStatusLabel
        {
            Text = "Checking for updates...",
            Spring = true,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = HostMuted
        };

        actionLabel = new ToolStripStatusLabel
        {
            IsLink = true,
            Visible = false,
            ForeColor = HostAccent
        };

        statusStrip.Items.Add(versionLabel);
        statusStrip.Items.Add(stateLabel);
        statusStrip.Items.Add(actionLabel);

        return statusStrip;
    }

    private sealed class HostColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => HostBackground;
        public override Color MenuStripGradientEnd => HostBackground;
        public override Color ToolStripBorder => HostLine;
        public override Color ToolStripDropDownBackground => HostSurface;
        public override Color ToolStripGradientBegin => HostSurface;
        public override Color ToolStripGradientMiddle => HostSurface;
        public override Color ToolStripGradientEnd => HostSurfaceAlt;
        public override Color StatusStripGradientBegin => HostSurface;
        public override Color StatusStripGradientEnd => HostSurfaceAlt;
        public override Color MenuBorder => HostLine;
        public override Color MenuItemBorder => HostLine;
        public override Color MenuItemSelected => HostAccentSoft;
        public override Color MenuItemSelectedGradientBegin => HostAccentSoft;
        public override Color MenuItemSelectedGradientEnd => HostAccentSoft;
        public override Color MenuItemPressedGradientBegin => HostAccentSoft;
        public override Color MenuItemPressedGradientMiddle => HostAccentSoft;
        public override Color MenuItemPressedGradientEnd => HostAccentSoft;
        public override Color SeparatorDark => HostLine;
        public override Color SeparatorLight => HostSurfaceAlt;
        public override Color ImageMarginGradientBegin => HostSurface;
        public override Color ImageMarginGradientMiddle => HostSurface;
        public override Color ImageMarginGradientEnd => HostSurfaceAlt;
        public override Color ButtonSelectedGradientBegin => HostAccentSoft;
        public override Color ButtonSelectedGradientMiddle => HostAccentSoft;
        public override Color ButtonSelectedGradientEnd => HostAccentSoft;
        public override Color ButtonSelectedBorder => HostAccent;
        public override Color ButtonPressedGradientBegin => HostAccentDark;
        public override Color ButtonPressedGradientMiddle => HostAccent;
        public override Color ButtonPressedGradientEnd => HostAccentDark;
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

        var version = informationalVersion.Trim();
        return string.IsNullOrWhiteSpace(version) ? "unknown" : version;
    }

    private static bool IsDeveloperVersionByFourthNode(string fullVersion)
    {
        if (!TryParseVersionParts(fullVersion, out _, out _, out _, out var build))
        {
            return false;
        }

        return build > 0;
    }

    private static string GetDisplayVersionForInstaller(string fullVersion, bool isDevFlavor)
    {
        if (TryParseVersionParts(fullVersion, out var major, out var minor, out var patch, out var build))
        {
            return isDevFlavor
                ? $"{major}.{minor}.{patch}.{build}"
                : $"{major}.{minor}.{patch}";
        }

        var sanitized = fullVersion.Split('+', 2)[0].Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    private static bool TryParseVersionParts(string? value, out int major, out int minor, out int patch, out int build)
    {
        major = 0;
        minor = 0;
        patch = 0;
        build = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = Regex.Match(value, "^v?(?<major>\\d+)\\.(?<minor>\\d+)\\.(?<patch>\\d+)(?:\\.(?<build>\\d+))?");
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["major"].Value, out major) ||
            !int.TryParse(match.Groups["minor"].Value, out minor) ||
            !int.TryParse(match.Groups["patch"].Value, out patch))
        {
            return false;
        }

        if (match.Groups["build"].Success && !int.TryParse(match.Groups["build"].Value, out build))
        {
            return false;
        }

        return true;
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
                var response = await http.PostAsJsonAsync(UpdateCheckNowUrl, new UpdateCheckNowRequest(appVersionFull));
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Update check endpoint returned HTTP {(int)response.StatusCode}.");
                }

                updateStatus = await response.Content.ReadFromJsonAsync<UpdateStatusResponse>();
            }
            else
            {
                var encodedVersion = Uri.EscapeDataString(appVersionFull);
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
            availableUpdateSha256 = updateStatus.ExpectedSha256;
            availableUpdateVersion = updateStatus.LatestVersion;
            updateActionLabel.Text = updateStatus.RequiredUpdate ? "Update Required" : "New Version Available";
            updateActionLabel.Visible = true;
            updateActionLabel.Click -= OpenAvailableUpdate;
            updateActionLabel.Click += OpenAvailableUpdate;
        }
        else
        {
            availableUpdateUrl = null;
            availableUpdateSha256 = null;
            availableUpdateVersion = null;
            updateActionLabel.Visible = false;
            updateActionLabel.Click -= OpenAvailableUpdate;
        }

        if (isManualCheck && !updateStatus.UpdateAvailable && string.IsNullOrWhiteSpace(updateStatus.Error))
        {
            updateStateLabel.Text = "No newer version is currently available.";
            updateStateLabel.ForeColor = Color.DimGray;
        }
    }

    private async void OpenAvailableUpdate(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(availableUpdateUrl))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(availableUpdateSha256))
        {
            Trace.WriteLine("[UpdateInstall] Missing expected SHA256 for update package. Installation blocked.");
            updateStateLabel.Text = "Update package verification metadata is missing.";
            updateStateLabel.ForeColor = Color.DarkOrange;

            var missingShaMetadata = BuildRollbackMetadata(
                failureReasonCode: FailureCodeMissingExpectedSha,
                failureMessage: "ExpectedSha256 was not provided by update status endpoint.",
                installerPath: null,
                orchestrationAttempted: false,
                rollbackTriggered: false);
            await WriteRollbackMetadataAsync(missingShaMetadata);

            if (isTestChannel)
            {
                MessageBox.Show(
                    "Update package metadata is incomplete. Installation was blocked.",
                    AppTitlePrefix,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            return;
        }

        var previousActionText = updateActionLabel.Text;
        var previousActionVisible = updateActionLabel.Visible;
        var previousStateText = updateStateLabel.Text;
        var previousStateColor = updateStateLabel.ForeColor;

        updateActionLabel.Enabled = false;
        updateActionLabel.Text = "Preparing update...";
        updateStateLabel.Text = "Downloading and verifying installer...";
        updateStateLabel.ForeColor = Color.DimGray;

        string? installerPath = null;
        var orchestrationAttempted = false;

        try
        {
            var forcedFault = GetForcedUpdateFaultMode();

            installerPath = await DownloadAndVerifyUpdateInstallerAsync(
                availableUpdateUrl,
                availableUpdateSha256,
                availableUpdateVersion,
                forcedFault);

            orchestrationAttempted = true;
            await RunPreInstallOrchestrationHooksAsync(forcedFault);

            if (forcedFault == FaultLaunch)
            {
                throw new InvalidOperationException("Forced launch failure for Stage 5 validation.");
            }

            var launchedInstaller = Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });

            if (launchedInstaller is null)
            {
                throw new InvalidOperationException("Installer process did not start.");
            }

            updateStateLabel.Text = "Installer verified and launched.";
            updateStateLabel.ForeColor = Color.FromArgb(20, 92, 148);

            if (isTestChannel && EnableUpdateOrchestrationForTestChannel)
            {
                Trace.WriteLine("[UpdateInstall] Closing host to allow installer update sequence.");
                BeginInvoke(new Action(Close));
            }
        }
        catch (Exception ex)
        {
            var failureReasonCode = ResolveFailureReasonCode(ex, orchestrationAttempted);
            var rollbackTriggered = isTestChannel && EnableUpdateOrchestrationForTestChannel &&
                                    IsRollbackTriggerFailure(failureReasonCode);

            var failureMetadata = BuildRollbackMetadata(
                failureReasonCode,
                ex.Message,
                installerPath,
                orchestrationAttempted,
                rollbackTriggered);

            await WriteRollbackMetadataAsync(failureMetadata);

            if (rollbackTriggered)
            {
                await WriteRollbackTriggerAsync(failureMetadata);
            }

            Trace.WriteLine($"[UpdateInstall] Installation blocked: {ex.Message}");
            updateStateLabel.Text = "Update installation blocked by safety checks.";
            updateStateLabel.ForeColor = Color.DarkOrange;

            if (isTestChannel)
            {
                MessageBox.Show(
                    "Update installation was blocked. See logs for details.",
                    AppTitlePrefix,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        finally
        {
            updateActionLabel.Enabled = true;
            updateActionLabel.Text = previousActionText;
            updateActionLabel.Visible = previousActionVisible;

            if (updateStateLabel.Text == "Downloading and verifying installer...")
            {
                updateStateLabel.Text = previousStateText;
                updateStateLabel.ForeColor = previousStateColor;
            }
        }
    }

    private RollbackMetadata BuildRollbackMetadata(
        string failureReasonCode,
        string failureMessage,
        string? installerPath,
        bool orchestrationAttempted,
        bool rollbackTriggered)
    {
        var updateRoot = GetLanPortalStateRoot();
        var backupRoot = Path.Combine(updateRoot, "Backups");

        return new RollbackMetadata(
            DateTimeOffset.UtcNow,
            appVersionFull,
            availableUpdateVersion,
            isTestChannel ? "test" : "production",
            isTestChannel,
            availableUpdateUrl,
            availableUpdateSha256,
            installerPath,
            backupRoot,
            $"version-{appVersionFull}",
            string.IsNullOrWhiteSpace(availableUpdateVersion) ? null : $"version-{availableUpdateVersion}",
            failureReasonCode,
            failureMessage,
            orchestrationAttempted,
            rollbackTriggered);
    }

    private static string ResolveFailureReasonCode(Exception ex, bool orchestrationAttempted)
    {
        var message = ex.Message;

        if (message.Contains("Checksum mismatch", StringComparison.OrdinalIgnoreCase))
        {
            return FailureCodeChecksumMismatch;
        }

        if (message.Contains("Failed to download update package", StringComparison.OrdinalIgnoreCase))
        {
            return FailureCodeDownloadFailed;
        }

        if (message.Contains("stop managed process", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Timed out stopping managed process", StringComparison.OrdinalIgnoreCase))
        {
            return FailureCodeOrchestrationFailed;
        }

        if (orchestrationAttempted)
        {
            return FailureCodeInstallerLaunchFailed;
        }

        return FailureCodeUnknown;
    }

    private static bool IsRollbackTriggerFailure(string failureReasonCode)
    {
        return failureReasonCode == FailureCodeOrchestrationFailed ||
               failureReasonCode == FailureCodeInstallerLaunchFailed;
    }

    private static async Task WriteRollbackMetadataAsync(RollbackMetadata metadata)
    {
        var updateStateDir = Path.Combine(GetLanPortalStateRoot(), "UpdateState");
        Directory.CreateDirectory(updateStateDir);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var latestPath = Path.Combine(updateStateDir, "rollback-metadata-latest.json");
        var historyPath = Path.Combine(
            updateStateDir,
            $"rollback-metadata-{metadata.CreatedAtUtc:yyyyMMddHHmmss}.json");

        var json = JsonSerializer.Serialize(metadata, options);
        await File.WriteAllTextAsync(latestPath, json);
        await File.WriteAllTextAsync(historyPath, json);

        Trace.WriteLine($"[UpdateInstall] Rollback metadata recorded: {latestPath}");
    }

    private static async Task WriteRollbackTriggerAsync(RollbackMetadata metadata)
    {
        var updateStateDir = Path.Combine(GetLanPortalStateRoot(), "UpdateState");
        Directory.CreateDirectory(updateStateDir);

        var trigger = new RollbackTrigger(
            metadata.CreatedAtUtc,
            metadata.FailureReasonCode,
            metadata.TargetVersion,
            metadata.Channel,
            "Rollback requested after install/restart failure path.");

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var triggerPath = Path.Combine(updateStateDir, "rollback-trigger.json");
        await File.WriteAllTextAsync(triggerPath, JsonSerializer.Serialize(trigger, options));

        Trace.WriteLine($"[UpdateInstall] Rollback trigger marker created: {triggerPath}");
    }

    private static string GetLanPortalStateRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ignyos",
            "LanPortalDev");
    }

    private async Task RunPreInstallOrchestrationHooksAsync()
        => await RunPreInstallOrchestrationHooksAsync(GetForcedUpdateFaultMode());

    private async Task RunPreInstallOrchestrationHooksAsync(string forcedFaultMode)
    {
        if (!EnableUpdateOrchestrationForTestChannel || !isTestChannel)
        {
            Trace.WriteLine("[UpdateInstall] Orchestration hooks skipped (not enabled for this channel).");
            return;
        }

        if (forcedFaultMode == FaultOrchestration)
        {
            throw new InvalidOperationException("Forced orchestration failure for Stage 5 validation.");
        }

        Trace.WriteLine("[UpdateInstall] Running pre-install orchestration hooks for test channel.");
        updatePollTimer.Stop();

        await StopManagedProcessAsync(managedWebProcess, "Ignyos.LanPortal.Web");
        managedWebProcess = null;

        await StopManagedProcessAsync(managedApiProcess, "Ignyos.LanPortal.Api");
        managedApiProcess = null;

        Trace.WriteLine("[UpdateInstall] Pre-install orchestration hooks completed.");
    }

    private static async Task StopManagedProcessAsync(Process? process, string processName)
    {
        if (process is null)
        {
            Trace.WriteLine($"[UpdateInstall] No managed process tracked for {processName}; skipping stop hook.");
            return;
        }

        try
        {
            if (process.HasExited)
            {
                Trace.WriteLine($"[UpdateInstall] Managed process already exited: {processName} (PID {process.Id}).");
                return;
            }

            Trace.WriteLine($"[UpdateInstall] Stopping managed process {processName} (PID {process.Id}).");
            process.Kill(entireProcessTree: true);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(cts.Token);
            Trace.WriteLine($"[UpdateInstall] Managed process stopped: {processName} (PID {process.Id}).");
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException($"Timed out stopping managed process {processName}.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to stop managed process {processName}: {ex.Message}", ex);
        }
    }

    private static async Task<string> DownloadAndVerifyUpdateInstallerAsync(
        string downloadUrl,
        string expectedSha256,
        string? version,
        string forcedFaultMode)
    {
        var updatesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ignyos",
            "LanPortalDev",
            "Updates");

        Directory.CreateDirectory(updatesRoot);

        var parsedUri = new Uri(downloadUrl);
        var fileName = Path.GetFileName(parsedUri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            var suffix = string.IsNullOrWhiteSpace(version) ? DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss") : version;
            fileName = $"Ignyos-LanPortal-Update-{suffix}.exe";
        }

        var targetPath = Path.Combine(updatesRoot, fileName);
        var downloadPath = targetPath + ".download";

        if (File.Exists(downloadPath))
        {
            File.Delete(downloadPath);
        }

        await DownloadInstallerWithRetryAsync(downloadUrl, downloadPath, maxAttempts: 3, forcedFaultMode);

        string actualSha256;
        await using (var verifyStream = new FileStream(downloadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var hashBytes = await SHA256.HashDataAsync(verifyStream);
            actualSha256 = Convert.ToHexString(hashBytes);
        }

        var normalizedExpected = expectedSha256.Trim().ToUpperInvariant();
        if (forcedFaultMode == FaultChecksum)
        {
            normalizedExpected = new string('0', 64);
        }

        if (!string.Equals(actualSha256, normalizedExpected, StringComparison.Ordinal))
        {
            File.Delete(downloadPath);
            throw new InvalidOperationException(
                $"Checksum mismatch for update package. Expected {normalizedExpected}, actual {actualSha256}.");
        }

        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        File.Move(downloadPath, targetPath);
        Trace.WriteLine($"[UpdateInstall] Verified installer downloaded to {targetPath}");

        return targetPath;
    }

    private static async Task DownloadInstallerWithRetryAsync(string downloadUrl, string downloadPath, int maxAttempts, string forcedFaultMode)
    {
        var initialDelay = TimeSpan.FromSeconds(2);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (forcedFaultMode == FaultDownload)
                {
                    throw new InvalidOperationException("Forced download failure for Stage 5 validation.");
                }

                if (File.Exists(downloadPath))
                {
                    File.Delete(downloadPath);
                }

                Trace.WriteLine($"[UpdateInstall] Download attempt {attempt}/{maxAttempts} started.");

                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using var sourceStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await sourceStream.CopyToAsync(fileStream);

                Trace.WriteLine($"[UpdateInstall] Download attempt {attempt}/{maxAttempts} succeeded.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(initialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                Trace.WriteLine($"[UpdateInstall] Download attempt {attempt}/{maxAttempts} failed: {ex.Message}. Retrying in {delay.TotalSeconds:N0}s.");
                await Task.Delay(delay);
            }
        }

        throw new InvalidOperationException($"Failed to download update package after {maxAttempts} attempts.");
    }

    private string GetForcedUpdateFaultMode()
    {
        if (!isTestChannel)
        {
            return FaultNone;
        }

        var raw = Environment.GetEnvironmentVariable("LANPORTAL_UPDATE_TEST_FAULT");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return FaultNone;
        }

        var normalized = raw.Trim().ToUpperInvariant();
        var allowed = normalized == FaultDownload ||
                      normalized == FaultChecksum ||
                      normalized == FaultOrchestration ||
                      normalized == FaultLaunch;

        if (!allowed)
        {
            Trace.WriteLine($"[UpdateInstall] Ignoring unsupported fault mode '{raw}'.");
            return FaultNone;
        }

        Trace.WriteLine($"[UpdateInstall] Forced test fault mode active: {normalized}");
        return normalized;
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
        [property: JsonPropertyName("expectedSha256")] string? ExpectedSha256,
        [property: JsonPropertyName("updateAvailable")] bool UpdateAvailable,
        [property: JsonPropertyName("requiredUpdate")] bool RequiredUpdate,
        [property: JsonPropertyName("channel")] string Channel,
        [property: JsonPropertyName("isTestChannel")] bool IsTestChannel,
        [property: JsonPropertyName("manifestUrl")] string ManifestUrl,
        [property: JsonPropertyName("checkedAtUtc")] DateTimeOffset CheckedAtUtc,
        [property: JsonPropertyName("isStale")] bool IsStale,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record RollbackMetadata(
        DateTimeOffset CreatedAtUtc,
        string CurrentVersion,
        string? TargetVersion,
        string Channel,
        bool IsTestChannel,
        string? DownloadUrl,
        string? ExpectedSha256,
        string? DownloadedInstallerPath,
        string BackupRootPath,
        string PreviousVersionMarker,
        string? TargetVersionMarker,
        string FailureReasonCode,
        string FailureMessage,
        bool OrchestrationAttempted,
        bool RollbackTriggered);

    private sealed record RollbackTrigger(
        DateTimeOffset CreatedAtUtc,
        string FailureReasonCode,
        string? TargetVersion,
        string Channel,
        string Message);
}
