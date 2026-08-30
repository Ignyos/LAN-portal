// Uploads go straight from the browser to the API so file bytes never travel
// through the Blazor circuit or get buffered on the web server.
window.lanPortalUpload = (function () {
    const activeBatches = new Map();

    function getBatch(batchId) {
        let batch = activeBatches.get(batchId);
        if (!batch) {
            batch = { request: null, cancelled: false };
            activeBatches.set(batchId, batch);
        }

        return batch;
    }

    function getSelectedFiles(inputElement) {
        if (!inputElement || !inputElement.files) {
            return [];
        }

        return Array.from(inputElement.files).map((file, index) => ({
            index,
            name: file.name,
            size: file.size
        }));
    }

    function clearSelection(inputElement) {
        if (inputElement) {
            inputElement.value = '';
        }
    }

    function uploadOne(file, index, options, batch, dotNetRef) {
        return new Promise((resolve) => {
            const request = new XMLHttpRequest();
            batch.request = request;

            const url = `${options.apiBaseUrl.replace(/\/$/, '')}/api/files/upload/stream`
                + `?fileName=${encodeURIComponent(file.name)}`
                + `&currentPath=${encodeURIComponent(options.currentPath ?? '')}`;

            request.open('POST', url, true);
            request.setRequestHeader('Authorization', `Bearer ${options.accessToken}`);
            request.setRequestHeader('Content-Type', 'application/octet-stream');
            if (options.correlationId) {
                request.setRequestHeader('X-Correlation-ID', options.correlationId);
            }

            request.upload.onprogress = function (event) {
                if (!event.lengthComputable) {
                    return;
                }

                dotNetRef.invokeMethodAsync('OnUploadProgress', index, event.loaded, event.total);
            };

            request.onload = function () {
                batch.request = null;
                if (request.status >= 200 && request.status < 300) {
                    resolve({ ok: true, status: request.status, error: null });
                    return;
                }

                resolve({
                    ok: false,
                    status: request.status,
                    error: describeFailure(request)
                });
            };

            request.onerror = function () {
                batch.request = null;
                resolve({ ok: false, status: request.status, error: 'The connection to the host was lost.' });
            };

            request.onabort = function () {
                batch.request = null;
                resolve({ ok: false, status: 0, cancelled: true, error: 'Cancelled.' });
            };

            request.send(file);
        });
    }

    function describeFailure(request) {
        const body = (request.responseText || '').trim();
        if (body) {
            return body.length > 400 ? `${body.slice(0, 400)}...` : body;
        }

        if (request.status === 401 || request.status === 403) {
            return 'Your session is no longer valid. Please request access again.';
        }

        if (request.status === 503) {
            return 'The host has not chosen a shared folder yet.';
        }

        return `The host returned status ${request.status}.`;
    }

    return {
        getSelectedFiles,
        clearSelection,

        async uploadFiles(inputElement, options, dotNetRef) {
            if (!inputElement || !inputElement.files || inputElement.files.length === 0) {
                return [];
            }

            const files = Array.from(inputElement.files);
            const batch = getBatch(options.batchId);
            const results = [];

            try {
                // Sequential keeps LAN bandwidth and disk contention predictable for large files.
                for (let index = 0; index < files.length; index++) {
                    if (batch.cancelled) {
                        results.push({
                            name: files[index].name,
                            sizeBytes: files[index].size,
                            succeeded: false,
                            cancelled: true,
                            error: 'Cancelled.'
                        });
                        continue;
                    }

                    const result = await uploadOne(files[index], index, options, batch, dotNetRef);
                    results.push({
                        name: files[index].name,
                        sizeBytes: files[index].size,
                        succeeded: result.ok,
                        cancelled: result.cancelled === true,
                        error: result.error
                    });

                    if (!result.ok && !result.cancelled && options.stopOnError) {
                        break;
                    }
                }
            } finally {
                activeBatches.delete(options.batchId);
                clearSelection(inputElement);
            }

            return results;
        },

        cancel(batchId) {
            const batch = activeBatches.get(batchId);
            if (!batch) {
                return;
            }

            batch.cancelled = true;
            if (batch.request) {
                batch.request.abort();
            }
        }
    };
})();
