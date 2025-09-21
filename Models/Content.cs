using System.ComponentModel.DataAnnotations;

namespace VideoLibrary.Models
{
    public class Content
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Content")]
        public string ContentText { get; set; } = string.Empty;

        [Display(Name = "Date Created")]
        public DateTime DateCreated { get; set; }

        [Display(Name = "Date Last Updated")]
        public DateTime DateLastUpdated { get; set; }

        public virtual ICollection<ContentTag> ContentTags { get; set; } = new List<ContentTag>();
    }
}
