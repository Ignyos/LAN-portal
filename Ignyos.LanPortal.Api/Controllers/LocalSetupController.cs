using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using Ignyos.LanPortal.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using QRCoder;

namespace Ignyos.LanPortal.Api.Controllers;

[ApiController]
public sealed class LocalSetupController(
    IAppSettingsStore settingsStore,
    IHostUiStateStore hostUiStateStore,
    IApplicationLogStore applicationLogStore,
    ApplicationEventLogger applicationEventLogger,
    IWindowsStartupRegistration windowsStartupRegistration) : Controller
{
    private const string GuestLoginHostName = "lan.home.arpa";
    private const int DevelopmentGuestLoginPort = 5014;

    [HttpGet("local/setup")]
    public IActionResult SetupPage()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        var model = new LocalSetupPageViewModel(
            settingsStore.IsSetupComplete(),
            settingsStore.GetStorageRootPath() ?? string.Empty,
            BuildGuestLoginUrl(),
            BuildCustomGuestLoginUrl(),
            EvaluateGuestDnsStatus());

        return View(model);
    }

    [HttpGet("local/settings")]
    public IActionResult SettingsPage()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        var startupState = windowsStartupRegistration.GetState();
        return View(new LocalSettingsPageViewModel(
            settingsStore.GetRunAtWindowsStartup(),
            startupState.IsSupported,
            startupState.Message,
            Request.Query.ContainsKey("saved")));
    }

    [HttpPost("local/settings/startup")]
    public IActionResult SaveStartupSettings([FromForm] StartupSettingsRequest request)
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        settingsStore.SetRunAtWindowsStartup(request.RunAtWindowsStartup);
        windowsStartupRegistration.Apply(request.RunAtWindowsStartup);

        return Redirect("/local/settings?saved=1");
    }

    [HttpGet("local/about")]
    public IActionResult AboutPage()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        return View(new LocalAboutPageViewModel(
            GetDisplayVersion(),
            GetReleaseDateDisplay()));
    }

    [HttpGet("local/advanced")]
    public IActionResult AdvancedPage()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        return View(new LocalAdvancedPageViewModel(
            BuildGuestLoginUrl(),
            BuildCustomGuestLoginUrl(),
            EvaluateGuestDnsStatus()));
    }

    [HttpGet("api/local/logs")]
    public ActionResult<IReadOnlyList<ApplicationLogRecord>> Logs(
        [FromQuery] string? severity,
        [FromQuery] string? category,
        [FromQuery] int maxCount = 50)
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        ApplicationLogSeverity? minimumSeverity = null;
        if (Enum.TryParse<ApplicationLogSeverity>(severity, true, out var parsedSeverity))
        {
            minimumSeverity = parsedSeverity;
        }

        ApplicationLogCategory? selectedCategory = null;
        if (Enum.TryParse<ApplicationLogCategory>(category, true, out var parsedCategory))
        {
            selectedCategory = parsedCategory;
        }

        var safeMaxCount = Math.Clamp(maxCount, 1, 200);
        return Ok(applicationLogStore.GetRecent(safeMaxCount, minimumSeverity, selectedCategory));
    }

    [HttpPost("api/local/security/rotate-signing-key")]
    public IActionResult RotateSigningKey()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        var result = settingsStore.RotateJwtSigningKey();
        applicationEventLogger.LogSigningKeyRotated(result.RevokedSessionCount, result.KeyFingerprint);
        return Ok(result);
    }

    [HttpGet("api/local/ui-state")]
    public IActionResult GetHostUiState([FromQuery] string? page)
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        return Ok(hostUiStateStore.GetPageState(page ?? string.Empty));
    }

    [HttpPost("api/local/ui-state")]
    public IActionResult SetHostUiState([FromBody] HostUiStateRequest request)
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.PageKey) || string.IsNullOrWhiteSpace(request.SectionKey))
        {
            return BadRequest("PageKey and SectionKey are required.");
        }

        hostUiStateStore.SetSectionState(request.PageKey, request.SectionKey, request.IsExpanded);
        return Ok();
    }

    [HttpGet("api/local/setup/guest-login-qr.svg")]
    public IActionResult GuestLoginQrCode()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        var qrUrl = BuildGuestLoginUrl();
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
        var qrSvg = new SvgQRCode(data).GetGraphic(8);

        return Content(qrSvg, "image/svg+xml", Encoding.UTF8);
    }

    [HttpGet("api/local/setup/status")]
    public IActionResult Status()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        return Ok(new
        {
            IsSetupComplete = settingsStore.IsSetupComplete(),
            StorageRootPath = settingsStore.GetStorageRootPath()
        });
    }

    [HttpPost("api/local/setup/storage-root")]
    public IActionResult SaveStorageRoot([FromBody] SaveStorageRootRequest request)
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.StorageRootPath))
        {
            return BadRequest("StorageRootPath is required.");
        }

        settingsStore.SetStorageRootPath(request.StorageRootPath);
        return Ok();
    }

    [HttpPost("api/local/setup/pick-storage-root")]
    public IActionResult PickStorageRoot([FromBody] PickStorageRootRequest? request)
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        if (!OperatingSystem.IsWindows())
        {
            return StatusCode(StatusCodes.Status501NotImplemented, "Folder picking is only available on Windows hosts.");
        }

        var selectedPath = TryPickStorageRoot(request?.CurrentPath);
        return string.IsNullOrWhiteSpace(selectedPath)
            ? NoContent()
            : Ok(new PickStorageRootResponse(selectedPath));
    }

    private static bool IsLocalRequest(HttpContext httpContext)
    {
        var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
        if (remoteIpAddress is null)
        {
            return false;
        }

        if (IPAddress.IsLoopback(remoteIpAddress))
        {
            return true;
        }

        if (remoteIpAddress.IsIPv4MappedToIPv6)
        {
            var mapped = remoteIpAddress.MapToIPv4();
            if (IPAddress.IsLoopback(mapped))
            {
                return true;
            }
        }

        var localIpAddress = httpContext.Connection.LocalIpAddress;
        return localIpAddress is not null && remoteIpAddress.Equals(localIpAddress);
    }

    private static string BuildGuestLoginUrl()
    {
        var lanIp = GetLanIpv4Address();
        var host = string.IsNullOrWhiteSpace(lanIp)
            ? "http://localhost/"
            : $"http://{lanIp}/";

        return AddDevelopmentPortIfNeeded(host);
    }

    private static string BuildCustomGuestLoginUrl()
    {
        return AddDevelopmentPortIfNeeded($"http://{GuestLoginHostName}/");
    }

    private static string AddDevelopmentPortIfNeeded(string baseUrl)
    {
        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
        {
            return baseUrl;
        }

        var builder = new UriBuilder(baseUrl)
        {
            Port = DevelopmentGuestLoginPort
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    private static GuestDnsStatus EvaluateGuestDnsStatus()
    {
        var lanIp = GetLanIpv4Address();
        if (string.IsNullOrWhiteSpace(lanIp))
        {
            return new GuestDnsStatus(
                false,
                  "Could not detect this machine's LAN IP. Keep using the default portal URL once network details are available.");
        }

        try
        {
            var resolvedIps = Dns.GetHostAddresses(GuestLoginHostName)
                .Where(address =>
                    address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address))
                .Select(address => address.ToString())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (resolvedIps.Any(ip => string.Equals(ip, lanIp, StringComparison.Ordinal)))
            {
                return new GuestDnsStatus(
                    true,
                    $"Custom URL is ready. {GuestLoginHostName} resolves to this host ({lanIp}).");
            }

            if (resolvedIps.Length > 0)
            {
                return new GuestDnsStatus(
                    false,
                    $"Custom URL is not ready yet. {GuestLoginHostName} currently resolves to {string.Join(", ", resolvedIps)} instead of {lanIp}. Keep using the default portal URL.");
            }
        }
        catch
        {
            // DNS lookup failures are common on isolated/offline hosts.
        }

        return new GuestDnsStatus(
            false,
            $"Custom URL is not configured yet. Map {GuestLoginHostName} to this host LAN IP ({lanIp}) in your router DNS. Keep using the default portal URL.");
    }

    private static string? GetLanIpv4Address()
    {
        try
        {
            var addresses = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (var address in addresses)
            {
                if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                {
                    continue;
                }

                var bytes = address.GetAddressBytes();
                var isPrivateRange =
                    bytes[0] == 10 ||
                    (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                    (bytes[0] == 192 && bytes[1] == 168);

                if (isPrivateRange)
                {
                    return address.ToString();
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public sealed record SaveStorageRootRequest(string StorageRootPath);

    public sealed record StartupSettingsRequest(bool RunAtWindowsStartup);

    public sealed record PickStorageRootRequest(string? CurrentPath);

    public sealed record PickStorageRootResponse(string StorageRootPath);

    public sealed record HostUiStateRequest(string PageKey, string SectionKey, bool IsExpanded);

    private static string GetReleaseDateDisplay()
    {
        var assemblyLocation = Assembly.GetEntryAssembly()?.Location;
        if (string.IsNullOrWhiteSpace(assemblyLocation) || !System.IO.File.Exists(assemblyLocation))
        {
            return "unknown";
        }

        return System.IO.File.GetLastWriteTimeUtc(assemblyLocation)
            .ToLocalTime()
            .ToString("yyyy-MM-dd");
    }

    private static string GetDisplayVersion()
    {
        var informational = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (TryParseVersionParts(informational, out var major, out var minor, out var patch, out var build))
        {
            return build > 0
                ? $"{major}.{minor}.{patch}.{build}"
                : $"{major}.{minor}.{patch}";
        }

        var fallback = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
        if (TryParseVersionParts(fallback, out major, out minor, out patch, out build))
        {
            return build > 0
                ? $"{major}.{minor}.{patch}.{build}"
                : $"{major}.{minor}.{patch}";
        }

        return "unknown";
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

    private static string? TryPickStorageRoot(string? currentPath)
    {
        try
        {
            var escapedPath = (currentPath ?? string.Empty).Replace("'", "''");
            var script = string.Join("; ",
                "Add-Type -AssemblyName System.Windows.Forms",
                "$dialog = New-Object System.Windows.Forms.FolderBrowserDialog",
                "$dialog.Description = 'Select storage folder for Ignyos LAN Portal'",
                "$dialog.UseDescriptionForTitle = $true",
                $"if ('{escapedPath}' -ne '') {{ $dialog.SelectedPath = '{escapedPath}' }}",
                "if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; Write-Output $dialog.SelectedPath }");

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -STA -WindowStyle Hidden -Command \"{script}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit();

            var selectedPath = output.Trim();
            return string.IsNullOrWhiteSpace(selectedPath) ? null : selectedPath;
        }
        catch
        {
            return null;
        }
    }
}

public sealed record LocalSetupPageViewModel(
    bool SetupComplete,
    string StorageRootPath,
    string GuestLoginUrl,
    string CustomGuestLoginUrl,
    GuestDnsStatus GuestDnsStatus);

public sealed record LocalSettingsPageViewModel(
    bool RunAtWindowsStartup,
    bool IsStartupSupported,
    string SupportMessage,
    bool ShowSavedNotice);

public sealed record LocalAboutPageViewModel(
    string Version,
    string ReleaseDate);

public sealed record LocalAdvancedPageViewModel(
    string GuestLoginUrl,
    string CustomGuestLoginUrl,
    GuestDnsStatus GuestDnsStatus);

public sealed record LocalAdminPageViewModel(
    string GuestLoginUrl,
    string CustomGuestLoginUrl,
    GuestDnsStatus GuestDnsStatus,
    string TokenExpiryOptionsJson);

public sealed record GuestDnsStatus(bool IsConfigured, string Message);
