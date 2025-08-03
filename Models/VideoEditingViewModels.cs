namespace VideoLibrary.Models
{
    public class VideoEditingViewModel
    {
        public Video Video { get; set; } = null!;
        public List<ClipData> Clips { get; set; } = new();
    }

    public class ClipData
    {
        public double Start { get; set; }
        public double Duration { get; set; }
        public double End => Start + Duration;

        public string StartFormatted => FormatTime(Start);
        public string DurationFormatted => FormatTime(Duration);
        public string EndFormatted => FormatTime(End);

        private static string FormatTime(double seconds)
        {
            var timeSpan = TimeSpan.FromSeconds(seconds);
            if (timeSpan.TotalHours >= 1)
                return timeSpan.ToString(@"h\:mm\:ss");
            else
                return timeSpan.ToString(@"m\:ss");
        }
    }

    public class ClipExportData
    {
        public string VideoName { get; set; } = string.Empty;
        public string VideoPath { get; set; } = string.Empty;
        public List<ClipExportItem> Clips { get; set; } = new();
    }

    public class ClipExportItem
    {
        public double Start { get; set; }
        public double Duration { get; set; }
    }
}