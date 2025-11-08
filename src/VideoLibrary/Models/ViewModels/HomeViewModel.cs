namespace VideoLibrary.Models.ViewModels
{
    // ViewModels/HomeViewModel.cs

    public class HomeViewModel
    {
        public List<VideoCardViewModel> RecentVideos { get; set; } = new List<VideoCardViewModel>();
        public List<VideoCardViewModel> UnwatchedVideos { get; set; } = new List<VideoCardViewModel>();
        public List<VideoCardViewModel> RandomVideos { get; set; } = new List<VideoCardViewModel>();
        public List<PopularVideoViewModel> MostViewedVideos { get; set; } = new List<PopularVideoViewModel>();
    }

    public class VideoCardViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public List<TagViewModel> Tags { get; set; } = new List<TagViewModel>();
    }

    public class PopularVideoViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int ViewCount { get; set; }
    }
}
