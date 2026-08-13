using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Ignyos.LanPortal.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ignyos.LanPortal.Api.Controllers;

[ApiController]
public sealed class LocalAdminController(IAppSettingsStore settingsStore, IDeviceLoginStore loginStore) : ControllerBase
{
    private const string GuestLoginHostName = "lan.home.arpa";

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
        var displayVersion = GetDisplayVersion();

        var html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>My Home Admin</title>
  <style>
    :root { --bg: #f5f7f8; --card: #ffffff; --ink: #0f1a20; --muted: #6a747a; --accent: #0a6c74; --line: #d9e1e4; }
    body { font-family: Segoe UI, sans-serif; margin: 0; background: linear-gradient(180deg,#f3f7f8,#edf3f5); color: var(--ink); padding-bottom: 52px; }
    .shell { max-width: 1200px; margin: 20px auto; padding: 0 16px 20px; }
    .header { background: var(--card); border: 1px solid var(--line); border-radius: 12px; padding: 16px; }
    .card { background: var(--card); border: 1px solid var(--line); border-radius: 12px; padding: 16px; }
    h2 { margin: 0 0 10px; font-size: 18px; }
    table { border-collapse: collapse; width: 100%; font-size: 14px; }
    th, td { border: 1px solid var(--line); padding: 8px; text-align: left; vertical-align: top; }
    input { width: 100%; box-sizing: border-box; padding: 7px; }
    select { width: 100%; box-sizing: border-box; padding: 7px; }
    label { display: block; margin-bottom: 4px; font-size: 12px; color: var(--muted); }
    button { padding: 7px 12px; margin-right: 6px; border: 1px solid var(--line); border-radius: 8px; background: #fff; cursor: pointer; }
    button.primary { border-color: var(--accent); color: #fff; background: var(--accent); }
    .muted { color: var(--muted); font-size: 13px; }
    .status { margin-top: 10px; }
    .ok { color: #116b2f; }
    .warn { color: #9d5d00; }
    .guest { margin-top: 16px; padding: 14px; border-radius: 10px; background: #f7fafb; border: 1px solid var(--line); display: flex; gap: 16px; align-items: center; flex-wrap: wrap; }
    .guest img { width: 156px; height: 156px; border: 1px solid var(--line); border-radius: 8px; background: #fff; padding: 8px; }
    .guest-url { font-family: Consolas, Menlo, monospace; font-size: 14px; word-break: break-all; }
    .banner { margin-top: 10px; padding: 10px 12px; border-radius: 10px; background: #f7fafb; border: 1px solid var(--line); }
    .banner.ok { color: #116b2f; background: #eef8f1; border-color: #cfe9d7; }
    .banner.warn { color: #9d5d00; background: #fff8eb; border-color: #f1dfb7; }
    details.router-help { margin-top: 10px; }
    details.router-help summary { cursor: pointer; font-weight: 600; }
    .sticky-footer { position: fixed; left: 0; right: 0; bottom: 0; background: #ffffff; border-top: 1px solid var(--line); color: var(--muted); font-size: 13px; padding: 8px 16px; }
  </style>
</head>
<body>
  <div class="shell">
    <div class="header">
      <h1>My Home Admin</h1>
      <div class="guest">
        <img src="/api/local/setup/guest-login-qr.svg" alt="Guest login QR code" />
        <div>
          <div><strong>Recommended guest URL</strong></div>
          <div class="guest-url">{{guestLoginUrl}}</div>
          <div class="muted" style="margin-top:6px;">Guests on the same Wi-Fi can scan this QR code to open login. No router changes required.</div>
          <div class="banner {{(guestDnsStatus.IsConfigured ? "ok" : "warn")}}">{{guestDnsStatus.Message}}</div>
          <div class="muted" style="margin-top:8px;"><strong>Optional custom URL:</strong> {{customGuestLoginUrl}}</div>
          <details class="router-help">
            <summary>How to customize a URL to replace the default {{guestLoginUrl}}</summary>
            <ol style="margin-top:8px; padding-left:20px;">
              <li>Create a DHCP reservation so this host keeps the same LAN IP.</li>
              <li>In your router DNS settings, add an A record: lan.home.arpa -> this host LAN IP.</li>
              <li>Reconnect guest devices to Wi-Fi (or toggle Wi-Fi) so they pick up updated DNS.</li>
              <li>Share <span class="guest-url">{{customGuestLoginUrl}}</span> instead of the default IP link.</li>
            </ol>
          </details>
        </div>
      </div>
    </div>

    <section class="card" style="margin-top:16px;">
      <h2>Pending Approvals</h2>
      <div id="pendingContainer" class="muted">Loading...</div>
    </section>

    <section class="card" style="margin-top:16px;">
      <h2>Logged In Users</h2>
      <div class="muted" style="margin-bottom:8px;">Revoke by user/device or revoke a single session.</div>
      <div style="display:grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-top: 0;">
        <div>
          <label for="revokeUserName">User Name</label>
          <input id="revokeUserName" placeholder="home-user" />
        </div>
        <div>
          <label for="revokeDeviceName">Device Name</label>
          <input id="revokeDeviceName" placeholder="Device-Browser" />
        </div>
      </div>
      <div style="margin-top:8px;">
        <button onclick="revokeByFilter()">Revoke Matching Sessions</button>
      </div>
      <div id="sessionsStatus" class="status"></div>
      <div id="activeSessionsContainer" class="muted" style="margin-top:10px;">Loading...</div>
    </section>
  </div>
  <footer class="sticky-footer">Version {{displayVersion}}</footer>

<script>
async function getJson(url) {
  const res = await fetch(url);
  if (!res.ok) {
    throw new Error(`Request failed: ${url} (${res.status})`);
  }
  return await res.json();
}

const approvalDrafts = new Map();
let pendingSignature = '';
let pendingRendered = false;

function createDefaultDraft(item) {
  return {
    userName: (item.requestedUserName && item.requestedUserName.trim().length > 0)
      ? item.requestedUserName.trim()
      : 'home-user',
    roles: ['User'],
    expiryOption: '60',
    customHours: 24
  };
}

function capturePendingDrafts() {
  const rows = document.querySelectorAll('#pendingContainer tbody tr[data-request-id]');
  for (const row of rows) {
    const id = row.getAttribute('data-request-id');
    if (!id) {
      continue;
    }

    const userNameInput = document.getElementById(`user-${id}`);
    const rolesSelect = document.getElementById(`roles-${id}`);
    const expirySelect = document.getElementById(`expiry-${id}`);
    const customHoursInput = document.getElementById(`custom-hours-${id}`);

    if (!userNameInput || !rolesSelect || !expirySelect || !customHoursInput) {
      continue;
    }

    approvalDrafts.set(id, {
      userName: userNameInput.value,
      roles: Array.from(rolesSelect.selectedOptions).map(option => option.value).filter(Boolean),
      expiryOption: expirySelect.value,
      customHours: Number(customHoursInput.value) || 24
    });
  }
}

function applyDraft(id, item) {
  const draft = approvalDrafts.get(id) ?? createDefaultDraft(item);
  approvalDrafts.set(id, draft);

  const userNameInput = document.getElementById(`user-${id}`);
  const rolesSelect = document.getElementById(`roles-${id}`);
  const expirySelect = document.getElementById(`expiry-${id}`);
  const customHoursInput = document.getElementById(`custom-hours-${id}`);

  if (!userNameInput || !rolesSelect || !expirySelect || !customHoursInput) {
    return;
  }

  userNameInput.value = draft.userName || '';
  for (const option of rolesSelect.options) {
    option.selected = draft.roles.includes(option.value);
  }

  expirySelect.value = draft.expiryOption || '60';
  customHoursInput.value = String(Math.max(1, Math.min(87600, draft.customHours || 24)));
  toggleCustomHours(id);
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

  let html = '<table><thead><tr><th>Device</th><th>Source</th><th>Requested</th><th>Actions</th></tr></thead><tbody>';
  for (const item of rows) {
    html += `<tr data-request-id="${item.requestId}">
      <td><div>${item.deviceName}</div><div class="muted">${item.requestId}</div></td>
      <td><div>${item.sourceIp ?? ''}</div><div class="muted">${item.userAgent ?? ''}</div></td>
      <td>
        <div>User: ${item.requestedUserName}</div>
        <div class="muted">Created: ${item.createdAtUtc}</div>
      </td>
      <td>
        <label for="user-${item.requestId}">User Name</label>
        <input id="user-${item.requestId}" placeholder="home-user" value="" />
        <label for="roles-${item.requestId}" style="margin-top:6px;">Roles</label>
        <select id="roles-${item.requestId}" multiple size="2">
          <option value="User">User</option>
          <option value="Admin">Admin</option>
        </select>
        <div class="muted" style="margin-top:4px;">Hold Ctrl (or Cmd on Mac) to select multiple roles.</div>
        <label for="expiry-${item.requestId}" style="margin-top:6px;">Token Expiration</label>
        <select id="expiry-${item.requestId}" onchange="toggleCustomHours('${item.requestId}')">
          <option value="60">1 hour</option>
          <option value="1440">1 day</option>
          <option value="10080">1 week</option>
          <option value="never">Never</option>
          <option value="custom">Custom</option>
        </select>
        <div id="custom-hours-wrap-${item.requestId}" style="display:none; margin-top:6px;">
          <label for="custom-hours-${item.requestId}">Custom (hours)</label>
          <input id="custom-hours-${item.requestId}" type="number" min="1" max="87600" value="" />
        </div>
        <div style="margin-top:8px;">
          <button class="primary" onclick="approve('${item.requestId}')">Approve</button>
          <button onclick="deny('${item.requestId}')">Deny</button>
        </div>
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

function toggleCustomHours(id) {
  const expiry = document.getElementById(`expiry-${id}`);
  const customWrap = document.getElementById(`custom-hours-wrap-${id}`);
  customWrap.style.display = expiry.value === 'custom' ? '' : 'none';
}

async function loadActiveSessions() {
  const rows = await getJson('/api/local/admin/sessions/active');
  const container = document.getElementById('activeSessionsContainer');

  if (!rows.length) {
    container.innerText = 'No active sessions.';
    return;
  }

  let html = '<table><thead><tr><th>User</th><th>Device</th><th>Roles</th><th>Issued</th><th>Expires</th><th>Last Seen</th><th>Action</th></tr></thead><tbody>';
  for (const item of rows) {
    const normalizedRoles = (item.roles || '').split(',').map(value => value.trim()).filter(Boolean);
    const userSelected = normalizedRoles.some(role => role.toLowerCase() === 'user') ? 'selected' : '';
    const adminSelected = normalizedRoles.some(role => role.toLowerCase() === 'admin') ? 'selected' : '';

    html += `<tr>
      <td>${item.userName}</td>
      <td>${item.deviceName}</td>
      <td>
        <select id="session-roles-${item.sessionId}" multiple size="2">
          <option value="User" ${userSelected}>User</option>
          <option value="Admin" ${adminSelected}>Admin</option>
        </select>
        <div class="muted" style="margin-top:4px;">Hold Ctrl (or Cmd on Mac) to select multiple roles.</div>
      </td>
      <td>${item.issuedAtUtc}</td>
      <td>${item.expiresAtUtc ?? 'Never'}</td>
      <td>${item.lastSeenAtUtc}</td>
      <td>
        <button onclick="updateSessionRoles('${item.sessionId}')">Save Roles</button>
        <button onclick="revokeSession('${item.sessionId}')">Revoke</button>
      </td>
    </tr>`;
  }
  html += '</tbody></table>';
  container.innerHTML = html;
}

function getSelectedRoles(sessionId) {
  const select = document.getElementById(`session-roles-${sessionId}`);
  if (!select) {
    return ['User'];
  }

  const roles = Array.from(select.selectedOptions)
    .map(option => option.value)
    .filter(Boolean);

  return roles.length ? roles : ['User'];
}

async function updateSessionRoles(sessionId) {
  const roles = getSelectedRoles(sessionId).join(',');

  const response = await fetch(`/api/local/admin/sessions/${sessionId}/roles`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ roles, reason: 'Roles updated by local admin operator.' })
  });

  const status = document.getElementById('sessionsStatus');
  if (!response.ok) {
    status.className = 'status warn';
    status.innerText = 'Failed to update roles.';
    return;
  }

  status.className = 'status ok';
  status.innerText = 'Roles updated. Changes apply on the user\'s next action.';
  await Promise.all([loadPending(), loadActiveSessions()]);
}

async function revokeSession(sessionId) {
  const response = await fetch(`/api/local/admin/sessions/${sessionId}/revoke`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason: 'Revoked by admin operator.' })
  });

  const status = document.getElementById('sessionsStatus');
  if (!response.ok) {
    status.className = 'status warn';
    status.innerText = 'Failed to revoke session.';
    return;
  }

  status.className = 'status ok';
  status.innerText = 'Session revoked.';
  await Promise.all([loadPending(), loadActiveSessions()]);
}

async function revokeByFilter() {
  const userName = document.getElementById('revokeUserName').value;
  const deviceName = document.getElementById('revokeDeviceName').value;

  const response = await fetch('/api/local/admin/sessions/revoke-by-filter', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userName, deviceName, reason: 'Revoked by admin operator.' })
  });

  const status = document.getElementById('sessionsStatus');
  if (!response.ok) {
    status.className = 'status warn';
    status.innerText = 'Failed to revoke matching sessions.';
    return;
  }

  const result = await response.json();
  status.className = 'status ok';
  status.innerText = `Revoked ${result.revokedCount} session(s).`;
  await Promise.all([loadPending(), loadActiveSessions()]);
}

async function approve(id) {
  const userName = document.getElementById(`user-${id}`).value;
  const roleSelect = document.getElementById(`roles-${id}`);
  const selectedRoles = Array.from(roleSelect.selectedOptions).map(option => option.value).filter(Boolean);
  const roles = selectedRoles.length ? selectedRoles.join(',') : 'User';
  const expiryOption = document.getElementById(`expiry-${id}`).value;
  const customHours = Number(document.getElementById(`custom-hours-${id}`).value);
  let tokenMinutes = 60;

  if (expiryOption === 'custom') {
    tokenMinutes = Math.max(1, Math.min(87600, customHours)) * 60;
  } else if (expiryOption === 'never') {
    tokenMinutes = null;
  } else {
    tokenMinutes = Number(expiryOption);
  }

  await fetch(`/api/local/approvals/${id}/approve`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userName, roles, tokenMinutes })
  });

  await Promise.all([loadPending(), loadActiveSessions()]);
}

async function deny(id) {
  await fetch(`/api/local/approvals/${id}/deny`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason: 'Denied by host operator.' })
  });

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

        var displayVersion = GetDisplayVersion();

        var html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>My Home Access History</title>
  <style>
    :root { --bg: #f5f7f8; --card: #ffffff; --ink: #0f1a20; --muted: #6a747a; --accent: #0a6c74; --line: #d9e1e4; }
    body { font-family: Segoe UI, sans-serif; margin: 0; background: linear-gradient(180deg,#f3f7f8,#edf3f5); color: var(--ink); padding-bottom: 52px; }
    .shell { max-width: 1200px; margin: 20px auto; padding: 0 16px 20px; }
    .header { background: var(--card); border: 1px solid var(--line); border-radius: 12px; padding: 16px; }
    .sub { color: var(--muted); margin-top: 6px; }
    .card { background: var(--card); border: 1px solid var(--line); border-radius: 12px; padding: 16px; margin-top: 16px; }
    table { border-collapse: collapse; width: 100%; font-size: 14px; }
    th, td { border: 1px solid var(--line); padding: 8px; text-align: left; vertical-align: top; }
    .muted { color: var(--muted); font-size: 13px; }
    .sticky-footer { position: fixed; left: 0; right: 0; bottom: 0; background: #ffffff; border-top: 1px solid var(--line); color: var(--muted); font-size: 13px; padding: 8px 16px; }
  </style>
</head>
<body>
  <div class="shell">
    <div class="header">
      <h1>My Home Access History</h1>
      <div class="sub">Review recent approval and denial decisions.</div>
    </div>

    <section class="card">
      <h2 style="margin-top:0;">Recent Decisions</h2>
      <div id="recentContainer" class="muted">Loading...</div>
    </section>
  </div>
  <footer class="sticky-footer">Version {{displayVersion}}</footer>

<script>
async function getJson(url) {
  const res = await fetch(url);
  if (!res.ok) {
    throw new Error(`Request failed: ${url} (${res.status})`);
  }
  return await res.json();
}

async function loadRecent() {
  const rows = await getJson('/api/local/approvals/recent');
  const container = document.getElementById('recentContainer');

  if (!rows.length) {
    container.innerText = 'No recent decisions.';
    return;
  }

  let html = '<table><thead><tr><th>Time (UTC)</th><th>Device</th><th>Decision</th><th>Details</th></tr></thead><tbody>';
  for (const item of rows) {
    html += `<tr>
      <td>${item.decidedAtUtc}</td>
      <td>${item.deviceName}</td>
      <td>${item.decision}</td>
      <td>
        <div>User: ${item.userName ?? '(n/a)'}</div>
        <div>Roles: ${item.roles ?? '(n/a)'}</div>
        <div>Reason: ${item.reason ?? '(n/a)'}</div>
      </td>
    </tr>`;
  }
  html += '</tbody></table>';
  container.innerHTML = html;
}

setInterval(loadRecent, 3000);
loadRecent();
</script>
</body>
</html>
""";

        return Content(html, "text/html", Encoding.UTF8);
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

        var revoked = settingsStore.RevokeAccessSession(sessionId, reason);
        return revoked ? Ok() : NotFound();
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

        var revokedCount = settingsStore.RevokeAccessByUserDevice(request.UserName, request.DeviceName, reason);
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
        return string.IsNullOrWhiteSpace(lanIp)
          ? $"http://{GuestLoginHostName}/login"
          : $"http://{lanIp}/login";
    }

    private static string BuildCustomGuestLoginUrl()
    {
        return $"http://{GuestLoginHostName}/login";
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

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational;
        }

        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
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
