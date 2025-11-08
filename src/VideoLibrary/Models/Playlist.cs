using System.ComponentModel.DataAnnotations;

namespace VideoLibrary.Models
{
    public class Playlist
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string VideoIds { get; set; } = string.Empty; // Comma-separated video IDs

        public bool IsShuffled { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.Now;

        public DateTime DateLastPlayed { get; set; } = DateTime.Now;

        public string? ThumbnailPath { get; set; }

        public virtual ICollection<PlaylistTag> PlaylistTags { get; set; } = new List<PlaylistTag>();

        public int PlayCount { get; set; } = 0;

        // Helper properties
        public List<int> GetVideoIdList()
        {
            if (string.IsNullOrEmpty(VideoIds))
                return new List<int>();

            return VideoIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id.Trim(), out var parsed) ? parsed : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .ToList();
        }

        public int VideoCount()
        {
            if (string.IsNullOrEmpty(VideoIds))
                return 0;

            return VideoIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id.Trim(), out var parsed) ? parsed : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .Count();
        }

        public int FirstVideoId()
        {
            if (string.IsNullOrEmpty(VideoIds))
                return 0;

            return VideoIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id.Trim(), out var parsed) ? parsed : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .FirstOrDefault();
        }

        public void SetVideoIds(IEnumerable<int> videoIds)
        {
            VideoIds = string.Join(",", videoIds);
        }
    }
}