namespace VideoLibrary.Models
{
    public class PlaylistTag
    {
        public int PlaylistId { get; set; }
        public virtual Playlist Playlist { get; set; } = null!;

        public int TagId { get; set; }
        public virtual Tag Tag { get; set; } = null!;
    }
}