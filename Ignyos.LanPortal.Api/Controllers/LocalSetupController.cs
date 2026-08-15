using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;
using Ignyos.LanPortal.Api.Services;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace Ignyos.LanPortal.Api.Controllers;

[ApiController]
public sealed class LocalSetupController(IAppSettingsStore settingsStore) : ControllerBase
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

        var setupComplete = settingsStore.IsSetupComplete();
        var storageRootPath = settingsStore.GetStorageRootPath() ?? string.Empty;
        var guestLoginUrl = BuildGuestLoginUrl();
        var customGuestLoginUrl = BuildCustomGuestLoginUrl();
        var guestDnsStatus = EvaluateGuestDnsStatus();

        var html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>My Home - File Share Setup</title>
    <style>
        :root { --bg: #f4f7f8; --card: #ffffff; --ink: #102028; --muted: #68757d; --accent: #0a6c74; --line: #d9e1e4; --ok: #116b2f; --warn: #9d5d00; }
        body { margin: 0; font-family: Segoe UI, sans-serif; background: linear-gradient(180deg,#f3f7f8,#edf3f5); color: var(--ink); }
        .shell { max-width: 760px; margin: 32px auto; padding: 0 16px 28px; }
        .card { background: var(--card); border: 1px solid var(--line); border-radius: 14px; padding: 20px; box-shadow: 0 14px 40px rgba(16,32,40,.06); }
        h1 { margin: 0; font-size: 28px; }
        .sub { color: var(--muted); margin-top: 8px; line-height: 1.5; }
        .banner { margin: 18px 0 0; padding: 12px 14px; border-radius: 10px; background: #f7fafb; border: 1px solid var(--line); }
        .banner.ok { color: var(--ok); background: #eef8f1; border-color: #cfe9d7; }
        .banner.warn { color: var(--warn); background: #fff8eb; border-color: #f1dfb7; }
        .steps { margin: 18px 0 0; padding-left: 20px; color: var(--ink); }
        .steps li { margin: 8px 0; }
        .field { margin-top: 18px; }
        label { display: block; font-weight: 600; margin-bottom: 8px; }
        input { width: 100%; box-sizing: border-box; padding: 11px 12px; border: 1px solid var(--line); border-radius: 10px; font: inherit; }
        .actions { display: flex; gap: 10px; margin-top: 16px; flex-wrap: wrap; }
        .path-row { display: flex; gap: 10px; align-items: center; }
        .path-row input { flex: 1 1 auto; }
        .path-row button { white-space: nowrap; }
        button, .linkbtn { border: 1px solid var(--line); border-radius: 10px; padding: 10px 14px; font: inherit; cursor: pointer; background: #fff; color: var(--ink); text-decoration: none; display: inline-flex; align-items: center; }
        button.primary, .linkbtn.primary { background: var(--accent); border-color: var(--accent); color: #fff; }
        .status { margin-top: 14px; min-height: 1.4em; }
        .muted { color: var(--muted); font-size: 14px; }
        .footer { margin-top: 16px; color: var(--muted); font-size: 14px; }
        .guest { margin-top: 18px; padding: 14px; border-radius: 10px; background: #f7fafb; border: 1px solid var(--line); display: flex; gap: 16px; align-items: center; flex-wrap: wrap; }
        .guest img { width: 156px; height: 156px; border: 1px solid var(--line); border-radius: 8px; background: #fff; padding: 8px; }
        .guest-url { font-family: Consolas, Menlo, monospace; font-size: 14px; word-break: break-all; }
        details.router-help { margin-top: 10px; }
        details.router-help summary { cursor: pointer; font-weight: 600; }
    </style>
</head>
<body>
    <div class="shell">
        <div class="card">
            <h1>My Home - File Share Setup</h1>
            <div class="sub">Use this page any time to verify or change where shared files are stored on this host machine.</div>

            <div class="banner {{(setupComplete ? "ok" : "warn")}}" id="setupBanner">
                {{(setupComplete ? "Setup is already complete. You can continue to the admin console." : "Setup is not complete yet. You only need to choose a storage folder.")}}
            </div>

            <ol class="steps">
                <li>Choose a storage folder for portal files.</li>
                <li>Save the setup.</li>
                <li>Open the admin console to manage approvals and access sessions.</li>
            </ol>

            <div class="field">
                <label for="storageRootPath">Storage folder</label>
                <div class="path-row">
                    <input id="storageRootPath" value="{{storageRootPath}}" placeholder="D:/Ignyos/LanPortal" />
                    <button type="button" onclick="pickStorageFolder()">Browse...</button>
                </div>
            </div>

            <div class="actions">
                <a class="linkbtn primary" id="openAdminLink" href="/local/admin" style="{{(setupComplete ? "" : "display:none;")}}">Open admin console</a>
            </div>

            <div class="status" id="status"></div>
            <div class="guest">
                <img src="/api/local/setup/guest-login-qr.svg" alt="Guest login QR code" />
                <div>
                    <div><strong>Recommended guest URL</strong></div>
                    <div class="guest-url">{{guestLoginUrl}}</div>
                    <div class="muted" style="margin-top:6px;">Guests on the same Wi-Fi can always scan this QR code to open login. No router changes required.</div>
                    <div class="banner {{(guestDnsStatus.IsConfigured ? "ok" : "warn")}}" style="margin-top:10px;">{{guestDnsStatus.Message}}</div>
                    <div class="muted" style="margin-top:8px;"><strong>Optional custom URL:</strong> {{customGuestLoginUrl}}</div>
                    <details class="router-help">
                        <summary>How to customize a URL to replace the default {{guestLoginUrl}}</summary>
                        <ol class="steps" style="margin-top:8px;">
                            <li>Create a DHCP reservation so this host keeps the same LAN IP.</li>
                            <li>In your router DNS settings, add an A record: lan.home.arpa -> this host LAN IP.</li>
                            <li>Reconnect guest devices to Wi-Fi (or toggle Wi-Fi) so they pick up updated DNS.</li>
                            <li>Share <span class="guest-url">{{customGuestLoginUrl}}</span> instead of the default IP link.</li>
                        </ol>
                    </details>
                </div>
            </div>
            <div class="footer">Nothing else is required during installation. Once setup is saved, you can use the portal from this browser window.</div>
        </div>
    </div>

    <script>
    async function persistStorageRoot(storageRootPath) {
        const response = await fetch('/api/local/setup/storage-root', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ storageRootPath })
        });

        return response;
    }

    async function pickStorageFolder() {
        const status = document.getElementById('status');
        const storageRootPathInput = document.getElementById('storageRootPath');
        const currentPath = storageRootPathInput.value;

        try {
            const response = await fetch('/api/local/setup/pick-storage-root', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ currentPath })
            });

            if (response.status === 204) {
                return;
            }

            if (!response.ok) {
                status.className = 'status warn';
                status.innerText = 'Could not open folder picker. Please enter a folder path manually.';
                return;
            }

            const payload = await response.json();
            if (payload && payload.storageRootPath) {
                storageRootPathInput.value = payload.storageRootPath;

                const saveResponse = await persistStorageRoot(payload.storageRootPath);
                if (!saveResponse.ok) {
                    status.className = 'status warn';
                    status.innerText = 'Folder selected, but setup could not be saved. Please try again.';
                    return;
                }

                document.getElementById('setupBanner').className = 'banner ok';
                document.getElementById('setupBanner').innerText = 'Setup saved. You can now open the admin console.';
                document.getElementById('openAdminLink').style.display = '';
                status.className = 'status ok';
                status.innerText = 'Folder selected and saved.';
            }
        } catch {
            status.className = 'status warn';
            status.innerText = 'Could not open folder picker. Please enter a folder path manually.';
        }
    }
    </script>
</body>
</html>
""";

        return Content(html, "text/html", Encoding.UTF8);
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
            ? $"http://{GuestLoginHostName}/login"
            : $"http://{lanIp}/login";

        return AddDevelopmentPortIfNeeded(host);
    }

    private static string BuildCustomGuestLoginUrl()
    {
        return AddDevelopmentPortIfNeeded($"http://{GuestLoginHostName}/login");
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
                "Could not detect this machine's LAN IP. Keep using the default IP login URL once network details are available.");
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
                    $"Custom URL is not ready yet. {GuestLoginHostName} currently resolves to {string.Join(", ", resolvedIps)} instead of {lanIp}. Keep using the default IP login URL.");
            }
        }
        catch
        {
            // DNS lookup failures are common on isolated/offline hosts.
        }

        return new GuestDnsStatus(
            false,
            $"Custom URL is not configured yet. Map {GuestLoginHostName} to this host LAN IP ({lanIp}) in your router DNS. Keep using the default IP login URL.");
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

    public sealed record PickStorageRootRequest(string? CurrentPath);

    public sealed record PickStorageRootResponse(string StorageRootPath);

    private sealed record GuestDnsStatus(bool IsConfigured, string Message);

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
