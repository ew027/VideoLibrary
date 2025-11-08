/**
 * Status Toggler Utility
 * Provides functionality for toggling status (archived/active) on items
 */

class StatusToggler {
    /**
     * Initialize status togglers
     * @param {Object} config - Configuration object
     * @param {string} config.selector - CSS selector for status toggle elements (default: '.setArchivedStatus')
     * @param {string} config.updateUrl - URL to send status updates (default: '/Video/SetArchivedStatus')
     * @param {string} config.idAttribute - Data attribute name for item ID (default: 'id')
     * @param {string} config.valueAttribute - Data attribute name for value to set (default: 'valuetoset')
     * @param {Object} config.labels - Label configuration
     * @param {string} config.labels.archived - Label for archived state (default: 'Archived')
     * @param {string} config.labels.active - Label for active state (default: 'Current')
     * @param {Object} config.classes - CSS class configuration
     * @param {string} config.classes.archived - Class for archived state (default: 'bg-warning')
     * @param {string} config.classes.active - Class for active state (default: 'bg-success')
     */
    constructor(config = {}) {
        this.config = {
            selector: '.setArchivedStatus',
            updateUrl: '/Video/SetArchivedStatus',
            idAttribute: 'id',
            valueAttribute: 'valuetoset',
            labels: {
                archived: 'Archived',
                active: 'Current',
                ...(config.labels || {})
            },
            classes: {
                archived: 'bg-warning',
                active: 'bg-success',
                ...(config.classes || {})
            },
            ...config
        };

        this.initialize();
    }

    initialize() {
        document.querySelectorAll(this.config.selector).forEach(element => {
            element.style.cursor = 'pointer';
            element.addEventListener('click', (e) => this.toggleStatus(element));
        });
    }

    toggleStatus(element) {
        const itemId = element.dataset[this.config.idAttribute];
        const valueToSet = element.dataset[this.config.valueAttribute];

        const formData = new FormData();
        formData.append('id', itemId);
        formData.append('isArchived', valueToSet);
        formData.append('isXhr', 'true');

        fetch(this.config.updateUrl, {
            method: 'POST',
            body: formData
        })
        .then(response => {
            if (response.ok && response.status === 200) {
                if (valueToSet === 'True') {
                    this.setArchived(element);
                } else {
                    this.setActive(element);
                }
            } else {
                throw new Error('Failed to update status');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            if (typeof Toast !== 'undefined') {
                Toast.error('Failed to update status');
            }
        });
    }

    setArchived(element) {
        element.textContent = this.config.labels.archived;
        element.classList.remove(this.config.classes.active);
        element.classList.add(this.config.classes.archived);
        element.dataset[this.config.valueAttribute] = 'False';
    }

    setActive(element) {
        element.textContent = this.config.labels.active;
        element.classList.remove(this.config.classes.archived);
        element.classList.add(this.config.classes.active);
        element.dataset[this.config.valueAttribute] = 'True';
    }

    /**
     * Refresh togglers - useful when new elements are added dynamically
     */
    refresh() {
        this.initialize();
    }
}

/**
 * Request Transcription Utility
 * Handles requesting transcription for videos
 */
class TranscriptionRequester {
    /**
     * Initialize transcription requesters
     * @param {Object} config - Configuration object
     * @param {string} config.selector - CSS selector for transcription request elements (default: '.requestTranscription')
     * @param {string} config.updateUrl - URL to send transcription requests (default: '/Video/RequestTranscript')
     */
    constructor(config = {}) {
        this.config = {
            selector: '.requestTranscription',
            updateUrl: '/Video/RequestTranscript',
            ...config
        };

        this.initialize();
    }

    initialize() {
        document.querySelectorAll(this.config.selector).forEach(element => {
            element.style.cursor = 'pointer';
            element.addEventListener('click', (e) => this.requestTranscription(element));
        });
    }

    requestTranscription(element) {
        const videoId = element.dataset.videoid;
        const iconElement = element.querySelector('i');

        const formData = new FormData();
        formData.append('id', videoId);
        formData.append('isXhr', 'true');

        fetch(this.config.updateUrl, {
            method: 'POST',
            body: formData
        })
        .then(response => {
            if (response.ok && response.status === 200) {
                if (iconElement) {
                    iconElement.classList.add('text-warning');
                }
                if (typeof Toast !== 'undefined') {
                    Toast.success('Transcription requested');
                }
            } else {
                throw new Error('Failed to request transcription');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            if (typeof Toast !== 'undefined') {
                Toast.error('Failed to request transcription');
            }
        });
    }

    /**
     * Refresh requesters - useful when new elements are added dynamically
     */
    refresh() {
        this.initialize();
    }
}
