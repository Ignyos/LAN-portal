const currentVersion = document.body.dataset.currentVersion ?? 'unknown';

function formatLocalDateTime(value) {
    if (!value) return 'unknown';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

async function checkForUpdates() {
    const status = document.getElementById('updateStatus');
    const button = document.getElementById('checkUpdatesButton');
    button.disabled = true;
    status.innerText = 'Checking for updates...';

    try {
        const response = await fetch('/api/local/update/check-now', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ currentVersion })
        });
        const result = await response.json();
        if (!response.ok) throw new Error(result?.message || 'Update check failed.');

        if (result.error) {
            status.innerText = `Unable to check for updates. ${result.error}`;
            return;
        }

        if (result.updateAvailable) {
            const published = result.checkedAtUtc ? ` Checked ${formatLocalDateTime(result.checkedAtUtc)}.` : '';
            status.innerText = `Update available: ${result.latestVersion}.${published}`;
            return;
        }

        status.innerText = `LAN Portal is up to date. Checked ${formatLocalDateTime(result.checkedAtUtc)}.`;
    } catch (error) {
        status.innerText = error.message || 'Update check failed.';
    } finally {
        button.disabled = false;
    }
}

document.getElementById('checkUpdatesButton')?.addEventListener('click', checkForUpdates);
