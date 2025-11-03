# JavaScript Utilities Documentation

This document describes the reusable JavaScript utilities extracted from the VideoLibrary views.

## Overview

The following utility modules have been created to reduce code duplication and improve maintainability:

1. **toast.js** - Toast notifications
2. **titleEditor.js** - Inline title editing
3. **tagEditor.js** - Tag editing interface
4. **viewToggler.js** - View/tab switching
5. **playlistUtils.js** - Playlist management
6. **statusToggler.js** - Status toggling (archived/active)
7. **contentLoader.js** - Lazy content loading

## Installation

Add the scripts to your layout or individual views in the correct order:

```html
<!-- Required dependencies -->
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js"></script>

<!-- Utility scripts -->
<script src="~/js/toast.js"></script>
<script src="~/js/titleEditor.js"></script>
<script src="~/js/tagEditor.js"></script>
<script src="~/js/viewToggler.js"></script>
<script src="~/js/playlistUtils.js"></script>
<script src="~/js/statusToggler.js"></script>
<script src="~/js/contentLoader.js"></script>

<!-- Existing script -->
<script src="~/js/multiTagSelector.js"></script>
```

## 1. Toast Notifications (toast.js)

### Usage

```javascript
// Simple usage
Toast.success('Operation completed!');
Toast.error('Something went wrong');
Toast.warning('Please review this');
Toast.info('Here is some information');

// With custom duration (in milliseconds)
Toast.success('Saved!', 5000);

// Generic method
Toast.show('Custom message', 'success', 3000);
```

### Legacy Support

The old `showToast()` function is still supported:

```javascript
showToast('Message', 'success');
```

## 2. Title Editor (titleEditor.js)

### HTML Structure Required

```html
<div id="titleDisplay" class="d-flex align-items-center">
    <h2>Original Title</h2>
    <button id="editTitleBtn" class="btn btn-sm btn-link">
        <i class="bi bi-pencil"></i>
    </button>
</div>

<form id="titleEditForm" class="d-none" method="post">
    <input type="hidden" name="id" value="123" />
    <input type="text" id="titleInput" name="title" value="Original Title" />
    <button type="submit" class="btn btn-sm btn-primary">Save</button>
    <button type="button" id="cancelEditBtn" class="btn btn-sm btn-secondary">Cancel</button>
</form>
```

### Usage

```javascript
document.addEventListener('DOMContentLoaded', function() {
    const titleEditor = new TitleEditor({
        titleDisplayId: 'titleDisplay',
        titleEditFormId: 'titleEditForm',
        editButtonId: 'editTitleBtn',
        cancelButtonId: 'cancelEditBtn',
        titleInputId: 'titleInput',
        originalTitle: '@Model.Video.Title',
        updateUrl: '/Video/EditTitle',
        titleElementSelector: 'h2'  // optional, defaults to 'h2'
    });
});
```

### Features

- Enter/Escape keyboard shortcuts
- AJAX form submission
- Automatic toast notifications
- Validates and displays errors

## 3. Tag Editor (tagEditor.js)

### HTML Structure Required

```html
<div id="tagsView">
    <!-- Tag display -->
</div>

<div id="tagsEdit" style="display: none">
    <input type="text" id="tagSearch" placeholder="Search tags..." />
    <div id="selectedTags"></div>
</div>
```

### Usage

Works in conjunction with `MultiTagSelector`:

```javascript
let multiTagSelector;
let tagEditor;

document.addEventListener('DOMContentLoaded', function() {
    // Initialize MultiTagSelector first
    multiTagSelector = new MultiTagSelector({
        tagSearchId: 'tagSearch',
        tagDropdownId: 'tagDropdown',
        tagData: @Html.Raw(Json.Serialize(Model.Tags)),
        selectedTagsContainerId: 'selectedTags'
    });
    
    // Initialize TagEditor
    tagEditor = new TagEditor({
        tagsViewId: 'tagsView',
        tagsEditId: 'tagsEdit',
        tagSearchId: 'tagSearch',
        multiTagSelector: multiTagSelector,
        getAllTagsUrl: '/Video/GetAllTags'
    });
    
    // Set as global for onclick handlers
    globalTagEditor = tagEditor;
});
```

### Legacy Functions

These global functions are still available:

```javascript
editTags();           // Enter edit mode
cancelTagsEdit();     // Cancel edit mode
addTagToSelection(id, name);  // Add a tag
```

## 4. View Toggler (viewToggler.js)

### HTML Structure Required

```html
<div class="btn-group">
    <button id="detailsBtn" class="btn btn-outline-primary active">Details</button>
    <button id="transcriptionBtn" class="btn btn-outline-primary">Transcript</button>
</div>

<div id="detailsView">
    <!-- Details content -->
</div>

<div id="transcriptionView" class="d-none">
    <!-- Transcription content -->
</div>
```

### Usage

