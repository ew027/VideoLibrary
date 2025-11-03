/**
 * Title Editor Utility
 * Provides inline title editing functionality with AJAX submission
 */

class TitleEditor {
    /**
     * Initialize a title editor
     * @param {Object} config - Configuration object
     * @param {string} config.titleDisplayId - ID of the title display element
     * @param {string} config.titleEditFormId - ID of the title edit form
     * @param {string} config.editButtonId - ID of the edit button
     * @param {string} config.cancelButtonId - ID of the cancel button
     * @param {string} config.titleInputId - ID of the title input field
     * @param {string} config.originalTitle - Original title value
     * @param {string} config.updateUrl - URL to send update requests to
     * @param {string} config.titleElementSelector - CSS selector for title element (default: 'h2')
     */
    constructor(config) {
        this.config = {
            titleElementSelector: 'h2',
            ...config
        };

        this.titleDisplay = document.getElementById(this.config.titleDisplayId);
        this.titleEditForm = document.getElementById(this.config.titleEditFormId);
        this.editButton = document.getElementById(this.config.editButtonId);
        this.cancelButton = document.getElementById(this.config.cancelButtonId);
        this.titleInput = document.getElementById(this.config.titleInputId);
        this.originalTitle = this.config.originalTitle;

        this.initialize();
    }

    initialize() {
        if (!this.titleDisplay || !this.titleEditForm || !this.editButton || 
            !this.cancelButton || !this.titleInput) {
            console.warn('TitleEditor: Required elements not found');
            return;
        }

        this.attachEventListeners();
    }

    attachEventListeners() {
        // Enter edit mode
        this.editButton.addEventListener('click', () => this.enterEditMode());

        // Cancel edit mode
        this.cancelButton.addEventListener('click', () => this.cancelEdit());

        // Handle keyboard shortcuts
        this.titleInput.addEventListener('keydown', (e) => this.handleKeyDown(e));

        // Handle form submission
        this.titleEditForm.addEventListener('submit', (e) => this.handleSubmit(e));
    }

    enterEditMode() {
        this.titleDisplay.classList.add('d-none');
        this.titleDisplay.classList.remove('d-flex');
        this.titleEditForm.classList.add('d-flex');
        this.titleEditForm.classList.remove('d-none');
        this.titleInput.focus();
        this.titleInput.select();
    }

    cancelEdit() {
        this.titleInput.value = this.originalTitle;
        this.exitEditMode();
    }

    exitEditMode() {
        this.titleDisplay.classList.add('d-flex');
        this.titleDisplay.classList.remove('d-none');
        this.titleEditForm.classList.add('d-none');
        this.titleEditForm.classList.remove('d-flex');
    }

    handleKeyDown(e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            this.titleEditForm.requestSubmit();
        } else if (e.key === 'Escape') {
            this.cancelEdit();
        }
    }

    handleSubmit(e) {
        e.preventDefault();

        const formData = new FormData(this.titleEditForm);

        fetch(this.config.updateUrl, {
            method: 'POST',
            body: formData
        })
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            if (data.success) {
                // Update the display with new title
                const titleElement = this.titleDisplay.querySelector(this.config.titleElementSelector);
                if (titleElement) {
                    titleElement.textContent = this.titleInput.value;
                }
                this.originalTitle = this.titleInput.value;
                this.exitEditMode();

                // Show success notification if Toast is available
                if (typeof Toast !== 'undefined') {
                    Toast.success('Title updated successfully!');
                } else if (typeof showToast === 'function') {
                    showToast('Title updated successfully!', 'success');
                }
            } else {
                throw new Error(data.message || 'Error updating title');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            
            // Show error notification
            if (typeof Toast !== 'undefined') {
                Toast.error(error.message || 'Error updating title');
            } else if (typeof showToast === 'function') {
                showToast(error.message || 'Error updating title', 'error');
            } else {
                alert(error.message || 'Error updating title');
            }
        });
    }
}
