using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;
using Ignyos.LanPortal.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ignyos.LanPortal.Api.Controllers;

[ApiController]
public sealed class LocalAdminController(
  IAppSettingsStore settingsStore,
  IDeviceLoginStore loginStore,
  IAccessHistoryStore accessHistoryStore,
  ISessionLifecycleService sessionLifecycleService) : Controller
{
    private const string GuestLoginHostName = "lan.home.arpa";
  private const int DevelopmentGuestLoginPort = 5014;

    [HttpGet("local/admin")]
    public IActionResult AdminPage()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        var model = new LocalAdminPageViewModel(
            BuildGuestLoginUrl(),
            BuildCustomGuestLoginUrl(),
            EvaluateGuestDnsStatus(),
            System.Text.Json.JsonSerializer.Serialize(
                Contracts.TokenExpiryOptions.All.Select(option => new { value = option.Value, label = option.Label })));

        return View(model);
    }

    [HttpGet("local/access-history")]
    public IActionResult AccessHistoryPage()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        return Redirect("/local/advanced#access-history");
    }

      [HttpGet("api/local/access-history")]
      public ActionResult<IReadOnlyList<AccessHistoryRecord>> AccessHistory()
      {
        if (!IsLocalRequest(HttpContext))
        {
          return NotFound();
        }

        return Ok(accessHistoryStore.GetRecent());
      }

    [HttpGet("api/local/admin/overview")]
    public IActionResult Overview()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        var version = GetDisplayVersion();
        return Ok(new
        {
            Status = "Running",
            Version = version,
            PendingRequests = loginStore.GetPendingRequests().Count,
            RecentDecisions = loginStore.GetRecentDecisions().Count,
            ActiveSessions = settingsStore.GetActiveAccessSessions(250).Count,
            SetupComplete = settingsStore.IsSetupComplete()
        });
    }

    [HttpGet("api/local/admin/sessions/active")]
    public ActionResult<IReadOnlyList<AccessSessionRecord>> ActiveSessions()
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        return Ok(settingsStore.GetActiveAccessSessions());
    }

    [HttpPost("api/local/admin/sessions/{sessionId:guid}/revoke")]
    public IActionResult RevokeSession(Guid sessionId, [FromBody] RevokeSessionRequest request)
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason)
          ? "Revoked by local admin operator."
          : request.Reason.Trim();

        return sessionLifecycleService.Revoke(sessionId, reason) is not null ? Ok() : NotFound();
    }

    [HttpPost("api/local/admin/sessions/revoke-by-filter")]
    public IActionResult RevokeByFilter([FromBody] RevokeByFilterRequest request)
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.UserName) && string.IsNullOrWhiteSpace(request.DeviceName))
        {
            return BadRequest("Provide UserName and/or DeviceName.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason)
          ? "Revoked by local admin operator."
          : request.Reason.Trim();

        var revokedCount = sessionLifecycleService.RevokeByFilter(request.UserName, request.DeviceName, reason).Count;
        return Ok(new { RevokedCount = revokedCount });
    }

    [HttpPost("api/local/admin/sessions/{sessionId:guid}/roles")]
    public IActionResult UpdateSessionRoles(Guid sessionId, [FromBody] UpdateSessionRolesRequest request)
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        var normalizedRoles = NormalizeRoles(request.Roles);
        if (string.IsNullOrWhiteSpace(normalizedRoles))
        {
            return BadRequest("At least one valid role is required.");
        }

        var changed = settingsStore.UpdateAccessSessionRoles(
          sessionId,
          normalizedRoles,
          "local-admin",
          string.IsNullOrWhiteSpace(request.Reason) ? "Roles updated by local admin operator." : request.Reason.Trim(),
          DateTimeOffset.UtcNow);

        return changed ? Ok(new { SessionId = sessionId, Roles = normalizedRoles }) : NotFound();
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

    public sealed record RevokeSessionRequest(string? Reason);

    public sealed record RevokeByFilterRequest(string? UserName, string? DeviceName, string? Reason);

    public sealed record UpdateSessionRolesRequest(string Roles, string? Reason);

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

    private static string NormalizeRoles(string roles)
    {
        var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "User", "Admin" };

        var values = roles
          .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
          .Where(role => allowedRoles.Contains(role))
          .Select(role => role.Equals("admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "User")
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
          .ToArray();

        return values.Length == 0 ? string.Empty : string.Join(',', values);
    }
}

