namespace VideoLibrary.Models
{
    public class ContentTag
    {
        public int ContentId { get; set; }
        public virtual Content Content { get; set; } = null!;

        public int TagId { get; set; }
        public virtual Tag Tag { get; set; } = null!;
    }
}