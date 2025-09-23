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

        // New video metadata properties
        public long? FileSizeBytes { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public double? DurationSeconds { get; set; }
        public string? VideoCodec { get; set; }
        public string? AudioCodec { get; set; }
        public long? BitRate { get; set; }

        // New Notes property
        public string? Notes { get; set; }

        public int ViewCount { get; set; } = 0;

        // Helper properties for display
        public string FileSizeFormatted => FileSizeBytes.HasValue ? FormatFileSize(FileSizeBytes.Value) : "Unknown";
        public string DurationFormatted => DurationSeconds.HasValue ? FormatDuration(DurationSeconds.Value) : "Unknown";
        public string ResolutionFormatted => Width.HasValue && Height.HasValue ? $"{Width}x{Height}" : "Unknown";

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private static string FormatDuration(double seconds)
        {
            var timeSpan = TimeSpan.FromSeconds(seconds);
            if (timeSpan.TotalHours >= 1)
                return timeSpan.ToString(@"h\:mm\:ss");
            else
                return timeSpan.ToString(@"m\:ss");
        }
    }
}
