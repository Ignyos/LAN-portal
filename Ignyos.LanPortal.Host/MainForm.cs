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
    private const string AppTitlePrefix = "Ignyos LAN Portal";

    private readonly WebView2 browser;
    private readonly string appVersion;

    public MainForm()
    {
        appVersion = GetAppVersion();
        Text = $"{AppTitlePrefix} v{appVersion}";
        Width = 1280;
        Height = 860;
        MinimumSize = new Size(900, 640);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;

        var menuStrip = BuildMenuStrip();
        MainMenuStrip = menuStrip;

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

    private MenuStrip BuildMenuStrip()
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

        fileMenu.DropDownItems.Add(setupMenuItem);
        fileMenu.DropDownItems.Add(adminMenuItem);
        fileMenu.DropDownItems.Add(accessHistoryMenuItem);
        fileMenu.DropDownItems.Add(refreshMenuItem);

        menuStrip.Items.Add(fileMenu);
        return menuStrip;
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

    private void ShowFatalError(string title, string detail)
    {
        MessageBox.Show($"{title}\n\n{detail}", AppTitlePrefix, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private sealed record SetupStatus([property: JsonPropertyName("isSetupComplete")] bool IsSetupComplete);
}
