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
public sealed class LocalSetupController(IAppSettingsStore settingsStore, IHostUiStateStore hostUiStateStore) : ControllerBase
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
    <title>LAN Portal | File Sharing</title>
    <link rel="stylesheet" href="/host.css?v=1" />
</head>
<body>
    <div class="shell">
        <header class="page-header">
            <p class="eyebrow">File Sharing</p>
        </header>

        <section class="card">
            <div class="step-title">
                <span class="step-badge">1</span>
                <h1 class="step-label">Choose a folder or drive to share</h1>
            </div>
            <div class="step-body">
                <div class="field">
                    <div class="path-row">
                        <input id="storageRootPath" value="{{storageRootPath}}" placeholder="D:/Ignyos/LanPortal" aria-label="Shared folder" readonly />
                        <button type="button" class="secondary" id="changeStorageRootButton">Browse</button>
                    </div>
                </div>
            </div>
        </section>

        <section class="card">
                <div class="step-title">
                    <span class="step-badge">2</span>
                    <h2 class="step-label">Share the link or QR Code</h2>
                </div>
                <div class="step-body">
                    <div class="guest-url" style="margin-top:1.25rem;">{{guestLoginUrl}}</div>
                    <div class="guest-qr">
                        <img src="/api/local/setup/guest-login-qr.svg" alt="Guest access QR code" />
                    </div>
                </div>
        </section>

        <section class="card">
                <div class="step-title">
                    <span class="step-badge">3</span>
                    <h2 class="step-label">Securely grant access to the shared folder or drive</h2>
                </div>
                <div class="step-body">
                    <div class="actions">
                        <button type="button" id="openAdminButton" onclick="openAdminConsole()">Open admin console</button>
                    </div>
                </div>
        </section>

        <div class="status" id="status"></div>
    </div>

    <script>
        async function persistStorageRoot(storageRootPath) {
            return fetch('/api/local/setup/storage-root', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ storageRootPath })
            });
        }

        async function changeStorageRoot() {
            const input = document.getElementById('storageRootPath');
            const status = document.getElementById('status');
            const currentPath = (input.value || '').trim();

            status.className = 'status';
            status.textContent = 'Opening folder picker...';

            try {
                const response = await fetch('/api/local/setup/pick-storage-root', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ currentPath })
                });

                if (response.status === 204) {
                    status.className = 'status error';
                    status.textContent = 'No folder was selected.';
                    return;
                }

                if (!response.ok) {
                    status.className = 'status error';
                    status.textContent = 'Could not open the folder picker.';
                    return;
                }

                const data = await response.json();
                const selectedPath = (data?.storageRootPath || '').trim();
                if (!selectedPath) {
                    status.className = 'status error';
                    status.textContent = 'No folder was selected.';
                    return;
                }

                const saveResponse = await persistStorageRoot(selectedPath);
                if (!saveResponse.ok) {
                    status.className = 'status error';
                    status.textContent = 'Could not save the selected folder.';
                    return;
                }

                input.value = selectedPath;
                status.className = 'status ok';
                status.textContent = 'Shared folder updated.';
            } catch {
                status.className = 'status error';
                status.textContent = 'Could not update the shared folder.';
            }
        }

        async function openAdminConsole() {
            const input = document.getElementById('storageRootPath');
            const status = document.getElementById('status');
            const storageRootPath = (input.value || '').trim();

            if (!storageRootPath) {
                status.className = 'status error';
                status.textContent = 'Please choose a shared folder first.';
                return;
            }

            status.className = 'status';
            status.textContent = 'Saving...';

            try {
                const response = await persistStorageRoot(storageRootPath);
                if (!response.ok) {
                    status.className = 'status error';
                    status.textContent = 'Could not save the shared folder. Please try again.';
                    return;
                }

                window.location.href = '/local/admin';
            } catch {
                status.className = 'status error';
                status.textContent = 'Could not save the shared folder. Please try again.';
            }
        }

        document.getElementById('changeStorageRootButton').addEventListener('click', changeStorageRoot);
    </script>
</body>
</html>
""";

        return Content(html, "text/html", Encoding.UTF8);
    }

    [HttpGet("local/settings")]
    public IActionResult SettingsPage()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        var html = """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>LAN Portal | Settings</title>
    <link rel="stylesheet" href="/host.css?v=1" />
</head>
<body>
    <div class="shell">
        <header class="page-header">
            <p class="eyebrow">Settings</p>
        </header>
    </div>
