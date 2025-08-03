namespace VideoLibrary.Models
{
    public class VideoViewModel
    {
        public Video Video { get; set; }
        public Tag Tag { get; set; }
        public List<Playlist> Playlists { get; set; }
    }
}
