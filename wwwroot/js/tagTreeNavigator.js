/**
 * Tag Tree Navigator
 * A toggleable, pinnable tree view for hierarchical tag navigation
 */

class TagTreeNavigator {
    constructor(config) {
        this.config = {
            containerId: 'tagTreeContainer',
            loadingId: 'tagTreeLoading',
            emptyId: 'tagTreeEmpty',
            loadUrl: '/Video/GetAllTagsHierarchical',
            showCounts: true,
            rememberState: true,
            multiSelect: false,
            autoLoadOnInit: true,
            ...config
        };

        this.tags = [];
        this.tagMap = new Map();
        this.expandedNodes = new Set();
        this.selectedTags = new Set();
        this.currentTagId = null;
        this.isPinned = false;
        this.isOpen = false;

        this.container = document.getElementById(this.config.containerId);
        this.loading = document.getElementById(this.config.loadingId);
        this.empty = document.getElementById(this.config.emptyId);

        this.initialize();
    }

    initialize() {
        // Load state from localStorage
        if (this.config.rememberState) {
            this.loadState();
        }

        // Setup UI event listeners
        this.setupEventListeners();

        // Load tags
        if (this.config.autoLoadOnInit) {
            this.loadTags();
        }
    }

    setupEventListeners() {
        // Toggle button
        const toggleBtn = document.getElementById('tagTreeToggleBtn');
        if (toggleBtn) {
            toggleBtn.addEventListener('click', () => this.toggle());
        }

        // Close button
        const closeBtn = document.getElementById('tagTreeCloseBtn');
        if (closeBtn) {
            closeBtn.addEventListener('click', () => this.close());
        }

        // Pin button
        const pinBtn = document.getElementById('tagTreePinBtn');
        if (pinBtn) {
            pinBtn.addEventListener('click', () => this.togglePin());
        }

        // Overlay
        const overlay = document.getElementById('tagTreeOverlay');
        if (overlay) {
            overlay.addEventListener('click', () => {
                if (!this.isPinned) {
                    this.close();
                }
            });
        }

        // Search
        const searchInput = document.getElementById('tagTreeSearch');
        if (searchInput) {
            searchInput.addEventListener('input', (e) => this.handleSearch(e.target.value));
        }

        const clearSearchBtn = document.getElementById('tagTreeClearSearch');
        if (clearSearchBtn) {
            clearSearchBtn.addEventListener('click', () => {
                searchInput.value = '';
                this.handleSearch('');
            });
        }

        // Expand/Collapse All
        const expandAllBtn = document.getElementById('tagTreeExpandAll');
        if (expandAllBtn) {
            expandAllBtn.addEventListener('click', () => this.expandAll());
        }

        const collapseAllBtn = document.getElementById('tagTreeCollapseAll');
        if (collapseAllBtn) {
            collapseAllBtn.addEventListener('click', () => this.collapseAll());
        }

        // Handle escape key
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && this.isOpen && !this.isPinned) {
                this.close();
            }
        });
    }

    async loadTags() {
        this.showLoading();

        try {
            const response = await fetch(this.config.loadUrl);
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            this.tags = await response.json();
            this.buildTagMap();
            this.render();
            this.updateCount();

            if (this.tags.length === 0) {
                this.showEmpty();
            } else {
                this.showContent();
            }
        } catch (error) {
            console.error('Error loading tags:', error);
            if (typeof Toast !== 'undefined') {
                Toast.error('Failed to load tags');
            }
            this.showEmpty();
        }
    }

    buildTagMap() {
        this.tagMap.clear();
        
        const buildMap = (tags) => {
            tags.forEach(tag => {
                this.tagMap.set(tag.id, tag);
                if (tag.children && tag.children.length > 0) {
                    buildMap(tag.children);
                }
            });
        };

        buildMap(this.tags);
    }

    render() {
        if (!this.container) return;

        this.container.innerHTML = '';
        const tree = this.buildTree(this.tags);
        this.container.appendChild(tree);
    }

    buildTree(tags, level = 0) {
        const ul = document.createElement('ul');
        ul.className = 'tag-tree-node';

        tags.forEach(tag => {
            const li = this.buildTreeNode(tag, level);
            ul.appendChild(li);
        });

        return ul;
    }

    buildTreeNode(tag, level) {
        const li = document.createElement('li');
        li.dataset.tagId = tag.id;

        const item = document.createElement('div');
        item.className = 'tag-tree-item';
        
        if (this.selectedTags.has(tag.id)) {
            item.classList.add('active');
        }

        // Expand/collapse button
        const hasChildren = tag.children && tag.children.length > 0;
        const expandBtn = document.createElement('button');
        expandBtn.className = 'tag-tree-expand-btn';
        if (!hasChildren) {
            expandBtn.classList.add('no-children');
        }
        if (this.expandedNodes.has(tag.id)) {
            expandBtn.classList.add('expanded');
        }
        expandBtn.innerHTML = '<i class="bi bi-chevron-right"></i>';
        expandBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.toggleNode(tag.id);
        });

        // Icon
        const icon = document.createElement('i');
        icon.className = `bi ${hasChildren ? 'bi-folder-fill' : 'bi-tag-fill'} tag-tree-icon`;

        // Label
        const label = document.createElement('span');
        label.className = 'tag-tree-label';
        label.textContent = tag.name;
        label.title = tag.name;

        // Count badge
        let countBadge = null;
        if (this.config.showCounts && tag.count !== undefined) {
            countBadge = document.createElement('span');
            countBadge.className = 'tag-tree-count';
            countBadge.textContent = tag.count;
        }

        // Assemble item
        item.appendChild(expandBtn);
        item.appendChild(icon);
        item.appendChild(label);
        if (countBadge) {
            item.appendChild(countBadge);
        }

        // Click handler
        item.addEventListener('click', () => this.handleTagClick(tag));

        li.appendChild(item);

        // Children
        if (hasChildren) {
            const childrenContainer = document.createElement('div');
            childrenContainer.className = 'tag-tree-children';
            if (this.expandedNodes.has(tag.id)) {
                childrenContainer.classList.add('expanded');
            }

            const childTree = this.buildTree(tag.children, level + 1);
            childrenContainer.appendChild(childTree);
            li.appendChild(childrenContainer);
        }

        return li;
    }

    toggleNode(tagId) {
        if (this.expandedNodes.has(tagId)) {
            this.expandedNodes.delete(tagId);
        } else {
            this.expandedNodes.add(tagId);
        }

        this.saveState();
        this.updateNodeExpansion(tagId);
    }

    updateNodeExpansion(tagId) {
        const li = this.container.querySelector(`li[data-tag-id="${tagId}"]`);
        if (!li) return;

        const expandBtn = li.querySelector('.tag-tree-expand-btn');
        const childrenContainer = li.querySelector('.tag-tree-children');

        if (this.expandedNodes.has(tagId)) {
            expandBtn.classList.add('expanded');
            if (childrenContainer) {
                childrenContainer.classList.add('expanded');
            }
        } else {
            expandBtn.classList.remove('expanded');
            if (childrenContainer) {
                childrenContainer.classList.remove('expanded');
            }
        }
    }

    handleTagClick(tag) {
        if (this.config.multiSelect) {
            // Toggle selection
            if (this.selectedTags.has(tag.id)) {
                this.selectedTags.delete(tag.id);
            } else {
                this.selectedTags.add(tag.id);
            }
        } else {
            // Single selection
            this.selectedTags.clear();
            this.selectedTags.add(tag.id);
            this.currentTagId = tag.id;
        }

        // Update UI
        this.updateSelection();

        // Callback
        if (this.config.onTagSelect) {
            this.config.onTagSelect(tag);
        }

        // Navigate if callback provided
        if (this.config.onTagClick) {
            this.config.onTagClick(tag);
        }
    }

    updateSelection() {
        // Update all items
        const items = this.container.querySelectorAll('.tag-tree-item');
        items.forEach(item => {
            const li = item.closest('li');
            const tagId = parseInt(li.dataset.tagId);
            
            if (this.selectedTags.has(tagId)) {
                item.classList.add('active');
            } else {
                item.classList.remove('active');
            }
        });

        // Update selected count
        const selectedCountSpan = document.getElementById('tagTreeSelected');
        const selectedCountValue = document.getElementById('tagTreeSelectedCount');
        
        if (selectedCountSpan && selectedCountValue) {
            if (this.selectedTags.size > 0) {
                selectedCountValue.textContent = this.selectedTags.size;
                selectedCountSpan.style.display = '';
            } else {
                selectedCountSpan.style.display = 'none';
            }
        }
    }

    handleSearch(query) {
        const searchInput = document.getElementById('tagTreeSearch');
        const clearBtn = document.getElementById('tagTreeClearSearch');

        if (clearBtn) {
            clearBtn.style.display = query ? '' : 'none';
        }

        if (!query) {
            // Clear search - show all nodes, restore expansion state
            this.clearSearchHighlight();
            return;
        }

        query = query.toLowerCase();

        // Find matching tags
        const matches = new Set();
        const findMatches = (tags) => {
            tags.forEach(tag => {
                if (tag.name.toLowerCase().includes(query)) {
                    matches.add(tag.id);
                    // Also expand ancestors
                    this.expandAncestors(tag.id);
                }
                if (tag.children && tag.children.length > 0) {
                    findMatches(tag.children);
                }
            });
        };

        findMatches(this.tags);

        // Highlight matches and dim non-matches
        const items = this.container.querySelectorAll('.tag-tree-item');
        items.forEach(item => {
            const li = item.closest('li');
            const tagId = parseInt(li.dataset.tagId);

            if (matches.has(tagId)) {
                item.classList.add('highlight');
                item.style.opacity = '1';
            } else {
                item.classList.remove('highlight');
                item.style.opacity = '0.4';
            }
        });
    }

    clearSearchHighlight() {
        const items = this.container.querySelectorAll('.tag-tree-item');
        items.forEach(item => {
            item.classList.remove('highlight');
            item.style.opacity = '1';
        });
    }

    expandAncestors(tagId) {
        const tag = this.tagMap.get(tagId);
        if (!tag) return;

        // Find parent and expand it
        const findAndExpandParent = (tags, targetId, parent = null) => {
            for (const t of tags) {
                if (t.id === targetId && parent) {
                    this.expandedNodes.add(parent.id);
                    this.updateNodeExpansion(parent.id);
                    return parent;
                }
                if (t.children && t.children.length > 0) {
                    const result = findAndExpandParent(t.children, targetId, t);
                    if (result) {
                        this.expandedNodes.add(t.id);
                        this.updateNodeExpansion(t.id);
                        return result;
                    }
                }
            }
            return null;
        };

        findAndExpandParent(this.tags, tagId);
    }

    expandAll() {
        const expandRecursive = (tags) => {
            tags.forEach(tag => {
                if (tag.children && tag.children.length > 0) {
                    this.expandedNodes.add(tag.id);
                    expandRecursive(tag.children);
                }
            });
        };

        expandRecursive(this.tags);
        this.render();
        this.saveState();
    }

    collapseAll() {
        this.expandedNodes.clear();
        this.render();
        this.saveState();
    }

    toggle() {
        if (this.isOpen) {
            this.close();
        } else {
            this.open();
        }
    }

    open() {
        const sidebar = document.getElementById('tagTreeSidebar');
        const overlay = document.getElementById('tagTreeOverlay');

        if (sidebar) {
            sidebar.classList.add('open');
        }

        if (overlay && !this.isPinned) {
            overlay.classList.add('show');
        }

        this.isOpen = true;
        this.saveState();
    }

    close() {
        const sidebar = document.getElementById('tagTreeSidebar');
        const overlay = document.getElementById('tagTreeOverlay');

        if (sidebar) {
            sidebar.classList.remove('open');
        }

        if (overlay) {
            overlay.classList.remove('show');
        }

        this.isOpen = false;
        this.saveState();
    }

    togglePin() {
        this.isPinned = !this.isPinned;

        const sidebar = document.getElementById('tagTreeSidebar');
        const pinBtn = document.getElementById('tagTreePinBtn');
        const overlay = document.getElementById('tagTreeOverlay');

        if (this.isPinned) {
            document.body.classList.add('tag-tree-pinned');
            if (sidebar) {
                sidebar.classList.add('pinned');
            }
            if (pinBtn) {
                pinBtn.classList.add('active');
                pinBtn.querySelector('i').className = 'bi bi-pin-fill';
            }
            if (overlay) {
                overlay.classList.remove('show');
            }
        } else {
            document.body.classList.remove('tag-tree-pinned');
            if (sidebar) {
                sidebar.classList.remove('pinned');
            }
            if (pinBtn) {
                pinBtn.classList.remove('active');
                pinBtn.querySelector('i').className = 'bi bi-pin';
            }
            if (overlay && this.isOpen) {
                overlay.classList.add('show');
            }
        }

        this.saveState();
    }

    updateCount() {
        const countElement = document.getElementById('tagTreeCount');
        if (countElement) {
            const totalTags = this.tagMap.size;
            countElement.textContent = totalTags;
        }
    }

    showLoading() {
        if (this.loading) this.loading.style.display = '';
        if (this.container) this.container.style.display = 'none';
        if (this.empty) this.empty.style.display = 'none';
    }

    showContent() {
        if (this.loading) this.loading.style.display = 'none';
        if (this.container) this.container.style.display = '';
        if (this.empty) this.empty.style.display = 'none';
    }

    showEmpty() {
        if (this.loading) this.loading.style.display = 'none';
        if (this.container) this.container.style.display = 'none';
        if (this.empty) this.empty.style.display = '';
    }

    // State persistence
    saveState() {
        if (!this.config.rememberState) return;

        const state = {
            expandedNodes: Array.from(this.expandedNodes),
            isPinned: this.isPinned,
            isOpen: this.isOpen
        };

        localStorage.setItem('tagTreeNavigator', JSON.stringify(state));
    }

    loadState() {
        if (!this.config.rememberState) return;

        try {
            const stateStr = localStorage.getItem('tagTreeNavigator');
            if (!stateStr) return;

            const state = JSON.parse(stateStr);
            
            if (state.expandedNodes) {
                this.expandedNodes = new Set(state.expandedNodes);
            }

            if (state.isPinned) {
                this.isPinned = state.isPinned;
                // Apply pinned state
                setTimeout(() => {
                    if (this.isPinned) {
                        this.togglePin(); // This will apply all the classes
                    }
                }, 0);
            }

            if (state.isOpen) {
                this.isOpen = state.isOpen;
                setTimeout(() => {
                    if (this.isOpen) {
                        this.open();
                    }
                }, 0);
            }
        } catch (error) {
            console.error('Error loading tag tree state:', error);
        }
    }

    clearState() {
        localStorage.removeItem('tagTreeNavigator');
    }

    // Public API
    selectTag(tagId) {
        this.selectedTags.clear();
        this.selectedTags.add(tagId);
        this.currentTagId = tagId;
        this.updateSelection();
    }

    getSelectedTags() {
        return Array.from(this.selectedTags);
    }

    getCurrentTag() {
        return this.currentTagId ? this.tagMap.get(this.currentTagId) : null;
    }

    refresh() {
        this.loadTags();
    }
}