</body>
</html>
""";

        return Content(html, "text/html", Encoding.UTF8);
    }

    [HttpGet("local/advanced")]
    public IActionResult AdvancedPage()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

                var guestLoginUrl = BuildGuestLoginUrl();
                var customGuestLoginUrl = BuildCustomGuestLoginUrl();
                var guestDnsStatus = EvaluateGuestDnsStatus();

                var html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>LAN Portal | Advanced</title>
    <link rel="stylesheet" href="/host.css?v=1" />
</head>
<body>
  <div class="shell">
        <header class="page-header">
            <p class="eyebrow">Advanced</p>
        </header>

        <main class="advanced-sections">
            <section class="advanced-section" data-section="customize-url">
                <button class="advanced-section-header" type="button" aria-expanded="false" aria-controls="customize-url-content">
                    <span class="advanced-section-marker" aria-hidden="true">&gt;</span>
                    <span>Customize URL</span>
                </button>
                <div id="customize-url-content" class="advanced-section-content" hidden>
                    <p class="sub">Use these options only if you want to replace the default local URL with a custom LAN-friendly name.</p>
                    <div class="section">
                        <div class="label">Recommended guest URL</div>
                        <div class="url">{{guestLoginUrl}}</div>
                        <div class="qr">
                            <img src="/api/local/setup/guest-login-qr.svg" alt="Guest access QR code" />
                        </div>
                    </div>
                    <div class="section">
                        <div class="label">Optional custom URL</div>
                        <div class="url">{{customGuestLoginUrl}}</div>
                        <div class="status {{(guestDnsStatus.IsConfigured ? "ok" : "warn")}}">{{guestDnsStatus.Message}}</div>
                    </div>
                    <div class="section">
                        <div class="label">How to customize it</div>
                        <ol class="steps">
                            <li>Create a DHCP reservation so this host keeps the same LAN IP.</li>
                            <li>In your router DNS settings, add an A record: lan.home.arpa → this host LAN IP.</li>
                            <li>Reconnect guest devices to Wi‑Fi or toggle Wi‑Fi so they pick up updated DNS.</li>
                            <li>Share <span class="url">{{customGuestLoginUrl}}</span> instead of the default URL.</li>
                        </ol>
                    </div>
                </div>
            </section>

            <section class="advanced-section" data-section="access-history">
                <button class="advanced-section-header" type="button" aria-expanded="false" aria-controls="access-history-content">
                    <span class="advanced-section-marker" aria-hidden="true">&gt;</span>
                    <span>Access History</span>
                </button>
                <div id="access-history-content" class="advanced-section-content" hidden>
                    <div id="recentContainer" class="muted">Loading...</div>
                </div>
            </section>

            <section class="advanced-section" data-section="logs">
                <button class="advanced-section-header" type="button" aria-expanded="false" aria-controls="logs-content">
                    <span class="advanced-section-marker" aria-hidden="true">&gt;</span>
                    <span>Logs</span>
                </button>
                <div id="logs-content" class="advanced-section-content" hidden>
                    <p class="sub">Logs will be available here.</p>
                </div>
            </section>

            <section class="advanced-section" data-section="security">
                <button class="advanced-section-header" type="button" aria-expanded="false" aria-controls="security-content">
                    <span class="advanced-section-marker" aria-hidden="true">&gt;</span>
                    <span>Security</span>
                </button>
                <div id="security-content" class="advanced-section-content" hidden>
                    <p class="sub">Security controls will be available here.</p>
                </div>
            </section>
        </main>
  </div>
<script>
const pageKey = 'advanced';

async function loadSectionState() {
    try {
        const response = await fetch(`/api/local/ui-state?page=${encodeURIComponent(pageKey)}`);
        if (!response.ok) return;
        const state = await response.json();
        for (const section of document.querySelectorAll('[data-section]')) {
            const key = section.dataset.section;
            if (state[key] === true) setSectionExpanded(section, true, false);
        }
    } catch {
    }
}

async function saveSectionState(section, isExpanded) {
    try {
        await fetch('/api/local/ui-state', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ pageKey, sectionKey: section.dataset.section, isExpanded })
        });
    } catch {
    }
}

function setSectionExpanded(section, isExpanded, persist) {
    const header = section.querySelector('.advanced-section-header');
    const content = section.querySelector('.advanced-section-content');
    header.setAttribute('aria-expanded', String(isExpanded));
    content.hidden = !isExpanded;
    section.classList.toggle('expanded', isExpanded);
    if (persist) saveSectionState(section, isExpanded);
    if (section.dataset.section === 'access-history' && isExpanded) loadRecent();
}

for (const section of document.querySelectorAll('[data-section]')) {
    section.querySelector('.advanced-section-header').addEventListener('click', () => {
        const isExpanded = section.querySelector('.advanced-section-header').getAttribute('aria-expanded') === 'true';
        setSectionExpanded(section, !isExpanded, true);
    });
}

async function loadRecent() {
    const container = document.getElementById('recentContainer');
    if (container.dataset.loaded === 'true') return;
    try {
        const response = await fetch('/api/local/approvals/recent');
        if (!response.ok) throw new Error('Request failed');
        const rows = await response.json();
        if (!rows.length) {
            container.innerText = 'No recent decisions.';
            container.dataset.loaded = 'true';
            return;
        }
        let html = '<table><thead><tr><th>Time</th><th>Device</th><th>Decision</th><th>Details</th></tr></thead><tbody>';
        for (const item of rows) {
            html += `<tr><td>${formatLocalDateTime(item.decidedAtUtc)}</td><td>${item.deviceName}</td><td>${item.decision}</td><td><div>User: ${item.userName ?? '(n/a)'}</div><div>Roles: ${item.roles ?? '(n/a)'}</div><div>Reason: ${item.reason ?? '(n/a)'}</div></td></tr>`;
        }
        container.innerHTML = html + '</tbody></table>';
        container.dataset.loaded = 'true';
    } catch {
        container.innerText = 'Access history is unavailable.';
    }
}

function formatLocalDateTime(value) {
    if (!value) return 'Never';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? 'Unknown' : date.toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
}

loadSectionState();
</script>
</body>
</html>
""";

        return Content(html, "text/html", Encoding.UTF8);
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
            ? $"http://{GuestLoginHostName}/"
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

    public sealed record PickStorageRootRequest(string? CurrentPath);

    public sealed record PickStorageRootResponse(string StorageRootPath);

    public sealed record HostUiStateRequest(string PageKey, string SectionKey, bool IsExpanded);

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
