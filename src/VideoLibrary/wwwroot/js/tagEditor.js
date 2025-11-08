/**
 * Tag Editor Utility
 * Provides tag editing functionality that works with MultiTagSelector
 * Handles the view/edit mode switching and loading of tags
 */

class TagEditor {
    /**
     * Initialize a tag editor
     * @param {Object} config - Configuration object
     * @param {string} config.tagsViewId - ID of the tags view container (default: 'tagsView')
     * @param {string} config.tagsEditId - ID of the tags edit container (default: 'tagsEdit')
     * @param {string} config.tagSearchId - ID of the tag search input (default: 'tagSearch')
     * @param {MultiTagSelector} config.multiTagSelector - Instance of MultiTagSelector
     * @param {string} config.getAllTagsUrl - URL to fetch all tags (default: '/Video/GetAllTags')
     */
    constructor(config) {
        this.config = {
            tagsViewId: 'tagsView',
            tagsEditId: 'tagsEdit',
            tagSearchId: 'tagSearch',
            getAllTagsUrl: '/Video/GetAllTags',
            ...config
        };

        this.tagsView = document.getElementById(this.config.tagsViewId);
        this.tagsEdit = document.getElementById(this.config.tagsEditId);
        this.tagSearch = document.getElementById(this.config.tagSearchId);
        this.multiTagSelector = this.config.multiTagSelector;

        if (!this.tagsView || !this.tagsEdit) {
            console.warn('TagEditor: Required elements not found');
        }
    }

    /**
     * Enter edit mode - show tag editing interface
     */
    async editTags() {
        if (!this.tagsView || !this.tagsEdit) {
            console.error('TagEditor: Cannot enter edit mode, elements not found');
            return;
        }

        this.tagsView.style.display = 'none';
        this.tagsEdit.style.display = '';

        if (this.multiTagSelector) {
            this.multiTagSelector.renderSelectedTags();

            // Load all tags if not already loaded
            if (this.multiTagSelector.allTags.length === 0) {
                try {
                    const response = await fetch(this.config.getAllTagsUrl);
                    if (response.ok) {
                        this.multiTagSelector.allTags = await response.json();
                    } else {
                        console.error('Failed to load tags:', response.statusText);
                    }
                } catch (error) {
                    console.error('Error loading tags:', error);
                }
            }
        }

        // Focus the search input
        if (this.tagSearch) {
            this.tagSearch.focus();
        }
    }

    /**
     * Cancel edit mode - return to view mode
     */
    cancelTagsEdit() {
        if (!this.tagsView || !this.tagsEdit) {
            return;
        }

        this.tagsView.style.display = '';
        this.tagsEdit.style.display = 'none';
    }

    /**
     * Add a tag to the selection
     * @param {number} tagId - The tag ID
     * @param {string} tagName - The tag name
     */
    addTagToSelection(tagId, tagName) {
        if (this.multiTagSelector) {
            this.multiTagSelector.selectTag(tagId, tagName);
        }
    }
}

/**
 * Global helper functions for backward compatibility
 * These can be called directly from views
 */
let globalTagEditor = null;

function editTags() {
    if (globalTagEditor) {
        globalTagEditor.editTags();
    } else {
        console.error('TagEditor not initialized');
    }
}

function cancelTagsEdit() {
    if (globalTagEditor) {
        globalTagEditor.cancelTagsEdit();
    } else {
        console.error('TagEditor not initialized');
    }
}

function addTagToSelection(tagId, tagName) {
    if (globalTagEditor) {
        globalTagEditor.addTagToSelection(tagId, tagName);
    } else {
        console.error('TagEditor not initialized');
    }
}
