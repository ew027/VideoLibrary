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
    }
}
