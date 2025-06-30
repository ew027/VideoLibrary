namespace VideoLibrary.Models
{
    public class GalleryListViewModel
    {
        public Gallery Gallery { get; set; } = null!;
        public List<GalleryImage> Images { get; set; } = new();
        public int ImageCount { get; set; }
        public GalleryImage? CoverImage { get; set; }
    }

    public class GalleryDetailViewModel
    {
        public Gallery Gallery { get; set; } = null!;
        public List<GalleryImage> Images { get; set; } = new();
        public int CurrentImageIndex { get; set; }
        public GalleryImage? CurrentImage { get; set; }
        public GalleryImage? PreviousImage { get; set; }
        public GalleryImage? NextImage { get; set; }
        public bool HasPrevious => CurrentImageIndex > 0;
        public bool HasNext => CurrentImageIndex < Images.Count - 1;
    }

    public class TagWithContentViewModel
    {
        public Tag Tag { get; set; } = null!;
        public List<Video> Videos { get; set; } = new();
        public List<Gallery> Galleries { get; set; } = new();
        public int TotalVideos => Videos.Count;
        public int TotalGalleries => Galleries.Count;
        public int TotalItems => TotalVideos + TotalGalleries;
    }
}