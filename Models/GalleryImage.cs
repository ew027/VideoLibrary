namespace VideoLibrary.Models
{
    public class GalleryImage
    {
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public string MediumPath { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public long FileSize { get; set; }
        public int Index { get; set; }
    }
}