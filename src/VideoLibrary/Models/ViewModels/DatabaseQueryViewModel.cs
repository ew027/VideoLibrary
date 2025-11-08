using System.ComponentModel.DataAnnotations;

namespace VideoLibrary.Models.ViewModels
{
    public class DatabaseQueryViewModel
    {
        [Required]
        [Display(Name = "SQL Query")]
        public string Query { get; set; } = string.Empty;

        public QueryResult? QueryResults { get; set; }
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public List<PredefinedQuery> PredefinedQueries { get; set; } = new();
    }

    public class QueryResult
    {
        public List<string> Columns { get; set; } = new();
        public List<Dictionary<string, object>> Rows { get; set; } = new();
        public int RowCount { get; set; }
    }

    public class PredefinedQuery
    {
        public string Name { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
    }

    public enum QueryType
    {
        Unknown,
        Select,
        Insert,
        Update,
        Delete
    }
}