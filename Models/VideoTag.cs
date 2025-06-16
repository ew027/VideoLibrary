namespace VideoLibrary.Models
{
    public class VideoTag
    {
        public int VideoId { get; set; }
        public virtual Video Video { get; set; } = null!;

        public int TagId { get; set; }
        public virtual Tag Tag { get; set; } = null!;
    }
}
