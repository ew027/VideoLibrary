namespace VideoLibrary.Models.ViewModels
{
    /// <summary>
    /// View model for the grouped tags page
    /// </summary>
    public class GroupedTagsViewModel
    {
        public List<TagGroupViewModel> RootTags { get; set; } = new List<TagGroupViewModel>();
        public List<TagCardViewModel> OrphanTags { get; set; } = new List<TagCardViewModel>();
        public List<Tag> AllTags { get; set; } = new List<Tag>(); // For the multi-select filter
    }

    /// <summary>
    /// Represents a root tag group (Movies, TV, Shorts)
    /// </summary>
    public class TagGroupViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public int ChildCount { get; set; }
        public int DirectContentCount { get; set; } // Content tagged directly to this root tag
        public int TotalCount { get; set; } // Direct + all descendants
        public List<TagCardViewModel> Children { get; set; } = new List<TagCardViewModel>();
    }

    /// <summary>
    /// Represents a tag card (for children and orphans)
    /// </summary>
    public class TagCardViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public int ContentCount { get; set; }
        public string Summary { get; set; } = string.Empty;
        public int Level { get; set; }
        public int? ParentId { get; set; }
    }
}
