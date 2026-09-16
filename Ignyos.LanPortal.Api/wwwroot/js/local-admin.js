async function getJson(url) {
  const res = await fetch(url);
  if (!res.ok) {
    throw new Error(`Request failed: ${url} (${res.status})`);
  }
  return await res.json();
}

const approvalDrafts = new Map();
const busyRequests = new Set();
const TOKEN_EXPIRY_OPTIONS = JSON.parse(document.getElementById('tokenExpiryOptions')?.textContent || '[]');
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
    if (!id) continue;

    const el = draftInputs(id);
    if (!el.userName || !el.deviceName || !el.admin || !el.expiry || !el.customHours) continue;

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
  if (!el.userName || !el.deviceName || !el.admin || !el.expiry || !el.customHours) return;

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
  if (!el.userName || !el.deviceName || !approveButton) return;

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
  if (!row) return;

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
    if (!activeIds.has(key)) approvalDrafts.delete(key);
  }

  if (!rows.length) {
    pendingSignature = '';
    pendingRendered = false;
    container.innerText = 'No pending requests.';
    return;
  }

  const nextSignature = rows.map(item => `${item.requestId}|${item.createdAtUtc}|${item.expiresAtUtc}`).join(';');
  if (pendingRendered && nextSignature === pendingSignature) return;

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
  if (!value) return 'Never';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'Never';
  const pad = (n) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function formatFriendlyDateTime(value) {
  if (!value) return 'Never';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'Never';
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
  if (!rawRoles) return ['User'];

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

  const previous = approvalDrafts.get(id);
  const roleSet = new Set((previous?.roles ?? ['User']).filter(role => role !== 'Admin'));
  if (el.admin.checked) roleSet.add('Admin');
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
  if (!id) return;

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
