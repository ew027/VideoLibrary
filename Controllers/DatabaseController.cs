using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Threading.Tasks;
using VideoLibrary.Models;
using VideoLibrary.Models.ViewModels;

namespace VideoLibrary.Controllers
{
    [Authorize]
    public class DatabaseController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DatabaseController> _logger;

        private static int? SqlTagId;

        public DatabaseController(AppDbContext context, ILogger<DatabaseController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DatabaseQueryViewModel
            {
                Query = "SELECT * FROM Videos LIMIT 10;",
                PredefinedQueries = await GetPredefinedQueries()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteQuery(DatabaseQueryViewModel model)
        {
            model.PredefinedQueries = await GetPredefinedQueries();

            if (string.IsNullOrWhiteSpace(model.Query))
            {
                ModelState.AddModelError(nameof(model.Query), "Query cannot be empty.");
                return View("Index", model);
            }

            try
            {
                var trimmedQuery = model.Query.Trim();
                var queryType = GetQueryType(trimmedQuery);

                _logger.LogInformation("Executing database query: {QueryType}", queryType);

                switch (queryType)
                {
                    case QueryType.Select:
                        await ExecuteSelectQuery(model, trimmedQuery);
                        break;
                    case QueryType.Insert:
                    case QueryType.Update:
                    case QueryType.Delete:
                        await ExecuteModificationQuery(model, trimmedQuery, queryType);
                        break;
                    default:
                        model.ErrorMessage = "Unsupported query type. Only SELECT, INSERT, UPDATE, and DELETE are allowed.";
                        break;
                }
            }
            catch (Exception ex)
            {
                model.ErrorMessage = $"Query execution failed: {ex.Message}";
                _logger.LogError(ex, "Database query execution failed");
            }

            return View("Index", model);
        }

        private async Task ExecuteSelectQuery(DatabaseQueryViewModel model, string query)
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = query;
            command.CommandType = CommandType.Text;

            await _context.Database.OpenConnectionAsync();

            using var reader = await command.ExecuteReaderAsync();

            var results = new List<Dictionary<string, object>>();
            var columns = new List<string>();

            // Get column names
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            // Read data
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[columns[i]] = value;
                }
                results.Add(row);
            }

            model.QueryResults = new QueryResult
            {
                Columns = columns,
                Rows = results,
                RowCount = results.Count
            };

            await _context.Database.CloseConnectionAsync();

            model.SuccessMessage = $"Query executed successfully. {results.Count} row(s) returned.";
        }

        private async Task ExecuteModificationQuery(DatabaseQueryViewModel model, string query, QueryType queryType)
        {
            var rowsAffected = await _context.Database.ExecuteSqlRawAsync(query);

            model.SuccessMessage = queryType switch
            {
                QueryType.Insert => $"Insert completed successfully. {rowsAffected} row(s) inserted.",
                QueryType.Update => $"Update completed successfully. {rowsAffected} row(s) updated.",
                QueryType.Delete => $"Delete completed successfully. {rowsAffected} row(s) deleted.",
                _ => $"Query completed successfully. {rowsAffected} row(s) affected."
            };
        }

        private QueryType GetQueryType(string query)
        {
            var firstWord = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToUpperInvariant();

            return firstWord switch
            {
                "SELECT" => QueryType.Select,
                "INSERT" => QueryType.Insert,
                "UPDATE" => QueryType.Update,
                "DELETE" => QueryType.Delete,
                _ => QueryType.Unknown
            };
        }

        private async Task<List<PredefinedQuery>> GetPredefinedQueries()
        {
            if (SqlTagId is null)
            {
                var sqlTag = await _context.Tags.FirstOrDefaultAsync(x => x.Name == "sql");

                if (sqlTag != null)
                {
                    SqlTagId = sqlTag.Id;
                }
            }

            var queries = await _context.Contents
                            .Where(c => c.ContentTags.Any(ct => ct.TagId == SqlTagId))
                            .Select(c => new PredefinedQuery() { Name = c.Title, Query = c.ContentText })
                            .ToListAsync();

            return queries;
             
            /*
            return new List<PredefinedQuery>
            {
                new() { Name = "All Videos", Query = "SELECT Id, Title, DurationFormatted, ResolutionFormatted, FileSizeFormatted FROM Videos ORDER BY Title;" },
                new() { Name = "Videos without Thumbnails", Query = "SELECT Id, Title, FilePath FROM Videos WHERE ThumbnailPath IS NULL OR ThumbnailPath = '';" },
                new() { Name = "Videos without Analysis", Query = "SELECT Id, Title, FilePath FROM Videos WHERE DurationSeconds IS NULL;" },
                new() { Name = "All Tags with Video Count", Query = "SELECT t.Id, t.Name, COUNT(vt.VideoId) as VideoCount FROM Tags t LEFT JOIN VideoTags vt ON t.Id = vt.TagId GROUP BY t.Id, t.Name ORDER BY VideoCount DESC;" },
                new() { Name = "Videos by Tag", Query = "SELECT v.Title, t.Name as TagName FROM Videos v INNER JOIN VideoTags vt ON v.Id = vt.VideoId INNER JOIN Tags t ON vt.TagId = t.Id ORDER BY t.Name, v.Title;" },
                new() { Name = "Database Schema - Tables", Query = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;" },
                new() { Name = "Database Schema - Videos Table", Query = "PRAGMA table_info(Videos);" },
                new() { Name = "Recently Added Videos", Query = "SELECT Id, Title, DateAdded FROM Videos ORDER BY DateAdded DESC LIMIT 20;" },
                new() { Name = "Largest Videos by File Size", Query = "SELECT Title, FileSizeFormatted, DurationFormatted FROM Videos WHERE FileSizeBytes IS NOT NULL ORDER BY FileSizeBytes DESC LIMIT 10;" },
                new() { Name = "Videos by Resolution", Query = "SELECT CASE WHEN Width IS NOT NULL AND Height IS NOT NULL THEN Width || 'x' || Height ELSE 'Unknown' END as Resolution, COUNT(*) as Count FROM Videos GROUP BY Resolution ORDER BY Count DESC;" }
            };
            */
        }

        [HttpPost]
        public async Task<IActionResult> LoadPredefinedQuery(string query)
        {
            var model = new DatabaseQueryViewModel
            {
                Query = query,
                PredefinedQueries = await GetPredefinedQueries()
            };

            return View("Index", model);
        }
    }
}