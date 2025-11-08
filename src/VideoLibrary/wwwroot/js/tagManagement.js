/**
 * Tag Management UI
 * Drag-and-drop tag hierarchy manager with rename, delete, and empty tag detection
 */

class TagManagementUI {
    constructor(config) {
        this.config = config;
        this.tags = [];
        this.tagMap = new Map();
        this.expandedNodes = new Set();
        this.selectedTagId = null;
        this.draggedTag = null;
        this.emptyTags = [];

        this.container = document.getElementById('tagTreeContainer');
        
        this.initialize();
    }

    async initialize() {
        this.setupEventListeners();
        await this.loadTags();
        await this.loadEmptyTags();
    }

    setupEventListeners() {
        // Expand/Collapse buttons
        document.getElementById('expandAllBtn')?.addEventListener('click', () => this.expandAll());
        document.getElementById('collapseAllBtn')?.addEventListener('click', () => this.collapseAll());
        document.getElementById('refreshBtn')?.addEventListener('click', () => this.refresh());

        // Search
        document.getElementById('tagSearchInput')?.addEventListener('input', (e) => {
            this.searchTags(e.target.value);
        });

        // Add tag modal
        document.getElementById('saveNewTagBtn')?.addEventListener('click', () => this.saveNewTag());
        
        // Rename tag modal
        document.getElementById('saveRenameBtn')?.addEventListener('click', () => this.saveRename());

        // Delete tag modal
        document.getElementById('confirmDeleteBtn')?.addEventListener('click', () => this.confirmDelete());

        // Add tag modal - populate parent dropdown when shown
        const addModal = document.getElementById('addTagModal');
        addModal?.addEventListener('show.bs.modal', () => {
            this.populateParentDropdown();
        });
    }

    async loadTags() {
        try {
            const response = await fetch(this.config.loadUrl);
            if (!response.ok) throw new Error('Failed to load tags');

            const data = await response.json();
            this.tags = data;
            this.buildTagMap();
            this.render();
            this.updateCounts();
        } catch (error) {
            console.error('Error loading tags:', error);
            Toast.error('Failed to load tags');
        }
    }

    async loadEmptyTags() {
        try {
            const response = await fetch(this.config.getEmptyTagsUrl);
            if (!response.ok) throw new Error('Failed to load empty tags');

            this.emptyTags = await response.json();
            this.renderEmptyTags();
        } catch (error) {
            console.error('Error loading empty tags:', error);
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
        
        if (this.tags.length === 0) {
            this.container.innerHTML = `
                <div class="text-center py-5 text-muted">
                    <i class="bi bi-tags" style="font-size: 3rem;"></i>
                    <p class="mt-2">No tags yet. Click "Add Tag" to create one.</p>
                </div>
            `;
            return;
        }

        const tree = this.buildTree(this.tags);
        this.container.appendChild(tree);
    }

    buildTree(tags) {
        const container = document.createElement('div');
        
        tags.forEach(tag => {
            const node = this.buildNode(tag);
            container.appendChild(node);
        });

        return container;
    }

    buildNode(tag) {
        const wrapper = document.createElement('div');
        wrapper.dataset.tagId = tag.id;

        // Main tag item
        const item = document.createElement('div');
        item.className = 'tag-tree-item';
        item.dataset.tagId = tag.id;
        item.draggable = true;
        
        if (tag.isEmpty) {
            item.classList.add('empty-tag');
        }

        if (this.selectedTagId === tag.id) {
            item.classList.add('selected');
        }

        // Expand/collapse button
        const hasChildren = tag.children && tag.children.length > 0;
        const expandBtn = document.createElement('button');
        expandBtn.className = 'tag-tree-expand-btn';
        expandBtn.type = 'button';
        
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

        // Drag handle
        const handle = document.createElement('span');
        handle.className = 'tag-tree-handle';
        handle.innerHTML = '<i class="bi bi-grip-vertical"></i>';

        // Icon
        const icon = document.createElement('i');
        icon.className = `bi ${hasChildren ? 'bi-folder-fill' : 'bi-tag-fill'} tag-tree-icon`;
        if (tag.isEmpty) {
            icon.style.color = '#ffc107';
        }

        // Label
        const label = document.createElement('span');
        label.className = 'tag-tree-label';
        label.textContent = tag.name;

        // Count
        const count = document.createElement('span');
        count.className = 'tag-tree-count';
        count.textContent = `${tag.contentCount} items`;

        // Actions
        const actions = document.createElement('div');
        actions.className = 'tag-tree-actions';
        
        const renameBtn = document.createElement('button');
        renameBtn.className = 'btn btn-sm btn-outline-primary';
        renameBtn.innerHTML = '<i class="bi bi-pencil"></i>';
        renameBtn.title = 'Rename';
        renameBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.showRenameModal(tag);
        });

