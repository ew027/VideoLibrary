using System.ComponentModel.DataAnnotations;

namespace VideoLibrary.Models
{
    public class Gallery
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string FolderPath { get; set; } = string.Empty;

        public string ThumbnailPath {  get; set; } = string.Empty;

        public int ImageCount { get; set; }

        public string? Description { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.Now;

        public virtual ICollection<GalleryTag> GalleryTags { get; set; } = new List<GalleryTag>();
    }
}