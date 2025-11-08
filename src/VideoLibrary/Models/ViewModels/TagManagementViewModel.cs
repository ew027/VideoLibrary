namespace VideoLibrary.Models.ViewModels
{
    public class TagManagementViewModel
    {
        public int TotalTags { get; set; }
        public int EmptyTags { get; set; }
        public int TagsWithContent { get; set; }
    }

    public class TagDetailViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public string? ParentName { get; set; }
        public int Level { get; set; }
        public int VideoCount { get; set; }
        public int GalleryCount { get; set; }
        public int PlaylistCount { get; set; }
        public int ContentCount { get; set; }
        public int TotalContentCount { get; set; }
        public int ChildCount { get; set; }
        public int DescendantCount { get; set; }
        public bool IsEmpty { get; set; }
        public string? ThumbnailPath { get; set; }
        public bool IsArchived { get; set; }
    }

    public class TagHierarchyDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public int Level { get; set; }
        public int Left { get; set; }
        public int Right { get; set; }
        public int ContentCount { get; set; }
        public bool IsEmpty { get; set; }
        public bool IsArchived { get; set; }
        public string? ThumbnailPath { get; set; }
        public List<TagHierarchyDto> Children { get; set; } = new List<TagHierarchyDto>();
    }

    public class MoveTagRequest
    {
        public int TagId { get; set; }
        public int? NewParentId { get; set; }
        public int? AfterTagId { get; set; } // For ordering among siblings
    }

    public class RenameTagRequest
    {
        public int TagId { get; set; }
        public string NewName { get; set; } = string.Empty;
    }

    public class DeleteTagRequest
    {
        public int TagId { get; set; }
        public bool DeleteDescendants { get; set; }
    }

    public class AddTagRequest
    {
        public string Name { get; set; } = string.Empty;
        public int? ParentId { get; set; }
    }
}
