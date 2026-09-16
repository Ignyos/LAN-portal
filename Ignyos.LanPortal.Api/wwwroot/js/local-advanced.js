const pageKey = 'advanced';

async function loadSectionState() {
    try {
        const response = await fetch(`/api/local/ui-state?page=${encodeURIComponent(pageKey)}`);
        if (!response.ok) return;
        const state = await response.json();
        for (const section of document.querySelectorAll('[data-section]')) {
            if (state[section.dataset.section] === true) setSectionExpanded(section, true, false);
        }
    } catch { }
}

async function saveSectionState(section, isExpanded) {
    try {
        await fetch('/api/local/ui-state', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ pageKey, sectionKey: section.dataset.section, isExpanded }) });
    } catch { }
}

function setSectionExpanded(section, isExpanded, persist) {
    const header = section.querySelector('.advanced-section-header');
    const content = section.querySelector('.advanced-section-content');
    header.setAttribute('aria-expanded', String(isExpanded));
    content.hidden = !isExpanded;
    section.classList.toggle('expanded', isExpanded);
    if (persist) saveSectionState(section, isExpanded);
    if (section.dataset.section === 'access-history' && isExpanded) loadRecent();
    if (section.dataset.section === 'logs' && isExpanded) loadLogs();
}

for (const section of document.querySelectorAll('[data-section]')) {
    section.querySelector('.advanced-section-header').addEventListener('click', () => {
        const expanded = section.querySelector('.advanced-section-header').getAttribute('aria-expanded') === 'true';
        setSectionExpanded(section, !expanded, true);
    });
}

document.getElementById('logsSeverityFilter')?.addEventListener('change', loadLogs);
document.getElementById('logsCategoryFilter')?.addEventListener('change', loadLogs);
document.getElementById('logsRefreshButton')?.addEventListener('click', loadLogs);

async function loadRecent() {
    const container = document.getElementById('recentContainer');
    if (container.dataset.loaded === 'true') return;
    try {
        const response = await fetch('/api/local/access-history');
        if (!response.ok) throw new Error('Request failed');
        const rows = await response.json();
        if (!rows.length) { container.innerText = 'No recent decisions.'; container.dataset.loaded = 'true'; return; }
        let html = '<table><thead><tr><th>Time</th><th>User</th><th>Device</th><th>Action</th><th>Reason</th></tr></thead><tbody>';
        for (const item of rows) html += `<tr><td>${formatLocalDateTime(item.occurredAtUtc)}</td><td>${escapeHtml(item.userName ?? '(n/a)')}</td><td>${escapeHtml(item.deviceName)}</td><td>${escapeHtml(item.eventType)}</td><td>${escapeHtml(item.reason ?? '(n/a)')}</td></tr>`;
        container.innerHTML = html + '</tbody></table>'; container.dataset.loaded = 'true';
    } catch { container.innerText = 'Access history is unavailable.'; }
}

async function loadLogs() {
    const container = document.getElementById('logsContainer');
    const params = new URLSearchParams({ maxCount: '50' });
    const severity = document.getElementById('logsSeverityFilter')?.value ?? '';
    const category = document.getElementById('logsCategoryFilter')?.value ?? '';
    if (severity) params.set('severity', severity);
    if (category) params.set('category', category);
    try {
        const response = await fetch(`/api/local/logs?${params.toString()}`);
        if (!response.ok) throw new Error('Request failed');
        const rows = await response.json();
        if (!rows.length) { container.innerText = 'No application logs yet.'; container.dataset.loaded = 'true'; return; }
        let html = '<table><thead><tr><th>Time</th><th>Severity</th><th>Category</th><th>Source</th><th>Message</th><th>Details</th></tr></thead><tbody>';
        for (const item of rows) html += `<tr><td>${formatLocalDateTime(item.occurredAtUtc)}</td><td>${escapeHtml(item.severity)}</td><td>${escapeHtml(item.category)}</td><td>${escapeHtml(item.source ?? 'unknown')}</td><td>${escapeHtml(item.message ?? '(no message)')}</td><td>${escapeHtml(formatLogDetails(item))}</td></tr>`;
        container.innerHTML = html + '</tbody></table>'; container.dataset.loaded = 'true';
    } catch { container.innerText = 'Application logs are unavailable.'; }
}

function formatLocalDateTime(value) { if (!value) return 'Never'; const date = new Date(value); return Number.isNaN(date.getTime()) ? 'Unknown' : date.toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' }); }
function escapeHtml(value) { return value === null || value === undefined ? '' : String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#39;'); }
function formatLogDetails(item) { return [item.exceptionType, item.exceptionMessage, item.detailsJson, item.correlationId ? `correlation: ${item.correlationId}` : ''].filter(Boolean).join(' | '); }

async function rotateSigningKey() {
    const status = document.getElementById('securityStatus');
    if (!confirm('Rotate the JWT signing key? This will immediately sign out all currently logged-in users, who will need to request access again.')) return;
    const button = document.getElementById('rotateSigningKeyButton'); button.disabled = true; status.innerText = 'Rotating signing key...';
    try { const response = await fetch('/api/local/security/rotate-signing-key', { method: 'POST' }); const result = await response.json(); if (!response.ok) throw new Error(result?.message || 'Rotation failed.'); status.innerText = `Signing key rotated at ${formatLocalDateTime(result.rotatedAtUtc)}. ${result.revokedSessionCount} active session(s) invalidated. Fingerprint: ${result.keyFingerprint}`; }
    catch (error) { status.innerText = error.message || 'Signing key rotation failed.'; }
    finally { button.disabled = false; }
}

document.getElementById('rotateSigningKeyButton')?.addEventListener('click', rotateSigningKey);
loadSectionState();
