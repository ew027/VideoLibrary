using System.ComponentModel.DataAnnotations;
using System.Text;

namespace VideoLibrary.Models
{
    public class Tag
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? ThumbnailPath { get; set; }

        public bool IsArchived { get; set; } = false;

        // Nested Set Model fields for hierarchical structure
        public int Left { get; set; }
        public int Right { get; set; }
        public int Level { get; set; }

        // Parent reference for easier UI operations and fallback queries
        public int? ParentId { get; set; }
        public virtual Tag? Parent { get; set; }
        public virtual ICollection<Tag> Children { get; set; } = new List<Tag>();

        public virtual ICollection<VideoTag> VideoTags { get; set; } = new List<VideoTag>();
        public virtual ICollection<GalleryTag> GalleryTags { get; set; } = new List<GalleryTag>();
        public virtual ICollection<PlaylistTag> PlaylistTags { get; set; } = new List<PlaylistTag>();
        public virtual ICollection<ContentTag> ContentTags { get; set; } = new List<ContentTag>();

        public string GetSummary()
        {
            var summary = new StringBuilder();

            if (VideoTags.Any())
            {
                summary.Append($"{VideoTags.Count}  video" + (VideoTags.Count > 1 ? "s" : "") + ", ");
            }

            if (PlaylistTags.Any())
            {
                summary.Append($"{PlaylistTags.Count}  playlist" + (PlaylistTags.Count > 1 ? "s" : "") + ", ");
            }

            if (GalleryTags.Any())
            {
                summary.Append(GalleryTags.Count + (GalleryTags.Count > 1 ? " galleries" : " gallery") + ", ");
            }

            if (ContentTags.Any())
            {
                summary.Append($"{ContentTags.Count}  content, ");
            }

            return summary.ToString()[..^2];
        }

        /// <summary>
        /// Check if this tag has any children
        /// </summary>
        public bool HasChildren()
        {
            return Right - Left > 1;
        }

        /// <summary>
        /// Get the number of descendants (not including self)
        /// </summary>
        public int GetDescendantCount()
        {
            return (Right - Left - 1) / 2;
        }

        /// <summary>
        /// Get display name with indentation based on level
        /// </summary>
        public string GetIndentedName(string indent = "  ")
        {
            return new string(' ', Level * indent.Length) + Name;
        }
    }
}
