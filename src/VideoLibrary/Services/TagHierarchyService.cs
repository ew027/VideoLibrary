using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using VideoLibrary.Models;

namespace VideoLibrary.Services
{
    /// <summary>
    /// Service for managing hierarchical tag structure using Nested Set Model
    /// Optimized for fast queries at the expense of more complex updates
    /// </summary>
    public class TagHierarchyService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TagHierarchyService> _logger;

        public TagHierarchyService(AppDbContext context, ILogger<TagHierarchyService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ============================================
        // QUERY OPERATIONS (Optimized for Speed)
        // ============================================

        /// <summary>
        /// Get all descendants of a tag (including the tag itself by default)
        /// This is extremely fast with nested sets - single query, no recursion
        /// </summary>
        public async Task<List<Tag>> GetDescendantsAsync(int tagId, bool includeSelf = true)
        {
            var tag = await _context.Tags.FindAsync(tagId);
            if (tag == null) return new List<Tag>();

            var query = _context.Tags
                .Where(t => t.Left >= tag.Left && t.Right <= tag.Right);

            if (!includeSelf)
                query = query.Where(t => t.Id != tagId);

            return await query.OrderBy(t => t.Left).ToListAsync();
        }

        /// <summary>
        /// Get all descendant IDs - useful for filtering content by tag hierarchy
        /// Example: Get all videos tagged with "Movies" or any sub-category
        /// </summary>
        public async Task<List<int>> GetDescendantIdsAsync(int tagId, bool includeSelf = true)
        {
            var tag = await _context.Tags.FindAsync(tagId);
            if (tag == null) return new List<int>();

            var query = _context.Tags
                .Where(t => t.Left >= tag.Left && t.Right <= tag.Right);

            if (!includeSelf)
                query = query.Where(t => t.Id != tagId);

            return await query.Select(t => t.Id).ToListAsync();
        }

        /// <summary>
        /// Get all ancestors of a tag (path from root to tag, excluding the tag itself)
        /// Useful for breadcrumb navigation
        /// </summary>
        public async Task<List<Tag>> GetAncestorsAsync(int tagId, bool includeSelf = false)
        {
            var tag = await _context.Tags.FindAsync(tagId);
            if (tag == null) return new List<Tag>();

            var query = _context.Tags
                .Where(t => t.Left < tag.Left && t.Right > tag.Right);

            if (includeSelf)
                query = _context.Tags.Where(t =>
                    (t.Left < tag.Left && t.Right > tag.Right) || t.Id == tagId);

            return await query.OrderBy(t => t.Left).ToListAsync();
        }

        /// <summary>
        /// Get immediate children of a tag
        /// </summary>
        public async Task<List<Tag>> GetChildrenAsync(int? parentId)
        {
            return await _context.Tags
                .Where(t => t.ParentId == parentId)
                .OrderBy(t => t.Left)
                .ToListAsync();
        }

        /// <summary>
        /// Get root tags (tags with no parent)
        /// </summary>
        public async Task<List<Tag>> GetRootTagsAsync()
        {
            return await _context.Tags
                .Where(t => t.ParentId == null)
                .OrderBy(t => t.Left)
                .ToListAsync();
        }

        /// <summary>
        /// Get full tree structure ordered by nested set left value
        /// This gives you the tree in pre-order traversal
        /// </summary>
        public async Task<List<Tag>> GetFullTreeAsync()
        {
            return await _context.Tags
                .OrderBy(t => t.Left)
                .ToListAsync();
        }

        /// <summary>
        /// Get siblings of a tag (tags with the same parent)
        /// </summary>
        public async Task<List<Tag>> GetSiblingsAsync(int tagId, bool includeSelf = false)
        {
            var tag = await _context.Tags.FindAsync(tagId);
            if (tag == null) return new List<Tag>();

            var query = _context.Tags
                .Where(t => t.ParentId == tag.ParentId);

            if (!includeSelf)
                query = query.Where(t => t.Id != tagId);

            return await query.OrderBy(t => t.Left).ToListAsync();
        }

        /// <summary>
        /// Check if a tag is an ancestor of another tag
        /// </summary>
        public async Task<bool> IsAncestorOfAsync(int potentialAncestorId, int tagId)
        {
            var ancestor = await _context.Tags.FindAsync(potentialAncestorId);
            var tag = await _context.Tags.FindAsync(tagId);

            if (ancestor == null || tag == null) return false;

            return tag.Left > ancestor.Left && tag.Right < ancestor.Right;
        }

        /// <summary>
        /// Check if a tag is a descendant of another tag
        /// </summary>
        public async Task<bool> IsDescendantOfAsync(int potentialDescendantId, int tagId)
        {
            return await IsAncestorOfAsync(tagId, potentialDescendantId);
        }

        /// <summary>
        /// Get the depth/level of a tag in the hierarchy
        /// </summary>
        public async Task<int> GetDepthAsync(int tagId)
        {
            var tag = await _context.Tags.FindAsync(tagId);
            return tag?.Level ?? 0;
        }

        // ============================================
        // MODIFICATION OPERATIONS
        // ============================================

        public async Task<Tag> InsertTagAsync(string name, int? parentId = null, string? thumbnailPath = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (parentId.HasValue)
                {
                    var parent = await _context.Tags.FindAsync(parentId.Value);

                    if (parent is null)
                    {
                        throw new ArgumentException("Invalid parent tag ID");
                    }
                }

                var tag = new Tag
                {
                    Name = name,
                    Left = 0,
                    Right = 0,
                    Level = 0,
                    ParentId = parentId,
                    ThumbnailPath = thumbnailPath
                };

                _context.Tags.Add(tag);
                await _context.SaveChangesAsync();

                var newTagId = tag.Id;

                // Rebuild tree to assign correct left/right/level values
                await RebuildTreeInternalAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Inserted new tag {TagId} ('{Name}') under parent {ParentId}",
                    newTagId, name, parentId);

                return tag;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to insert new tag '{Name}' under parent {ParentId}",
                    name, parentId);
                throw;
            }
        }


        /// <summary>
        /// Move a tag (and its entire subtree) to a new parent
        /// SIMPLIFIED VERSION: Updates parent_id and rebuilds tree
        /// This is slower but much more reliable and easier to understand
        /// </summary>
        public async Task MoveTagAsync(int tagId, int? newParentId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Step 1: Validate the tag
                var tag = await _context.Tags.FindAsync(tagId);
                if (tag == null)
                    throw new ArgumentException($"Tag with ID {tagId} not found");

                // Step 2: Validate new parent
                if (newParentId.HasValue)
                {
                    if (newParentId.Value == tagId)
                        throw new InvalidOperationException("Cannot move tag to itself");

                    var newParent = await _context.Tags.FindAsync(newParentId.Value);
                    if (newParent == null)
                        throw new ArgumentException($"New parent tag with ID {newParentId.Value} not found");

                    // Check for circular reference using current tree structure
                    if (await IsAncestorOfAsync(tagId, newParentId.Value))
                        throw new InvalidOperationException("Cannot move tag to its own descendant");
                }

                // Step 3: Check if already at target parent
                if (tag.ParentId == newParentId)
                {
                    _logger.LogInformation("Tag {TagId} already at parent {ParentId}, skipping",
                        tagId, newParentId);
                    await transaction.RollbackAsync();
                    return;
                }

                // Step 4: Update parent reference
                var oldParentId = tag.ParentId;
                tag.ParentId = newParentId;
                await _context.SaveChangesAsync();

                // Step 5: Rebuild the entire tree to recalculate left/right/level values
                // This is the key: instead of trying to manually adjust all the values,
                // we just rebuild from the parent relationships
                await RebuildTreeInternalAsync();

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Moved tag {TagId} ('{Name}') from parent {OldParent} to parent {NewParent}",
                    tagId, tag.Name, oldParentId, newParentId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to move tag {TagId} to parent {ParentId}", tagId, newParentId);
                throw;
            }
        }

        /// <summary>
        /// Delete a tag and optionally its descendants
        /// </summary>
        public async Task DeleteTagAsync(int tagId, bool deleteDescendants = false)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var tag = await _context.Tags.FindAsync(tagId);

                if (tag == null)
                    throw new ArgumentException($"Tag with ID {tagId} not found");

                if (deleteDescendants)
                {
                    // Delete entire subtree
                    int subtreeSize = tag.Right - tag.Left + 1;

                    // Delete tags
                    await _context.Database.ExecuteSqlRawAsync(@"
                        DELETE FROM tags 
                        WHERE ""left"" >= {0} AND ""right"" <= {1}",
                        tag.Left, tag.Right);

                    _context.ChangeTracker.Clear();

                    _logger.LogInformation(
                        "Deleted tag {TagId} ('{Name}') and {Count} descendants",
                        tagId, tag.Name, (subtreeSize - 1) / 2);
                }
                else
                {
                    // Move children up one level before deleting
                    var children = await GetChildrenAsync(tagId);
                    foreach (var child in children)
                    {
                        child.ParentId = tag.ParentId;
                        child.Level = tag.Level;
                    }

                    _context.Tags.Remove(tag);

                    _logger.LogInformation(
                        "Deleted tag {TagId} ('{Name}'), promoted {Count} children",
                        tagId, tag.Name, children.Count);
                }

                await _context.SaveChangesAsync();

                // Rebuild tree to fix left/right/level values
                await RebuildTreeInternalAsync();

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to delete tag {TagId}", tagId);
                throw;
            }
        }

        /// <summary>
        /// Internal rebuild method that doesn't create its own transaction
        /// Used by MoveTagAsync which already has a transaction open
        /// </summary>
        private async Task RebuildTreeInternalAsync()
        {
            _logger.LogInformation("Rebuilding tree structure after move");

            var allTags = await _context.Tags.OrderBy(t => t.Name).ToListAsync();
            int counter = 0;

            void RebuildNode(Tag tag, int level)
            {
                tag.Left = ++counter;
                tag.Level = level;

                var children = allTags
                    .Where(t => t.ParentId == tag.Id)
                    .OrderBy(t => t.Name)
                    .ToList();

                foreach (var child in children)
                {
                    RebuildNode(child, level + 1);
                }

                tag.Right = ++counter;
            }

            var roots = allTags
                .Where(t => t.ParentId == null)
                .OrderBy(t => t.Name)
                .ToList();

            foreach (var root in roots)
            {
                RebuildNode(root, 0);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Tree rebuild completed. Processed {Count} tags", allTags.Count);
        }

        /// <summary>
        /// Public rebuild method (keeps existing implementation with its own transaction)
        /// </summary>
        public async Task RebuildTreeAsync()
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                await RebuildTreeInternalAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Tag tree structure rebuilt successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to rebuild tag tree");
                throw;
            }
        }

        /// <summary>
        /// Validate tree integrity - useful for debugging
        /// </summary>
        public async Task<List<string>> ValidateTreeAsync()
        {
            var errors = new List<string>();
            var tags = await _context.Tags.OrderBy(t => t.Left).ToListAsync();

            // Check for duplicate left/right values
            var leftValues = tags.GroupBy(t => t.Left).Where(g => g.Count() > 1);
            var rightValues = tags.GroupBy(t => t.Right).Where(g => g.Count() > 1);

            foreach (var group in leftValues)
            {
                errors.Add($"Duplicate left value {group.Key}: {string.Join(", ", group.Select(t => t.Name))}");
            }

            foreach (var group in rightValues)
            {
                errors.Add($"Duplicate right value {group.Key}: {string.Join(", ", group.Select(t => t.Name))}");
            }

            // Check that right > left for all nodes
            var invalidNodes = tags.Where(t => t.Right <= t.Left);
            foreach (var tag in invalidNodes)
            {
                errors.Add($"Tag '{tag.Name}' has invalid left/right values: {tag.Left}/{tag.Right}");
            }

            // Check that parent's left < child's left < child's right < parent's right
            var tagsWithParents = tags.Where(t => t.ParentId.HasValue);
            foreach (var tag in tagsWithParents)
            {
                var parent = tags.FirstOrDefault(t => t.Id == tag.ParentId);
                if (parent != null)
                {
                    if (!(parent.Left < tag.Left && tag.Right < parent.Right))
                    {
                        errors.Add($"Tag '{tag.Name}' not properly nested within parent '{parent.Name}'");
                    }
                }
            }

            return errors;
        }
    }

}
