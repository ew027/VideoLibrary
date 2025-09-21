class MultiTagSelector {
    constructor(config) {
        this.config = {
            tagSearchId: 'tagSearch',
            tagDropdownId: 'tagDropdown',
            tagData: [],
            selectedTagsContainerId: 'selectedTags',
            ...config
        }

        this.selectedTags = new Map(this.config.tagData.map(tag => [tag.id, tag.name]));
        this.allTags = [];
        this.initializeEventListeners();

        this.newTagId = -1;
    }

    initializeEventListeners() {
        const tagSearch = document.getElementById(this.config.tagSearchId);
        const tagDropdown = document.getElementById(this.config.tagDropdownId);

        // Input event for autocomplete
        tagSearch.addEventListener('input', (e) => {
            this.handleSearchInput(e.target.value);
        });

        // Handle Enter key
        tagSearch.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                const dropdown = document.getElementById(this.config.tagDropdownId);
                const firstItem = dropdown.querySelector('.dropdown-item');
                if (firstItem && dropdown.style.display != 'none') {
                    firstItem.click();
                }
                else {
                    // treat it as a new tag
                    this.selectTag(this.newTagId, e.target.value);
                }
            }
        });

        // Close dropdown when clicking outside
        document.addEventListener('click', (e) => {
            if (!e.target.closest('.tag-input-container')) {
                tagDropdown.style.display = 'none';
            }
        });
    }

    handleSearchInput(searchTerm) {
        const dropdown = document.getElementById(this.config.tagDropdownId);

        if (searchTerm.trim() === '') {
            dropdown.style.display = 'none';
            return;
        }

        const filteredTags = this.allTags.filter(tag =>
            tag.name.toLowerCase().includes(searchTerm.toLowerCase()) &&
            !this.selectedTags.has(tag.id)
        );

        this.renderDropdown(filteredTags, searchTerm);
    }

    renderDropdown(tags, searchTerm) {
        const dropdown = document.getElementById(this.config.tagDropdownId);

        if (tags.length === 0) {
            dropdown.style.display = 'none';
            return;
        }

        const html = tags.slice(0, 10).map(tag => `
                <button type="button" class="dropdown-item" onclick="multiTagSelector.selectTag(${tag.id}, '${tag.name}')">
                    <i class="bi bi-tag"></i> ${this.highlightMatch(tag.name, searchTerm)}
                </button>
            `).join('');

        dropdown.innerHTML = html;
        dropdown.style.display = 'block';
        dropdown.style.position = 'absolute';
        dropdown.style.top = '100%';
        dropdown.style.left = '0';
        dropdown.style.right = '0';
        dropdown.style.zIndex = '1000';
    }

    highlightMatch(text, searchTerm) {
        const regex = new RegExp(`(${searchTerm})`, 'gi');
        return text.replace(regex, '<strong>$1</strong>');
    }

    selectTag(tagId, tagName) {
        this.selectedTags.set(tagId, tagName);
        this.renderSelectedTags();
        //this.updateUI();

        if (tagId < 0) {
            this.newTagId--;
        }

        // Clear search input
        document.getElementById(this.config.tagSearchId).value = '';
        document.getElementById(this.config.tagDropdownId).style.display = 'none';
    }

    removeTag(tagId) {
        this.selectedTags.delete(tagId);
        this.renderSelectedTags();
        //this.updateUI();
    }

    renderSelectedTags() {
        const container = document.getElementById(this.config.selectedTagsContainerId);

        if (this.selectedTags.size === 0) {
            container.innerHTML = '';
            return;
        }

        const html = Array.from(this.selectedTags.entries()).map(([tagId, tagName]) => `
                <span class="badge bg-primary me-1 mb-1 selected-tag">
                    ${tagName}
                    <button type="button" class="btn-close btn-close-white ms-1"
                            onclick="multiTagSelector.removeTag(${tagId})" style="font-size: 0.7em;"></button>
                </span>
            `).join('');

        container.innerHTML = html;

        const existingTagIds = [];
        const newTagValues = [];

        // Iterate through the map
        this.selectedTags.forEach((value, id) => {
            if (id > 0) {
                existingTagIds.push(id);
            } else if (id < 0) {
                newTagValues.push(value);
            }
        });

        document.getElementById('existingTagIds').value = existingTagIds.join(',');
        document.getElementById('newTagValues').value = newTagValues.join(',');
    }

    updateUI() {
        const count = this.selectedTags.size;
        const countElement = document.getElementById('selectedTagsCount');
        const viewButton = document.getElementById('viewTagsBtn');

        if (count === 0) {
            countElement.textContent = 'No tags selected';
            viewButton.disabled = true;
        } else {
            countElement.textContent = `${count} tag${count === 1 ? '' : 's'} selected`;
            viewButton.disabled = false;
        }
    }

    clear() {
        this.selectedTags.clear();
        this.renderSelectedTags();
        //this.updateUI();
        document.getElementById(this.config.tagSearchId).value = '';
        document.getElementById(this.config.tagDropdownId).style.display = 'none';
    }

    viewSelected() {
        if (this.selectedTags.size === 0) return;

        const tagIds = Array.from(this.selectedTags.keys()).join(',');
        const url = `/Video/ByMultipleTags?tagIds=${tagIds}`;
        window.location.href = url;
    }
}