```javascript
document.addEventListener('DOMContentLoaded', function() {
    const viewToggler = new ViewToggler({
        views: [
            {
                buttonId: 'detailsBtn',
                viewId: 'detailsView'
            },
            {
                buttonId: 'transcriptionBtn',
                viewId: 'transcriptionView',
                onActivate: function() {
                    // This runs only once when first activated
                    loadTranscription();
                }
            }
        ]
    });
});
```

### Legacy Function

```javascript
showView(activeBtn, inactiveBtn, activeView, inactiveView);
```

## 5. Playlist Utils (playlistUtils.js)

### Usage

```javascript
// Show the playlist creation modal
PlaylistUtils.showPlaylistModal();

// Select/deselect all videos
PlaylistUtils.selectAllVideos();
PlaylistUtils.deselectAllVideos();

// Initialize listeners (call on DOMContentLoaded)
PlaylistUtils.initializeCheckboxListeners();
PlaylistUtils.initializeRadioListeners();
PlaylistUtils.updatePlaylistLinks();
```

### Global Functions

```javascript
showPlaylistModal();
selectAllVideos();
deselectAllVideos();
createCustomPlaylist();
updatePlaylistLinks();
```

## 6. Status Toggler (statusToggler.js)

### HTML Structure Required

```html
<span class="setArchivedStatus badge bg-success" 
      data-id="123" 
      data-valuetoset="True">
    Current
</span>
```

### Usage

```javascript
document.addEventListener('DOMContentLoaded', function() {
    // Initialize with defaults
    const statusToggler = new StatusToggler();
    
    // Or with custom configuration
    const customToggler = new StatusToggler({
        selector: '.customStatusToggle',
        updateUrl: '/Custom/UpdateStatus',
        labels: {
            archived: 'Archived',
            active: 'Active'
        },
        classes: {
            archived: 'bg-secondary',
            active: 'bg-primary'
        }
    });
});
```

### Transcription Requester

```javascript
document.addEventListener('DOMContentLoaded', function() {
    const transcriptionRequester = new TranscriptionRequester({
        selector: '.requestTranscription',
        updateUrl: '/Video/RequestTranscript'
    });
});
```

## 7. Content Loader (contentLoader.js)

### Usage

```javascript
const contentLoader = new ContentLoader({
    contentId: 'transcriptionContent',
    loaderId: 'transcriptionLoader',
    placeholderId: 'transcriptionPlaceholder',
    loadUrl: `/Content/EmbeddedView?id=${videoId}`,
    onLoad: function() {
        console.log('Content loaded successfully');
    },
    onError: function(error) {
        console.error('Failed to load:', error);
    }
});

// Load the content
contentLoader.load();

// Check if loaded
if (contentLoader.isLoaded()) {
    console.log('Already loaded');
}

// Reset
contentLoader.reset();
```

### Helper Function

```javascript
// Quick transcription loader
const transcriptionLoader = createTranscriptionLoader(videoId);
transcriptionLoader.load();
```

## Migration Guide

### Example: Video Details Page

**Before:**
```html
<script>
    function showToast(message, type) {
        alert(message);
    }
    
    document.addEventListener('DOMContentLoaded', function() {
        const titleDisplay = document.getElementById('titleDisplay');
        // ... 50+ lines of code ...
    });
</script>
```

**After:**
```html
<script src="~/js/toast.js"></script>
<script src="~/js/titleEditor.js"></script>
<script src="~/js/tagEditor.js"></script>
<script src="~/js/multiTagSelector.js"></script>

<script>
    let multiTagSelector, tagEditor;
    
    document.addEventListener('DOMContentLoaded', function() {
        // Title editor
        new TitleEditor({
            titleDisplayId: 'titleDisplay',
            titleEditFormId: 'titleEditForm',
            editButtonId: 'editTitleBtn',
            cancelButtonId: 'cancelEditBtn',
            titleInputId: 'titleInput',
            originalTitle: '@Model.Video.Title',
            updateUrl: '/Video/EditTitle'
        });
        
        // Tag editor
        multiTagSelector = new MultiTagSelector({
            tagSearchId: 'tagSearch',
            tagDropdownId: 'tagDropdown',
            tagData: @Html.Raw(Json.Serialize(Model.Video.VideoTags)),
            selectedTagsContainerId: 'selectedTags'
        });
        
        tagEditor = new TagEditor({
            multiTagSelector: multiTagSelector
        });
        globalTagEditor = tagEditor;
    });
</script>
```

## Best Practices

1. **Load order matters** - Load toast.js first as other utilities may depend on it
2. **Global instances** - Store instances in global variables if you need to access them from onclick handlers
3. **Initialization** - Always initialize in `DOMContentLoaded` event
4. **Error handling** - Utilities will log errors to console, check browser console for debugging
5. **Customization** - All utilities accept configuration objects for customization

## Browser Compatibility

These utilities require:
- ES6 support (modern browsers)
- Bootstrap 5.x
- Bootstrap Icons (for icons)

## Future Enhancements

Consider adding:
- TypeScript definitions
- Unit tests
- Minified versions
- NPM package distribution
