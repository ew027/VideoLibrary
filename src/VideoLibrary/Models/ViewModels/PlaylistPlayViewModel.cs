namespace VideoLibrary.Models.ViewModels
{
    public class PlaylistPlayViewModel
    {
        public int PlaylistId { get; set; }
        public int? TagId { get; set; }
        public string TagName { get; set; } = string.Empty;
        public List<Video> Videos { get; set; } = new();
        public int CurrentIndex { get; set; } = 0;
        public bool IsRandom { get; set; } = false;
        public bool IsRecent { get; set; } = false;
        public bool IsAll { get; set; } = false;
        public List<PlaylistTag> PlaylistTags { get; set; } = new();

        public Video? CurrentVideo => Videos.Count > CurrentIndex ? Videos[CurrentIndex] : null;
        public bool HasNext => CurrentIndex < Videos.Count - 1;
        public bool HasPrevious => CurrentIndex > 0;
        public Video? NextVideo => HasNext ? Videos[CurrentIndex + 1] : null;
        public Video? PreviousVideo => HasPrevious ? Videos[CurrentIndex - 1] : null;
    }
}