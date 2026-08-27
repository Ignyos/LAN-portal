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
  ISessionLifecycleService sessionLifecycleService) : ControllerBase
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

        var guestLoginUrl = BuildGuestLoginUrl();
        var customGuestLoginUrl = BuildCustomGuestLoginUrl();
        var guestDnsStatus = EvaluateGuestDnsStatus();
        var tokenExpiryOptionsJson = System.Text.Json.JsonSerializer.Serialize(
            Contracts.TokenExpiryOptions.All.Select(option => new { value = option.Value, label = option.Label }));
        var html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Admin</title>
  <link rel="stylesheet" href="/host.css?v=2" />
</head>
<body>
  <div class="shell">
    <header class="page-header">
      <p class="eyebrow">Admin</p>
    </header>

    <!-- <section class="card" style="margin-top:16px;">
      <h1>My Home</h1>
    </section> -->

    <section class="card" style="margin-top:16px;">
      <h2>Pending Approvals</h2>
      <div id="pendingContainer" class="muted">Loading...</div>
    </section>

    <section class="card" style="margin-top:16px;">
      <h2>Manage Active Users</h2>
      <div class="muted" style="margin-bottom:8px;">Review active users and revoke access when needed.</div>
      <div id="sessionsStatus" class="status"></div>
      <div id="activeSessionsContainer" class="muted" style="margin-top:10px;">Loading...</div>
    </section>

    <div id="denyRequestModal" class="modal-backdrop" style="display:none;" role="presentation" onclick="closeDenyRequestModal()">
      <section class="modal-dialog" role="dialog" aria-modal="true" aria-labelledby="denyRequestTitle" onclick="event.stopPropagation()">
        <div class="modal-header">
          <h3 id="denyRequestTitle">Deny Request</h3>
          <button type="button" class="modal-close" aria-label="Close deny dialog" onclick="closeDenyRequestModal()">×</button>
        </div>

        <dl class="modal-details">
          <div>
            <dt>Device</dt>
            <dd id="denyRequestDevice">-</dd>
          </div>
        </dl>

        <div class="muted" style="margin-top:12px;">The request will be dismissed and the waiting device will be told access was denied.</div>

        <div style="margin-top:12px;">
          <label for="denyRequestReason">Reason (optional)</label>
          <input id="denyRequestReason" placeholder="Shared with the requesting device" />
        </div>

        <div class="modal-actions">
          <button type="button" onclick="closeDenyRequestModal()">Cancel</button>
          <button type="button" class="primary" id="confirmDenyRequest" onclick="confirmDenyRequest()">Confirm Deny</button>
        </div>
      </section>
    </div>
  </div>
<script>
async function getJson(url) {
  const res = await fetch(url);
  if (!res.ok) {
    throw new Error(`Request failed: ${url} (${res.status})`);
  }
  return await res.json();
}

const approvalDrafts = new Map();
const busyRequests = new Set();
const TOKEN_EXPIRY_OPTIONS = {{tokenExpiryOptionsJson}};
let pendingSignature = '';
let pendingRendered = false;

function createDefaultDraft(item) {
  return {
    userName: (item.requestedUserName && item.requestedUserName.trim().length > 0)
      ? item.requestedUserName.trim()
      : 'home-user',
    deviceName: (item.deviceName && item.deviceName.trim().length > 0) ? item.deviceName.trim() : '',
    roles: ['User'],
    expiryOption: '60',
    customHours: 24
  };
}

function draftInputs(id) {
  return {
    userName: document.getElementById(`user-${id}`),
    deviceName: document.getElementById(`device-${id}`),
    admin: document.getElementById(`admin-${id}`),
    expiry: document.getElementById(`expiry-${id}`),
    customHours: document.getElementById(`custom-hours-${id}`)
  };
}

