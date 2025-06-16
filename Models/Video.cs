using System.ComponentModel.DataAnnotations;

namespace VideoLibrary.Models
{
    public class Video
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string FilePath { get; set; } = string.Empty;

        public string? ThumbnailPath { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.Now;

        public virtual ICollection<VideoTag> VideoTags { get; set; } = new List<VideoTag>();
    }
}
