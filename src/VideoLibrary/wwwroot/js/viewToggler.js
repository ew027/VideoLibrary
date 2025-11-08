/**
 * View Toggler Utility
 * Provides tab-like switching between different views with optional lazy loading
 */

class ViewToggler {
    /**
     * Initialize a view toggler
     * @param {Object} config - Configuration object
     * @param {Array} config.views - Array of view configurations
     * Each view config should have:
     *   - buttonId: ID of the button that activates this view
     *   - viewId: ID of the view container
     *   - onActivate: Optional callback function when view is activated
     */
    constructor(config) {
        this.views = config.views || [];
        this.loadedViews = new Set();
        this.activeViewId = null;

        this.initialize();
    }

    initialize() {
        this.views.forEach(view => {
            const button = document.getElementById(view.buttonId);
            const viewElement = document.getElementById(view.viewId);

            if (!button || !viewElement) {
                console.warn(`ViewToggler: Elements not found for view ${view.viewId}`);
                return;
            }

            button.addEventListener('click', () => {
                this.showView(view.viewId);
            });

            // Set first view as active if not specified
            if (!this.activeViewId && button.classList.contains('active')) {
                this.activeViewId = view.viewId;
            }
        });

        // If no active view, activate the first one
        if (!this.activeViewId && this.views.length > 0) {
            this.showView(this.views[0].viewId);
        }
    }

    /**
     * Show a specific view
     * @param {string} viewId - ID of the view to show
     */
    showView(viewId) {
        const viewConfig = this.views.find(v => v.viewId === viewId);
        
        if (!viewConfig) {
            console.error(`ViewToggler: View ${viewId} not found in configuration`);
            return;
        }

        // Deactivate all views and buttons
        this.views.forEach(view => {
            const button = document.getElementById(view.buttonId);
            const viewElement = document.getElementById(view.viewId);

            if (button) {
                button.classList.remove('active');
            }
            if (viewElement) {
                viewElement.classList.add('d-none');
            }
        });

        // Activate the selected view and button
        const activeButton = document.getElementById(viewConfig.buttonId);
        const activeView = document.getElementById(viewConfig.viewId);

        if (activeButton) {
            activeButton.classList.add('active');
        }
        if (activeView) {
            activeView.classList.remove('d-none');
        }

        // Call onActivate callback if this view hasn't been loaded yet
        if (!this.loadedViews.has(viewId) && viewConfig.onActivate) {
            viewConfig.onActivate();
            this.loadedViews.add(viewId);
        }

        this.activeViewId = viewId;
    }

    /**
     * Get the currently active view ID
     * @returns {string|null} Active view ID
     */
    getActiveViewId() {
        return this.activeViewId;
    }
}

/**
 * Legacy helper function for backward compatibility
 * Shows a view by toggling button and view states
 */
function showView(activeBtn, inactiveBtn, activeView, inactiveView) {
    if (!activeBtn || !inactiveBtn || !activeView || !inactiveView) {
        return;
    }

    // Update button states
    activeBtn.classList.add('active');
    inactiveBtn.classList.remove('active');

    // Toggle views
    activeView.classList.remove('d-none');
    inactiveView.classList.add('d-none');
}