function capturePendingDrafts() {
  const rows = document.querySelectorAll('#pendingContainer tbody tr[data-request-id]');
  for (const row of rows) {
    const id = row.getAttribute('data-request-id');
    if (!id) {
      continue;
    }

    const el = draftInputs(id);
    if (!el.userName || !el.deviceName || !el.admin || !el.expiry || !el.customHours) {
      continue;
    }

    // Roles the host UI has no control for are preserved so future add-in roles survive an approval.
    const previous = approvalDrafts.get(id);
    const roles = new Set((previous?.roles ?? ['User']).filter(role => role !== 'Admin'));
    if (el.admin.checked) {
      roles.add('Admin');
    }
    roles.add('User');

    approvalDrafts.set(id, {
      userName: el.userName.value,
      deviceName: el.deviceName.value,
      roles: Array.from(roles),
      expiryOption: el.expiry.value,
      customHours: Number(el.customHours.value) || 24
    });
  }
}

function applyDraft(id, item) {
  const draft = approvalDrafts.get(id) ?? createDefaultDraft(item);
  approvalDrafts.set(id, draft);

  const el = draftInputs(id);
  if (!el.userName || !el.deviceName || !el.admin || !el.expiry || !el.customHours) {
    return;
  }

  el.userName.value = draft.userName || '';
  el.deviceName.value = draft.deviceName || '';
  el.admin.checked = draft.roles.includes('Admin');
  el.expiry.value = draft.expiryOption || '60';
  el.customHours.value = String(Math.max(1, Math.min(87600, draft.customHours || 24)));
  toggleCustomHours(id);
  updateApproveState(id);
}

function updateApproveState(id) {
  const el = draftInputs(id);
  const approveButton = document.getElementById(`approve-${id}`);
  if (!el.userName || !el.deviceName || !approveButton) {
    return;
  }

  const valid = el.userName.value.trim().length > 0 && el.deviceName.value.trim().length > 0;
  approveButton.disabled = !valid || busyRequests.has(id);
}

function setRowBusy(id, busy) {
  if (busy) {
    busyRequests.add(id);
  } else {
    busyRequests.delete(id);
  }

  const row = document.querySelector(`#pendingContainer tbody tr[data-request-id="${id}"]`);
  if (!row) {
    return;
  }

  for (const control of row.querySelectorAll('input, select, button')) {
    control.disabled = busy;
  }

  if (!busy) {
    updateApproveState(id);
  }
}

async function loadPending() {
  const rows = await getJson('/api/local/approvals/pending');
  const container = document.getElementById('pendingContainer');

  capturePendingDrafts();

  const activeIds = new Set(rows.map(item => item.requestId));
  for (const key of approvalDrafts.keys()) {
    if (!activeIds.has(key)) {
      approvalDrafts.delete(key);
    }
  }

  if (!rows.length) {
    pendingSignature = '';
    pendingRendered = false;
    container.innerText = 'No pending requests.';
    return;
  }

  const nextSignature = rows
    .map(item => `${item.requestId}|${item.createdAtUtc}|${item.expiresAtUtc}`)
    .join(';');

  if (pendingRendered && nextSignature === pendingSignature) {
    return;
  }

  const expiryOptionsHtml = TOKEN_EXPIRY_OPTIONS
    .map(option => `<option value="${option.value}">${option.label}</option>`)
    .join('');

  let html = '<table><thead><tr><th>User</th><th>Device</th><th>Admin</th><th>Expiration</th><th>Actions</th></tr></thead><tbody>';
  for (const item of rows) {
    html += `<tr data-request-id="${item.requestId}">
      <td><input id="user-${item.requestId}" placeholder="home-user" value="" aria-label="Approved user name" oninput="updateApproveState('${item.requestId}')" /></td>
      <td><input id="device-${item.requestId}" placeholder="Desktop" value="" aria-label="Device name" oninput="updateApproveState('${item.requestId}')" /></td>
      <td>
        <label class="checkbox-row">
          <input id="admin-${item.requestId}" type="checkbox" aria-label="Grant administrator access" />
          <span>Admin</span>
        </label>
      </td>
      <td>
        <select id="expiry-${item.requestId}" aria-label="Token expiration" onchange="toggleCustomHours('${item.requestId}')">${expiryOptionsHtml}</select>
        <div id="custom-hours-wrap-${item.requestId}" style="display:none; margin-top:6px;">
          <label for="custom-hours-${item.requestId}">Custom (hours)</label>
          <input id="custom-hours-${item.requestId}" type="number" min="1" max="87600" value="" />
        </div>
      </td>
      <td>
        <button class="primary" id="approve-${item.requestId}" onclick="approve('${item.requestId}')">Approve</button>
        <button type="button" title="Deny request" aria-label="Deny request from ${item.deviceName}" onclick="openDenyRequestModal('${item.requestId}')">Deny</button>
      </td>
    </tr>`;
  }
  html += '</tbody></table>';
  container.innerHTML = html;

  for (const item of rows) {
    applyDraft(item.requestId, item);
  }

  pendingSignature = nextSignature;
  pendingRendered = true;
}

