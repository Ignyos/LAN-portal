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

document.getElementById('changeStorageRootButton')?.addEventListener('click', changeStorageRoot);
document.getElementById('openAdminButton')?.addEventListener('click', openAdminConsole);
