namespace VideoLibrary.Models.ViewModels
{
    public class VideoViewModel
    {
        public Video Video { get; set; }
        public Tag Tag { get; set; }
        public List<Playlist> Playlists { get; set; }

        public Transcription? Transcription { get; set; }
    }
}
