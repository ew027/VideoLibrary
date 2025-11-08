/**
 * Content Loader Utility
 * Provides lazy loading functionality for content sections
 */

class ContentLoader {
    /**
     * Initialize a content loader
     * @param {Object} config - Configuration object
     * @param {string} config.contentId - ID of the content container
     * @param {string} config.loaderId - ID of the loading spinner (optional)
     * @param {string} config.placeholderId - ID of the placeholder element (optional)
     * @param {string} config.loadUrl - URL to fetch content from
     * @param {Function} config.onLoad - Callback function after content loads
     * @param {Function} config.onError - Callback function on error
     */
    constructor(config) {
        this.config = config;
        this.loaded = false;
        this.loading = false;

        this.contentElement = document.getElementById(config.contentId);
        this.loaderElement = config.loaderId ? document.getElementById(config.loaderId) : null;
        this.placeholderElement = config.placeholderId ? document.getElementById(config.placeholderId) : null;
    }

    /**
     * Load the content
     * @returns {Promise<void>}
     */
    async load() {
        if (this.loaded || this.loading) {
            return;
        }

        this.loading = true;
        this.showLoading();

        try {
            const response = await fetch(this.config.loadUrl);
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const html = await response.text();
            
            if (this.contentElement) {
                this.contentElement.innerHTML = html;
            }

            this.loaded = true;
            this.hideLoading();

            if (this.config.onLoad) {
                this.config.onLoad();
            }

            if (typeof Toast !== 'undefined') {
                Toast.success('Content loaded');
            }
        } catch (error) {
            console.error('Error loading content:', error);
            this.hideLoading();
            
            if (this.placeholderElement) {
                this.placeholderElement.textContent = 'Error loading content';
            }

            if (this.config.onError) {
                this.config.onError(error);
            }

            if (typeof Toast !== 'undefined') {
                Toast.error('Failed to load content');
            }
        } finally {
            this.loading = false;
        }
    }

    showLoading() {
        if (this.loaderElement) {
            this.loaderElement.style.display = 'block';
        }
        if (this.placeholderElement) {
            this.placeholderElement.textContent = 'Loading...';
        }
    }

    hideLoading() {
        if (this.loaderElement) {
            this.loaderElement.style.display = 'none';
        }
        if (this.placeholderElement) {
            this.placeholderElement.textContent = '';
        }
    }

    /**
     * Reset the loader state
     */
    reset() {
        this.loaded = false;
        this.loading = false;
        if (this.contentElement) {
            this.contentElement.innerHTML = '';
        }
    }

    /**
     * Check if content is loaded
     * @returns {boolean}
     */
    isLoaded() {
        return this.loaded;
    }
}

/**
 * Create a simple transcription loader
 * @param {string} videoId - The video ID
 * @param {string} contentId - ID of content container (default: 'transcriptionContent')
 * @param {string} loaderId - ID of loader element (default: 'transcriptionLoader')
 * @param {string} placeholderId - ID of placeholder element (default: 'transcriptionPlaceholder')
 * @returns {ContentLoader}
 */
function createTranscriptionLoader(videoId, contentId = 'transcriptionContent', 
                                   loaderId = 'transcriptionLoader', 
                                   placeholderId = 'transcriptionPlaceholder') {
    return new ContentLoader({
        contentId: contentId,
        loaderId: loaderId,
        placeholderId: placeholderId,
        loadUrl: `/Content/EmbeddedView?id=${videoId}`
    });
}
