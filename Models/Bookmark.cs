using System.ComponentModel.DataAnnotations;

namespace VideoLibrary.Models
{
    public class Bookmark
    {
        public int Id { get; set; }

        public int VideoId { get; set; }

        // Time in seconds
        public double TimeInSeconds { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public Video Video { get; set; } = null!;
    }
}