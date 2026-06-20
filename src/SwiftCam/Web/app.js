// Audio status polling
(function() {
    var stateEl = document.getElementById('audio-state');
    var reasonEl = document.getElementById('audio-reason');
    var wasUnavailable = false;

    function pollStatus() {
        fetch('/api/audio-status')
            .then(function(res) {
                if (!res.ok) throw new Error('HTTP ' + res.status);
                return res.json();
            })
            .then(function(data) {
                stateEl.textContent = data.state || 'Unknown';
                stateEl.className = 'value';
                reasonEl.textContent = data.reason || '';
                reasonEl.className = 'value';
                wasUnavailable = false;
            })
            .catch(function() {
                stateEl.textContent = 'Status unavailable';
                stateEl.className = 'value unavailable';
                reasonEl.textContent = '';
                wasUnavailable = true;
            });
    }

    pollStatus();
    setInterval(pollStatus, 5000);
})();

// Capture gallery
(function() {
    var gridEl = document.getElementById('gallery-grid');
    var emptyEl = document.getElementById('gallery-empty');
    var refreshBtn = document.getElementById('gallery-refresh-btn');
    var captureBtn = document.getElementById('take-capture-btn');
    var galleryEl = document.getElementById('capture-gallery');
    var deleteDialog = document.getElementById('delete-confirm-dialog');
    var deleteDialogFilename = document.getElementById('delete-dialog-filename');
    var deleteCancelBtn = document.getElementById('delete-cancel-btn');
    var deleteConfirmBtn = document.getElementById('delete-confirm-btn');
    var isLoading = false;
    var isCaptureLoading = false;
    var pendingDelete = null;
    var deletingFiles = new Set();

    function parseFilenameTimestamp(filename) {
        var name = filename.replace('.jpg', '').replace('.JPG', '');
        var parts = name.split('_');
        if (parts.length < 2) return filename;
        var datePart = parts[0];
        var timePart = parts[1];
        var datePieces = datePart.split('-');
        if (datePieces.length < 3) return filename;
        var year = datePieces[0];
        var month = datePieces[1];
        var day = datePieces[2];
        var timePieces = timePart.split('-');
        if (timePieces.length < 3) return filename;
        var hour = timePieces[0];
        var minute = timePieces[1];
        var second = timePieces[2];
        return month + ' ' + parseInt(day, 10) + ', ' + year + ' ' + hour + ':' + minute + ':' + second;
    }

    function renderGallery(captures) {
        gridEl.innerHTML = '';
        if (!captures || captures.length === 0) {
            emptyEl.style.display = '';
            return;
        }
        emptyEl.style.display = 'none';
        for (var i = 0; i < captures.length; i++) {
            var filename = captures[i];
            var thumbDiv = document.createElement('div');
            thumbDiv.className = 'thumb';
            thumbDiv.setAttribute('data-filename', filename);

            var link = document.createElement('a');
            link.href = '/api/captures/' + encodeURIComponent(filename);
            link.target = '_blank';
            link.rel = 'noopener';

            var img = document.createElement('img');
            img.src = '/api/captures/' + encodeURIComponent(filename);
            img.alt = filename;
            img.loading = 'lazy';
            link.appendChild(img);

            var deleteBtn = document.createElement('button');
            deleteBtn.className = 'delete-btn';
            deleteBtn.type = 'button';
            deleteBtn.textContent = '\u00D7';
            deleteBtn.title = 'Delete ' + filename;
            deleteBtn.setAttribute('aria-label', 'Delete ' + filename);
            deleteBtn.setAttribute('data-filename', filename);
            if (deletingFiles.has(filename)) {
                deleteBtn.disabled = true;
                deleteBtn.textContent = '\u2026';
            }
            deleteBtn.addEventListener('click', function(e) {
                e.preventDefault();
                e.stopPropagation();
                var fn = this.getAttribute('data-filename');
                if (!deletingFiles.has(fn)) {
                    showDeleteDialog(fn);
                }
            });

            var ts = document.createElement('span');
            ts.className = 'timestamp';
            ts.textContent = parseFilenameTimestamp(filename);

            thumbDiv.appendChild(link);
            thumbDiv.appendChild(deleteBtn);
            thumbDiv.appendChild(ts);
            gridEl.appendChild(thumbDiv);
        }
    }

    function fetchCaptures() {
        if (isLoading) return;
        isLoading = true;
        refreshBtn.disabled = true;
        refreshBtn.textContent = 'Loading...';

        fetch('/api/captures')
            .then(function(res) {
                if (!res.ok) throw new Error('HTTP ' + res.status);
                return res.json();
            })
            .then(function(data) {
                renderGallery(data);
            })
            .catch(function() {
                gridEl.innerHTML = '';
                emptyEl.style.display = '';
                emptyEl.textContent = 'Failed to load captures';
            })
            .finally(function() {
                isLoading = false;
                refreshBtn.disabled = false;
                refreshBtn.textContent = 'Refresh';
            });
    }

    refreshBtn.addEventListener('click', fetchCaptures);
    fetchCaptures();

    function showGalleryError(message) {
        var errorDiv = document.createElement('div');
        errorDiv.className = 'gallery-error';
        errorDiv.textContent = message;
        galleryEl.insertBefore(errorDiv, gridEl);
        setTimeout(function() {
            if (errorDiv.parentNode) {
                errorDiv.parentNode.removeChild(errorDiv);
            }
        }, 5000);
    }

    function takeCapture() {
        if (isCaptureLoading) return;
        isCaptureLoading = true;
        captureBtn.disabled = true;
        captureBtn.textContent = 'Capturing...';

        fetch('/api/captures', { method: 'POST' })
            .then(function(res) {
                if (res.status === 201) {
                    fetchCaptures();
                } else {
                    return res.json().then(function(data) {
                        showGalleryError(data.error || 'Capture failed');
                    });
                }
            })
            .catch(function() {
                showGalleryError('Network error: could not reach the server');
            })
            .finally(function() {
                isCaptureLoading = false;
                captureBtn.disabled = false;
                captureBtn.textContent = 'Take Capture';
            });
    }

    captureBtn.addEventListener('click', takeCapture);

    function showDeleteDialog(filename) {
        pendingDelete = filename;
        deleteDialogFilename.textContent = filename;
        deleteDialog.classList.add('visible');
    }

    function hideDeleteDialog() {
        pendingDelete = null;
        deleteDialog.classList.remove('visible');
    }

    function confirmDelete() {
        if (!pendingDelete) return;
        var filename = pendingDelete;
        hideDeleteDialog();

        deletingFiles.add(filename);
        var thumbDiv = gridEl.querySelector('[data-filename="' + CSS.escape(filename) + '"]');
        var btn = thumbDiv ? thumbDiv.querySelector('.delete-btn') : null;
        if (btn) {
            btn.disabled = true;
            btn.textContent = '\u2026';
        }

        fetch('/api/captures/' + encodeURIComponent(filename), { method: 'DELETE' })
            .then(function(res) {
                if (res.status === 204) {
                    if (thumbDiv && thumbDiv.parentNode) {
                        thumbDiv.parentNode.removeChild(thumbDiv);
                    }
                    if (gridEl.children.length === 0) {
                        emptyEl.style.display = '';
                    }
                } else {
                    return res.json().then(function(data) {
                        showGalleryError(data.error || 'Delete failed');
                    });
                }
            })
            .catch(function() {
                showGalleryError('Network error: could not reach the server');
            })
            .finally(function() {
                deletingFiles.delete(filename);
                if (btn) {
                    btn.disabled = false;
                    btn.textContent = '\u00D7';
                }
            });
    }

    deleteCancelBtn.addEventListener('click', hideDeleteDialog);
    deleteConfirmBtn.addEventListener('click', confirmDelete);
})();