function formatLocalDateTime(value) {
  if (!value) {
    return 'Never';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return 'Never';
  }

  const pad = (n) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function formatFriendlyDateTime(value) {
  if (!value) {
    return 'Never';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return 'Never';
  }

  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const month = months[date.getMonth()];
  const day = date.getDate();
  const year = date.getFullYear();
  const hour24 = date.getHours();
  const hour12 = hour24 % 12 || 12;
  const minute = String(date.getMinutes()).padStart(2, '0');
  const suffix = hour24 >= 12 ? 'pm' : 'am';
  return `${month} ${day} ${year} ${hour12}:${minute}${suffix}`;
}

function toggleCustomHours(id) {
  const expiry = document.getElementById(`expiry-${id}`);
  const customWrap = document.getElementById(`custom-hours-wrap-${id}`);
  customWrap.style.display = expiry.value === 'custom' ? '' : 'none';
}

async function loadActiveSessions() {
  const rows = await getJson('/api/local/admin/sessions/active');
  const container = document.getElementById('activeSessionsContainer');
  container.dataset.sessionRows = JSON.stringify(rows);

  if (!rows.length) {
    container.innerText = 'No active sessions.';
    return;
  }

  let html = '<table><thead><tr><th>User</th><th>Device</th><th>Role</th><th>Access Expires</th><th>Revoke</th></tr></thead><tbody>';
  for (const item of rows) {
    const roles = parseRoles(item.roles);
    const hasAdmin = roles.includes('Admin');

    html += `<tr>
      <td>${item.userName}</td>
      <td>${item.deviceName}</td>
      <td>${hasAdmin ? 'Admin' : 'User'}</td>
      <td>${formatFriendlyDateTime(item.expiresAtUtc)}</td>
      <td>
        <button class="icon-btn" type="button" title="Revoke access for ${item.userName}" aria-label="Revoke access for ${item.userName}" onclick="revokeSession('${item.sessionId}')" style="opacity:1; cursor:pointer;">✕</button>
      </td>
    </tr>`;
  }
  html += '</tbody></table>';
  container.innerHTML = html;
}

function parseRoles(rawRoles) {
  if (!rawRoles) {
    return ['User'];
  }

  const values = String(rawRoles)
    .split(',')
    .map(value => value.trim())
    .filter(Boolean)
    .map(value => value.toLowerCase() === 'admin' ? 'Admin' : value.toLowerCase() === 'user' ? 'User' : value);

  return values.length ? [...new Set(values)] : ['User'];
}

async function revokeSession(sessionId) {
  const status = document.getElementById('sessionsStatus');
  if (!sessionId) {
    status.className = 'status warn';
    status.innerText = 'No session was selected to revoke.';
    return;
  }

  const response = await fetch(`/api/local/admin/sessions/${sessionId}/revoke`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason: 'Revoked by local admin operator.' })
  });

  if (!response.ok) {
    status.className = 'status warn';
    status.innerText = 'Failed to revoke session.';
    return;
  }

  status.className = 'status ok';
  status.innerText = 'Session revoked.';
  await Promise.all([loadPending(), loadActiveSessions()]);
}

