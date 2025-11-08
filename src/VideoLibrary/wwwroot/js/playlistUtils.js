/**
 * Playlist Utilities
 * Provides functionality for creating and managing playlists
 */

const PlaylistUtils = {
    /**
     * Show the playlist modal
     * @param {string} modalId - ID of the modal element (default: 'playlistModal')
     */
    showPlaylistModal: function(modalId = 'playlistModal') {
        const modal = new bootstrap.Modal(document.getElementById(modalId));
        this.updateSelectedCount();
        modal.show();
    },

    /**
     * Select all videos in the playlist modal
     */
    selectAllVideos: function() {
        const checkboxes = document.querySelectorAll('#playlistModal input[type="checkbox"]');
        checkboxes.forEach(cb => {
            cb.checked = true;
        });
        this.updateSelectedCount();
    },

    /**
     * Deselect all videos in the playlist modal
     */
    deselectAllVideos: function() {
        const checkboxes = document.querySelectorAll('#playlistModal input[type="checkbox"]');
        checkboxes.forEach(cb => {
            cb.checked = false;
        });
        this.updateSelectedCount();
    },

    /**
     * Update the selected video count display
     */
    updateSelectedCount: function() {
        const checkboxes = document.querySelectorAll('#playlistModal input[type="checkbox"]');
        const selectedCount = Array.from(checkboxes).filter(cb => cb.checked).length;
        const totalCount = checkboxes.length;

        const countElement = document.getElementById('selectedCount');
        if (countElement) {
            countElement.textContent = selectedCount;
        }

        // Enable/disable create button based on selection
        const createBtn = document.getElementById('createPlaylistBtn');
        if (createBtn) {
            createBtn.disabled = selectedCount === 0;
        }
    },

    /**
     * Create a custom playlist from selected videos
     */
    createCustomPlaylist: function() {
        const checkboxes = document.querySelectorAll('#playlistModal input[type="checkbox"]:checked');
        const selectedVideos = Array.from(checkboxes).map(cb => cb.value);

        if (selectedVideos.length === 0) {
            if (typeof Toast !== 'undefined') {
                Toast.warning('Please select at least one video');
            } else {
                alert('Please select at least one video');
            }
            return;
        }

        // Get selected order
        const orderRadio = document.querySelector('input[name="playlistOrder"]:checked');
        const order = orderRadio ? orderRadio.value : 'order';

        // Build URL
        const videoIds = selectedVideos.join(',');
        const url = `/Playlist/Create?videoIds=${videoIds}&order=${order}`;

        // Navigate to create playlist page
        window.location.href = url;
    },

    /**
     * Initialize playlist checkbox event listeners
     */
    initializeCheckboxListeners: function() {
        const checkboxes = document.querySelectorAll('#playlistModal input[type="checkbox"]');
        checkboxes.forEach(cb => {
            cb.addEventListener('change', () => this.updateSelectedCount());
        });
    },

    /**
     * Initialize radio button styling for playlist order
     */
    initializeRadioListeners: function() {
        const radioButtons = document.querySelectorAll('input[name="playlistOrder"]');
        radioButtons.forEach(radio => {
            radio.addEventListener('change', function() {
                radioButtons.forEach(r => {
                    r.parentElement.classList.toggle('active', r.checked);
                });
            });
        });
    },

    /**
     * Update playlist links to support Ctrl+Click for custom playlists
     */
    updatePlaylistLinks: function() {
        document.querySelectorAll('.playAllLink').forEach(link => {
            link.addEventListener('click', function(e) {
                if (e.ctrlKey || e.metaKey) {
                    // Ctrl+Click opens modal for custom selection
                    e.preventDefault();
                    const originalHref = this.getAttribute('href');
                    
                    // Show modal
                    PlaylistUtils.showPlaylistModal();
                    
                    // Update create button to use the tag
                    const createBtn = document.getElementById('createPlaylistBtn');
                    if (createBtn) {
                        createBtn.onclick = function() {
                            e.preventDefault();
                            const url = new URL(originalHref);
                            const tagId = url.searchParams.get('tagId') || url.pathname.split('/').pop();
                            window.location.href = `/Playlist/Create?tagId=${tagId}&order=order`;
                        };
                    }
                }
                // Normal click plays immediately
            });
        });
    }
};

// Global functions for backward compatibility
function showPlaylistModal() {
    PlaylistUtils.showPlaylistModal();
}

function selectAllVideos() {
    PlaylistUtils.selectAllVideos();
}

function deselectAllVideos() {
    PlaylistUtils.deselectAllVideos();
}

function updateSelectedCount() {
    PlaylistUtils.updateSelectedCount();
}

function createCustomPlaylist() {
    PlaylistUtils.createCustomPlaylist();
}

function updatePlaylistLinks() {
    PlaylistUtils.updatePlaylistLinks();
}
