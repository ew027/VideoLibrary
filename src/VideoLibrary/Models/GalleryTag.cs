namespace VideoLibrary.Models
{
    public class GalleryTag
    {
        public int GalleryId { get; set; }
        public virtual Gallery Gallery { get; set; } = null!;

        public int TagId { get; set; }
        public virtual Tag Tag { get; set; } = null!;
    }
}