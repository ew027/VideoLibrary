// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Video library JavaScript functionality

document.addEventListener('DOMContentLoaded', function () {
    // Add loading indicators for thumbnail refresh
    const refreshButtons = document.querySelectorAll('form[action*="RefreshThumbnail"] button[type="submit"]');
    refreshButtons.forEach(button => {
        button.addEventListener('click', function () {
            this.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span> Refreshing...';
            this.disabled = true;
        });
    });

    // Add confirmation for tag updates
    const tagForms = document.querySelectorAll('form[action*="UpdateTags"]');
    tagForms.forEach(form => {
        form.addEventListener('submit', function (e) {
            const checkedBoxes = form.querySelectorAll('input[type="checkbox"]:checked');
            if (checkedBoxes.length === 0) {
                if (!confirm('This will remove all tags from the video. Continue?')) {
                    e.preventDefault();
                }
            }
        });
    });

    // Auto-focus on tag name input
    const tagNameInput = document.querySelector('input[name="tagName"]');
    if (tagNameInput) {
        tagNameInput.focus();
    }
});