/**
 * Tag Thumbnail Picker
 * Allows selecting a specific thumbnail for a tag from available videos and galleries
 */

class TagThumbnailPicker {
    constructor(config = {}) {
        this.config = {
            modalId: 'thumbnailPickerModal',
            loadUrl: '/Video/GetTagThumbnails',
            saveUrl: '/Video/SetTagThumbnail',
            ...config
        };

        this.tagId = null;
        this.currentThumbnailPath = null;
        this.initialize();
    }

    initialize() {
        // Create modal HTML if it doesn't exist
        if (!document.getElementById(this.config.modalId)) {
            this.createModal();
        }

        // Add click handler for the picker button
        document.addEventListener('click', (e) => {
            if (e.target.closest('.pick-tag-thumbnail-btn')) {
                const button = e.target.closest('.pick-tag-thumbnail-btn');
                this.tagId = button.dataset.tagId;
                this.currentThumbnailPath = button.dataset.currentThumbnail;
                this.openPicker();
            }
        });
    }

    createModal() {
        const modalHtml = `
            <div class="modal fade" id="${this.config.modalId}" tabindex="-1" aria-labelledby="thumbnailPickerLabel" aria-hidden="true">
                <div class="modal-dialog modal-xl modal-dialog-scrollable">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="thumbnailPickerLabel">
                                <i class="bi bi-image"></i> Select Tag Thumbnail
                            </h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <div id="thumbnailPickerLoading" class="text-center py-5">
                                <div class="spinner-border text-primary" role="status">
                                    <span class="visually-hidden">Loading...</span>
                                </div>
                                <p class="mt-3">Loading thumbnails...</p>
                            </div>
                            <div id="thumbnailPickerContent" class="d-none">
                                <div class="row g-3" id="thumbnailGrid"></div>
                            </div>
                            <div id="thumbnailPickerEmpty" class="d-none text-center py-5">
                                <i class="bi bi-images text-muted" style="font-size: 3rem;"></i>
                                <p class="mt-3 text-muted">No thumbnails available for this tag</p>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        document.body.insertAdjacentHTML('beforeend', modalHtml);
    }

    async openPicker() {
        const modal = new bootstrap.Modal(document.getElementById(this.config.modalId));
        modal.show();

        // Reset view
        document.getElementById('thumbnailPickerLoading').classList.remove('d-none');
        document.getElementById('thumbnailPickerContent').classList.add('d-none');
        document.getElementById('thumbnailPickerEmpty').classList.add('d-none');

        try {
            const response = await fetch(`${this.config.loadUrl}?tagId=${this.tagId}`);
            const data = await response.json();

            if (data.success && data.thumbnails.length > 0) {
                this.renderThumbnails(data.thumbnails);
            } else {
                this.showEmpty();
            }
        } catch (error) {
            console.error('Error loading thumbnails:', error);
            Toast.error('Failed to load thumbnails');
            modal.hide();
        }
    }

    renderThumbnails(thumbnails) {
        const grid = document.getElementById('thumbnailGrid');
        grid.innerHTML = '';

        thumbnails.forEach(item => {
            const isSelected = item.thumbnailPath === this.currentThumbnailPath;
            const thumbnailUrl = item.type === 'video' 
                ? `/Video/SmallThumbnail/${item.id}`
                : `/Gallery/Thumbnail?galleryId=${item.id}&fileName=${item.thumbnailPath}`;

            const col = document.createElement('div');
            col.className = 'col-lg-2 col-md-3 col-sm-4 col-6';
            col.innerHTML = `
                <div class="card thumbnail-option ${isSelected ? 'border-primary border-3' : ''}" 
                     data-thumbnail-path="${item.thumbnailPath}"
                     style="cursor: pointer;">
                    <img src="${thumbnailUrl}" 
                         class="card-img-top" 
                         alt="${item.title}"
                         style="height: 150px; object-fit: cover;">
                    <div class="card-body p-2">
                        <p class="card-text small mb-1">
                            <i class="bi bi-${item.type === 'video' ? 'camera-video' : 'images'}"></i>
                            ${item.title}
                        </p>
                        ${isSelected ? '<span class="badge bg-primary w-100">Current</span>' : ''}
                    </div>
                </div>
            `;

            col.querySelector('.thumbnail-option').addEventListener('click', () => {
                this.selectThumbnail(item.thumbnailPath);
            });

            grid.appendChild(col);
        });

        document.getElementById('thumbnailPickerLoading').classList.add('d-none');
        document.getElementById('thumbnailPickerContent').classList.remove('d-none');
    }

    showEmpty() {
        document.getElementById('thumbnailPickerLoading').classList.add('d-none');
        document.getElementById('thumbnailPickerEmpty').classList.remove('d-none');
    }

    async selectThumbnail(thumbnailPath) {
        try {
            const formData = new FormData();
            formData.append('tagId', this.tagId);
            formData.append('thumbnailPath', thumbnailPath);

            // Add anti-forgery token
            const token = document.querySelector('input[name="__RequestVerificationToken"]');
            if (token) {
                formData.append('__RequestVerificationToken', token.value);
            }

            const response = await fetch(this.config.saveUrl, {
                method: 'POST',
                body: formData
            });

            const data = await response.json();

            if (data.success) {
                Toast.success('Thumbnail updated successfully');
                
                // Close modal
                const modal = bootstrap.Modal.getInstance(document.getElementById(this.config.modalId));
                modal.hide();

                // Reload page to show new thumbnail
                window.location.reload();
            } else {
                Toast.error(data.message || 'Failed to update thumbnail');
            }
        } catch (error) {
            console.error('Error setting thumbnail:', error);
            Toast.error('Failed to update thumbnail');
        }
    }
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', function() {
    window.tagThumbnailPicker = new TagThumbnailPicker();
});
