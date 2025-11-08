namespace VideoLibrary.Models.ViewModels
{
    public class ContentWithContextViewModel
    {
        public Content Content { get; set; }
        public List<string> MatchingSnippets { get; set; }
    }
}