        const deleteBtn = document.createElement('button');
        deleteBtn.className = 'btn btn-sm btn-outline-danger';
        deleteBtn.innerHTML = '<i class="bi bi-trash"></i>';
        deleteBtn.title = 'Delete';
        deleteBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.showDeleteModal(tag);
        });

        actions.appendChild(renameBtn);
        actions.appendChild(deleteBtn);

        // Assemble item
        item.appendChild(expandBtn);
        item.appendChild(handle);
        item.appendChild(icon);
        item.appendChild(label);
        item.appendChild(count);
        item.appendChild(actions);

        // Click to select and show details
        item.addEventListener('click', () => this.selectTag(tag));

        // Drag and drop events
        item.addEventListener('dragstart', (e) => this.handleDragStart(e, tag));
        item.addEventListener('dragend', (e) => this.handleDragEnd(e));
        item.addEventListener('dragover', (e) => this.handleDragOver(e));
        item.addEventListener('drop', (e) => this.handleDrop(e, tag));
        item.addEventListener('dragleave', (e) => this.handleDragLeave(e));

        wrapper.appendChild(item);

        // Children
        if (hasChildren) {
            const childrenContainer = document.createElement('div');
            childrenContainer.className = 'tag-tree-children';
            
            if (!this.expandedNodes.has(tag.id)) {
                childrenContainer.style.display = 'none';
            }

            const childTree = this.buildTree(tag.children);
            childrenContainer.appendChild(childTree);
            wrapper.appendChild(childrenContainer);
        }

        return wrapper;
    }

    toggleNode(tagId) {
        if (this.expandedNodes.has(tagId)) {
            this.expandedNodes.delete(tagId);
        } else {
            this.expandedNodes.add(tagId);
        }

        const wrapper = this.container.querySelector(`[data-tag-id="${tagId}"]`);
        if (!wrapper) return;

        const expandBtn = wrapper.querySelector('.tag-tree-expand-btn');
        const childrenContainer = wrapper.querySelector('.tag-tree-children');

        if (this.expandedNodes.has(tagId)) {
            expandBtn?.classList.add('expanded');
            if (childrenContainer) {
                childrenContainer.style.display = 'block';
            }
        } else {
            expandBtn?.classList.remove('expanded');
            if (childrenContainer) {
                childrenContainer.style.display = 'none';
            }
        }
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
    }

    collapseAll() {
        this.expandedNodes.clear();
        this.render();
    }

    async selectTag(tag) {
        this.selectedTagId = tag.id;
        this.render();
        await this.showTagDetails(tag.id);
    }

    async showTagDetails(tagId) {
        const panel = document.getElementById('tagDetailsPanel');
        const detailsDiv = document.getElementById('tagDetails');

        if (!panel || !detailsDiv) return;

        try {
            const response = await fetch(`${this.config.getDetailsUrl}?id=${tagId}`);
            if (!response.ok) throw new Error('Failed to load tag details');

            const details = await response.json();

            detailsDiv.innerHTML = `
                <div class="detail-row">
                    <span class="detail-label">Name</span>
                    <span class="detail-value"><strong>${details.name}</strong></span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Parent</span>
                    <span class="detail-value">${details.parentName || '<em>None (Root)</em>'}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Level</span>
                    <span class="detail-value">${details.level}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Videos</span>
                    <span class="detail-value">
                        <span class="badge bg-primary">${details.videoCount}</span>
                    </span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Galleries</span>
                    <span class="detail-value">
                        <span class="badge bg-success">${details.galleryCount}</span>
                    </span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Playlists</span>
                    <span class="detail-value">
                        <span class="badge bg-info">${details.playlistCount}</span>
                    </span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Content</span>
                    <span class="detail-value">
                        <span class="badge bg-warning text-dark">${details.contentCount}</span>
                    </span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Total</span>
                    <span class="detail-value">
                        <strong>${details.totalContentCount} items</strong>
                    </span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Children</span>
                    <span class="detail-value">${details.childCount}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Descendants</span>
                    <span class="detail-value">${details.descendantCount}</span>
                </div>
                ${details.isEmpty ? '<div class="alert alert-warning mt-3 mb-0"><i class="bi bi-exclamation-triangle"></i> This tag has no content</div>' : ''}
            `;

            panel.style.display = 'block';
        } catch (error) {
            console.error('Error loading tag details:', error);
            Toast.error('Failed to load tag details');
        }
    }

    searchTags(query) {
        if (!query) {
            // Clear search
            const items = this.container.querySelectorAll('.tag-tree-item');
            items.forEach(item => {
                item.style.display = '';
                item.style.opacity = '1';
            });
            return;
        }

        query = query.toLowerCase();
        const items = this.container.querySelectorAll('.tag-tree-item');

        items.forEach(item => {
            const label = item.querySelector('.tag-tree-label');
            const text = label?.textContent.toLowerCase() || '';

            if (text.includes(query)) {
                item.style.display = '';
                item.style.opacity = '1';
                
                // Expand parents
                const tagId = parseInt(item.dataset.tagId);
                this.expandParents(tagId);
            } else {
                item.style.opacity = '0.3';
            }
        });
    }

    expandParents(tagId) {
        const tag = this.tagMap.get(tagId);
        if (!tag || !tag.parentId) return;

        this.expandedNodes.add(tag.parentId);
        
        const wrapper = this.container.querySelector(`[data-tag-id="${tag.parentId}"]`);
        if (wrapper) {
            const expandBtn = wrapper.querySelector('.tag-tree-expand-btn');
            const childrenContainer = wrapper.querySelector('.tag-tree-children');
            
            expandBtn?.classList.add('expanded');
            if (childrenContainer) {
                childrenContainer.style.display = 'block';
            }
        }

        this.expandParents(tag.parentId);
    }

    // Drag and Drop handlers
    handleDragStart(e, tag) {
        this.draggedTag = tag;
        e.currentTarget.classList.add('dragging');
        e.dataTransfer.effectAllowed = 'move';
        e.dataTransfer.setData('text/plain', tag.id.toString());
    }

    handleDragEnd(e) {
        e.currentTarget.classList.remove('dragging');
        
        // Remove all drag-over classes
        const items = this.container.querySelectorAll('.tag-tree-item');
        items.forEach(item => item.classList.remove('drag-over'));
        
        this.draggedTag = null;
    }

    handleDragOver(e) {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'move';
        
        const item = e.currentTarget;
        if (!item.classList.contains('dragging')) {
            item.classList.add('drag-over');
        }
    }

    handleDragLeave(e) {
        e.currentTarget.classList.remove('drag-over');
    }

    async handleDrop(e, targetTag) {
        e.preventDefault();
        e.stopPropagation();
        
        e.currentTarget.classList.remove('drag-over');

        if (!this.draggedTag || this.draggedTag.id === targetTag.id) {
            return;
        }

        // Check if trying to move to own descendant
        if (await this.isDescendant(targetTag.id, this.draggedTag.id)) {
            Toast.error('Cannot move a tag to its own descendant');
            return;
        }

        await this.moveTag(this.draggedTag.id, targetTag.id);
    }

    async isDescendant(potentialDescendantId, ancestorId) {
        const descendant = this.tagMap.get(potentialDescendantId);
        const ancestor = this.tagMap.get(ancestorId);

        if (!descendant || !ancestor) return false;

        return descendant.left > ancestor.left && descendant.right < ancestor.right;
    }

    async moveTag(tagId, newParentId) {
        try {
            const response = await fetch(this.config.moveUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    tagId: tagId,
                    newParentId: newParentId
                })
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Failed to move tag');
            }

            Toast.success('Tag moved successfully');
            
            // Expand the new parent
            this.expandedNodes.add(newParentId);
            
            await this.loadTags();
            
            // Highlight the moved tag
            setTimeout(() => {
                const movedItem = this.container.querySelector(`[data-tag-id="${tagId}"] > .tag-tree-item`);
                if (movedItem) {
                    movedItem.classList.add('just-moved');
                    movedItem.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            }, 100);
        } catch (error) {
            console.error('Error moving tag:', error);
            Toast.error(error.message || 'Failed to move tag');
        }
    }

    // Modal handlers
    populateParentDropdown() {
        const select = document.getElementById('newTagParent');
        if (!select) return;

        select.innerHTML = '<option value="">None (Root Level)</option>';

        const addOptions = (tags, indent = '') => {
            tags.forEach(tag => {
                const option = document.createElement('option');
                option.value = tag.id;
                option.textContent = indent + tag.name;
                select.appendChild(option);

                if (tag.children && tag.children.length > 0) {
                    addOptions(tag.children, indent + '  ');
                }
            });
        };

        addOptions(this.tags);
    }

    async saveNewTag() {
        const nameInput = document.getElementById('newTagName');
        const parentSelect = document.getElementById('newTagParent');

        const name = nameInput?.value.trim();
        if (!name) {
            Toast.error('Please enter a tag name');
            return;
        }

        const parentId = parentSelect?.value ? parseInt(parentSelect.value) : null;

        try {
            const response = await fetch(this.config.addUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    name: name,
                    parentId: parentId
                })
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Failed to add tag');
            }

            const result = await response.json();
            Toast.success('Tag added successfully');

            // Close modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('addTagModal'));
            modal?.hide();

            // Clear form
            if (nameInput) nameInput.value = '';
            if (parentSelect) parentSelect.value = '';

            // Reload tags
            if (parentId) {
                this.expandedNodes.add(parentId);
            }
            await this.loadTags();

            // Highlight new tag
            setTimeout(() => {
                const newItem = this.container.querySelector(`[data-tag-id="${result.id}"] > .tag-tree-item`);
                if (newItem) {
                    newItem.classList.add('just-moved');
                    newItem.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            }, 100);
        } catch (error) {
            console.error('Error adding tag:', error);
            Toast.error(error.message || 'Failed to add tag');
        }
    }

    showRenameModal(tag) {
        const modal = new bootstrap.Modal(document.getElementById('renameTagModal'));
        
        document.getElementById('renameTagId').value = tag.id;
        document.getElementById('renameTagName').value = tag.name;
        
        modal.show();
    }

    async saveRename() {
        const idInput = document.getElementById('renameTagId');
        const nameInput = document.getElementById('renameTagName');

        const id = parseInt(idInput?.value || '0');
        const name = nameInput?.value.trim();

        if (!name) {
            Toast.error('Please enter a tag name');
            return;
        }

        try {
            const response = await fetch(this.config.renameUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    tagId: id,
                    newName: name
                })
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Failed to rename tag');
            }

            Toast.success('Tag renamed successfully');

            // Close modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('renameTagModal'));
            modal?.hide();

            // Reload tags
            await this.loadTags();
        } catch (error) {
            console.error('Error renaming tag:', error);
            Toast.error(error.message || 'Failed to rename tag');
        }
    }

    showDeleteModal(tag) {
        const modal = new bootstrap.Modal(document.getElementById('deleteTagModal'));
        
        document.getElementById('deleteTagId').value = tag.id;
        document.getElementById('deleteTagName').textContent = tag.name;
        document.getElementById('deleteDescendantCount').textContent = tag.children?.length || 0;
        document.getElementById('deleteTagContentCount').textContent = tag.contentCount;

        const warningDiv = document.getElementById('deleteTagContentWarning');
        if (tag.contentCount > 0) {
            warningDiv.style.display = 'block';
        } else {
            warningDiv.style.display = 'none';
        }

        // Reset checkbox
        const checkbox = document.getElementById('deleteDescendants');
        if (checkbox) checkbox.checked = false;
        
        modal.show();
    }

    async confirmDelete() {
        const idInput = document.getElementById('deleteTagId');
        const checkbox = document.getElementById('deleteDescendants');

        const id = parseInt(idInput?.value || '0');
        const deleteDescendants = checkbox?.checked || false;

        try {
            const response = await fetch(this.config.deleteUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    tagId: id,
                    deleteDescendants: deleteDescendants
                })
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Failed to delete tag');
            }

            Toast.success('Tag deleted successfully');

            // Close modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('deleteTagModal'));
            modal?.hide();

            // Clear selection if deleted tag was selected
            if (this.selectedTagId === id) {
                this.selectedTagId = null;
                document.getElementById('tagDetailsPanel').style.display = 'none';
            }

            // Reload tags
            await this.loadTags();
            await this.loadEmptyTags();
        } catch (error) {
            console.error('Error deleting tag:', error);
            Toast.error(error.message || 'Failed to delete tag');
        }
    }

    renderEmptyTags() {
        const container = document.getElementById('emptyTagsList');
        const badge = document.getElementById('emptyTagsBadge');
        const countSpan = document.getElementById('emptyTagCount');

        if (!container) return;

        if (this.emptyTags.length === 0) {
            container.innerHTML = '<p class="text-muted small mb-0">No empty tags found</p>';
            if (badge) badge.style.display = 'none';
            return;
        }

        if (badge) badge.style.display = '';
        if (countSpan) countSpan.textContent = this.emptyTags.length;

        container.innerHTML = '';
        
        this.emptyTags.forEach(tag => {
            const item = document.createElement('div');
            item.className = 'empty-tag-item';
            
            item.innerHTML = `
                <div>
                    <strong>${tag.name}</strong>
                    ${tag.parentName ? `<br><small class="text-muted">Parent: ${tag.parentName}</small>` : ''}
                </div>
                <button class="btn btn-sm btn-outline-danger" data-tag-id="${tag.id}">
                    <i class="bi bi-trash"></i>
                </button>
            `;

            const deleteBtn = item.querySelector('button');
            deleteBtn.addEventListener('click', () => {
                this.showDeleteModal(tag);
            });

            container.appendChild(item);
        });
    }

    updateCounts() {
        const totalSpan = document.getElementById('totalTagCount');
        if (totalSpan) {
            totalSpan.textContent = this.tagMap.size;
        }
    }

    async refresh() {
        Toast.info('Refreshing...');
        await this.loadTags();
        await this.loadEmptyTags();
        Toast.success('Refreshed');
    }
}
