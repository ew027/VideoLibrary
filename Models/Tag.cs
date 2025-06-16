using System.ComponentModel.DataAnnotations;

namespace VideoLibrary.Models
{
    public class Tag
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? ThumbnailPath { get; set; }

        public virtual ICollection<VideoTag> VideoTags { get; set; } = new List<VideoTag>();
    }
}
