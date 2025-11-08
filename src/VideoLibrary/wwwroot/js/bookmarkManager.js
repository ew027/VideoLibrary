class BookmarkManager {
    constructor(videoElement, videoId, bookmarks = []) {
        this.videoElement = videoElement;
        this.videoId = videoId;
        this.bookmarks = bookmarks;
        this.containerElement = document.getElementById('bookmarksContainer');
        this.listElement = document.getElementById('bookmarksList');
        this.countElement = document.getElementById('bookmarksCount');
        this.addButton = document.getElementById('addBookmarkBtn');

        this.init();
    }

    init() {
        this.renderBookmarks();
        this.updateCount();
        this.attachEventListeners();
    }

    attachEventListeners() {
        // Add bookmark button
        if (this.addButton) {
            this.addButton.addEventListener('click', () => this.addBookmark());
        }
    }

    renderBookmarks() {
        if (!this.listElement) return;

        if (this.bookmarks.length === 0) {
            this.listElement.innerHTML = `
                <div class="text-center text-muted py-3">
                    <i class="bi bi-bookmark"></i>
                    <p class="mb-0 small">No bookmarks yet</p>
                </div>
            `;
            return;
        }

        // Sort bookmarks by time
        const sortedBookmarks = [...this.bookmarks].sort((a, b) => a.timeInSeconds - b.timeInSeconds);

        this.listElement.innerHTML = sortedBookmarks.map(bookmark => `
            <div class="bookmark-item d-flex align-items-center justify-content-between py-2 px-2 border-bottom" data-bookmark-id="${bookmark.id}">
                <div class="bookmark-info flex-grow-1" style="cursor: pointer;" onclick="bookmarkManager.seekTo(${bookmark.timeInSeconds})">
                    <div class="d-flex align-items-center">
                        <span class="badge bg-primary me-2">${this.formatTime(bookmark.timeInSeconds)}</span>
                        <span class="bookmark-name">${this.escapeHtml(bookmark.name)}</span>
                    </div>
                </div>
                <div class="bookmark-actions">
                    <button class="btn btn-sm btn-outline-secondary me-1" onclick="bookmarkManager.editBookmark(${bookmark.id})" title="Edit">
                        <i class="bi bi-pencil"></i>
                    </button>
                    <button class="btn btn-sm btn-outline-danger" onclick="bookmarkManager.deleteBookmark(${bookmark.id})" title="Delete">
                        <i class="bi bi-trash"></i>
                    </button>
                </div>
            </div>
        `).join('');
    }

    updateCount() {
        if (this.countElement) {
            this.countElement.textContent = this.bookmarks.length;
        }
    }

    async addBookmark() {
        if (!this.videoElement) return;

        const currentTime = this.videoElement.currentTime;

        try {
            const response = await fetch('/Bookmark/Add', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                },
                body: new URLSearchParams({
                    videoId: this.videoId,
                    timeInSeconds: currentTime
                })
            });

            const result = await response.json();

            if (result.success) {
                this.bookmarks.push(result.bookmark);
                this.renderBookmarks();
                this.updateCount();
                this.showNotification('Bookmark added successfully', 'success');
            } else {
                this.showNotification(result.message || 'Failed to add bookmark', 'error');
            }
        } catch (error) {
            console.error('Error adding bookmark:', error);
            this.showNotification('Failed to add bookmark', 'error');
        }
    }

    editBookmark(bookmarkId) {
        const bookmark = this.bookmarks.find(b => b.id === bookmarkId);
        if (!bookmark) return;

        const bookmarkElement = this.listElement.querySelector(`[data-bookmark-id="${bookmarkId}"]`);
        if (!bookmarkElement) return;

        // Create edit form
        const editForm = `
            <div class="bookmark-edit-form p-2 bg-light">
                <div class="mb-2">
                    <label class="form-label small mb-1">Name</label>
                    <input type="text" class="form-control form-control-sm" id="edit-name-${bookmarkId}" value="${this.escapeHtml(bookmark.name)}">
                </div>
                <div class="mb-2">
                    <label class="form-label small mb-1">Time (MM:SS)</label>
                    <input type="text" class="form-control form-control-sm" id="edit-time-${bookmarkId}" value="${this.formatTime(bookmark.timeInSeconds)}" placeholder="1:30">
                </div>
                <div class="d-flex gap-2">
                    <button class="btn btn-sm btn-success flex-grow-1" onclick="bookmarkManager.saveEdit(${bookmarkId})">
                        <i class="bi bi-check"></i> Save
                    </button>
                    <button class="btn btn-sm btn-secondary" onclick="bookmarkManager.cancelEdit(${bookmarkId})">
                        <i class="bi bi-x"></i> Cancel
                    </button>
                </div>
            </div>
        `;

        bookmarkElement.innerHTML = editForm;
    }

    cancelEdit(bookmarkId) {
        this.renderBookmarks();
    }

    async saveEdit(bookmarkId) {
        const nameInput = document.getElementById(`edit-name-${bookmarkId}`);
        const timeInput = document.getElementById(`edit-time-${bookmarkId}`);

        if (!nameInput || !timeInput) return;

        const name = nameInput.value.trim();
        const timeInSeconds = this.parseTimeInput(timeInput.value);

        if (timeInSeconds === null) {
            this.showNotification('Invalid time format. Use MM:SS (e.g., 1:30)', 'error');
            return;
        }

        try {
            const response = await fetch('/Bookmark/Update', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                },
                body: new URLSearchParams({
                    id: bookmarkId,
                    name: name,
                    timeInSeconds: timeInSeconds
                })
            });

            const result = await response.json();

            if (result.success) {
                const bookmarkIndex = this.bookmarks.findIndex(b => b.id === bookmarkId);
                if (bookmarkIndex !== -1) {
                    this.bookmarks[bookmarkIndex] = result.bookmark;
                }
                this.renderBookmarks();
                this.showNotification('Bookmark updated successfully', 'success');
            } else {
                this.showNotification(result.message || 'Failed to update bookmark', 'error');
            }
        } catch (error) {
            console.error('Error updating bookmark:', error);
            this.showNotification('Failed to update bookmark', 'error');
        }
    }

    async deleteBookmark(bookmarkId) {
        if (!confirm('Are you sure you want to delete this bookmark?')) {
            return;
        }

        try {
            const response = await fetch('/Bookmark/Delete', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                },
                body: new URLSearchParams({
                    id: bookmarkId
                })
            });

            const result = await response.json();

            if (result.success) {
                this.bookmarks = this.bookmarks.filter(b => b.id !== bookmarkId);
                this.renderBookmarks();
                this.updateCount();
                this.showNotification('Bookmark deleted successfully', 'success');
            } else {
                this.showNotification(result.message || 'Failed to delete bookmark', 'error');
            }
        } catch (error) {
            console.error('Error deleting bookmark:', error);
            this.showNotification('Failed to delete bookmark', 'error');
        }
    }

    seekTo(timeInSeconds) {
        if (this.videoElement) {
            this.videoElement.currentTime = timeInSeconds;
            this.videoElement.play();
        }
    }

    formatTime(seconds) {
        const minutes = Math.floor(seconds / 60);
        const secs = Math.floor(seconds % 60);
        return `${minutes}:${secs.toString().padStart(2, '0')}`;
    }

    parseTimeInput(timeString) {
        // Parse MM:SS format
        const parts = timeString.trim().split(':');
        if (parts.length !== 2) return null;

        const minutes = parseInt(parts[0], 10);
        const seconds = parseInt(parts[1], 10);

        if (isNaN(minutes) || isNaN(seconds) || seconds >= 60 || seconds < 0) {
            return null;
        }

        return minutes * 60 + seconds;
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    showNotification(message, type = 'info') {
        // Create toast notification
        const toastContainer = document.getElementById('toastContainer') || this.createToastContainer();

        const toastId = `toast-${Date.now()}`;
        const bgClass = type === 'success' ? 'bg-success' : type === 'error' ? 'bg-danger' : 'bg-info';

        const toast = document.createElement('div');
        toast.id = toastId;
        toast.className = `toast align-items-center text-white ${bgClass} border-0`;
        toast.setAttribute('role', 'alert');
        toast.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">${message}</div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>
        `;

        toastContainer.appendChild(toast);

        const bsToast = new bootstrap.Toast(toast);
        bsToast.show();

        toast.addEventListener('hidden.bs.toast', () => {
            toast.remove();
        });
    }

    createToastContainer() {
        const container = document.createElement('div');
        container.id = 'toastContainer';
        container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
        document.body.appendChild(container);
        return container;
    }
}

// Make it available globally
window.BookmarkManager = BookmarkManager;