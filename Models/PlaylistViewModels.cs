using System.ComponentModel.DataAnnotations;

namespace VideoLibrary.Models
{
    public class SavedPlaylistViewModel
    {
        public Playlist Playlist { get; set; } = null!;
        public int VideoCount => Playlist.VideoCount();
    }

    public class SavedPlaylistListViewModel
    {
        public List<SavedPlaylistViewModel> Playlists { get; set; } = new();
        public int TotalPlaylists => Playlists.Count;
        public int TotalVideos => Playlists.Sum(p => p.VideoCount);
    }

    public class CreatePlaylistViewModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string VideoIds { get; set; } = string.Empty;

        public bool IsShuffled { get; set; }

        public int? TagId { get; set; }
    }
}
