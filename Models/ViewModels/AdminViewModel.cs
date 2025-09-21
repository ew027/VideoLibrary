namespace VideoLibrary.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalVideos { get; set; }
        public int VideosWithThumbnails { get; set; }
        public int VideosWithAnalysis { get; set; }
        public int TotalTags { get; set; }
        public int TagsWithThumbnails { get; set; }

        // Add these new properties
        public int TotalGalleries { get; set; }
        public int GalleriesWithTags { get; set; }

        public int VideosWithoutThumbnails => TotalVideos - VideosWithThumbnails;
        public int VideosWithoutAnalysis => TotalVideos - VideosWithAnalysis;
        public int TagsWithoutThumbnails => TotalTags - TagsWithThumbnails;
        public int GalleriesWithoutTags => TotalGalleries - GalleriesWithTags;

        public double ThumbnailProgress => TotalVideos > 0 ? (double)VideosWithThumbnails / TotalVideos * 100 : 0;
        public double AnalysisProgress => TotalVideos > 0 ? (double)VideosWithAnalysis / TotalVideos * 100 : 0;
        public double GalleryTagProgress => TotalGalleries > 0 ? (double)GalleriesWithTags / TotalGalleries * 100 : 0;
    }

    public class AdminStatisticsViewModel
    {
        public List<CodecStat> VideosByCodec { get; set; } = new();
        public List<ResolutionStat> VideosByResolution { get; set; } = new();
        public long TotalFileSize { get; set; }
        public double TotalDuration { get; set; }
        public int VideosNeedingThumbnails { get; set; }
        public int VideosNeedingAnalysis { get; set; }

        public string TotalFileSizeFormatted => FormatFileSize(TotalFileSize);
        public string TotalDurationFormatted => FormatDuration(TotalDuration);

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private static string FormatDuration(double seconds)
        {
            var timeSpan = TimeSpan.FromSeconds(seconds);
            if (timeSpan.TotalDays >= 1)
                return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m";
            else if (timeSpan.TotalHours >= 1)
                return timeSpan.ToString(@"h\:mm\:ss");
            else
                return timeSpan.ToString(@"m\:ss");
        }
    }

    public class CodecStat
    {
        public string Codec { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ResolutionStat
    {
        public string Resolution { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}