async function approve(id) {
  const el = draftInputs(id);
  const userName = el.userName.value.trim();
  const deviceName = el.deviceName.value.trim();
  const status = document.getElementById('sessionsStatus');

  if (!userName || !deviceName) {
    status.className = 'status warn';
    status.innerText = 'User and Device are required before approving.';
    return;
  }

  // Roles the host UI has no control for are preserved so future add-in roles survive an approval.
  const previous = approvalDrafts.get(id);
  const roleSet = new Set((previous?.roles ?? ['User']).filter(role => role !== 'Admin'));
  if (el.admin.checked) {
    roleSet.add('Admin');
  }
  roleSet.add('User');

  const expiryOption = el.expiry.value;
  const customHours = Number(el.customHours.value);
  let tokenMinutes = 60;

  if (expiryOption === 'custom') {
    tokenMinutes = Math.max(1, Math.min(87600, customHours)) * 60;
  } else if (expiryOption === 'never') {
    tokenMinutes = null;
  } else {
    tokenMinutes = Number(expiryOption);
  }

  setRowBusy(id, true);
  try {
    const response = await fetch(`/api/local/approvals/${id}/approve`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userName, roles: Array.from(roleSet).join(','), tokenMinutes, deviceName })
    });

    if (!response.ok) {
      status.className = 'status warn';
      status.innerText = 'Failed to approve request.';
      return;
    }

    approvalDrafts.delete(id);
  } finally {
    setRowBusy(id, false);
  }

  await Promise.all([loadPending(), loadActiveSessions()]);
}

function openDenyRequestModal(id) {
  const el = draftInputs(id);
  const modal = document.getElementById('denyRequestModal');
  document.getElementById('denyRequestDevice').textContent = el.deviceName?.value || 'Unknown device';
  document.getElementById('denyRequestReason').value = '';
  modal.dataset.requestId = id;
  modal.style.display = 'flex';
}

function closeDenyRequestModal() {
  const modal = document.getElementById('denyRequestModal');
  if (modal) {
    modal.style.display = 'none';
    modal.dataset.requestId = '';
  }
}

async function confirmDenyRequest() {
  const modal = document.getElementById('denyRequestModal');
  const id = modal?.dataset.requestId;
  if (!id) {
    return;
  }

  const reason = document.getElementById('denyRequestReason').value.trim();
  closeDenyRequestModal();
  await deny(id, reason || 'Denied by host operator.');
}

async function deny(id, reason) {
  const status = document.getElementById('sessionsStatus');
  setRowBusy(id, true);
  try {
    const response = await fetch(`/api/local/approvals/${id}/deny`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reason: reason || 'Denied by host operator.' })
    });

    if (!response.ok) {
      status.className = 'status warn';
      status.innerText = 'Failed to deny request.';
      return;
    }

    approvalDrafts.delete(id);
  } finally {
    setRowBusy(id, false);
  }

  await Promise.all([loadPending(), loadActiveSessions()]);
}

async function pollPendingApprovals() {
  try {
    await loadPending();
  } catch (error) {
    console.error(error);
  }
}

async function pollActiveSessions() {
  try {
    await loadActiveSessions();
  } catch (error) {
    console.error(error);
  }
}

setInterval(pollPendingApprovals, 3000);
setInterval(pollActiveSessions, 3000);
Promise.all([pollPendingApprovals(), pollActiveSessions()]);
</script>
</body>
</html>
""";

        return Content(html, "text/html", Encoding.UTF8);
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
