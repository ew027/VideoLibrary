
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;
using VideoLibrary.Services;
using System.IO;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;

namespace VideoLibrary.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HomeController> _logger;
        private readonly ThumbnailService _thumbnailService;
        private readonly ImageCacheService _imageCacheService;

        public HomeController(AppDbContext context, ILogger<HomeController> logger, ThumbnailService thumbnailService, ImageCacheService imageCacheService)
        {
            _context = context;
            _logger = logger;
            _thumbnailService = thumbnailService;
            _imageCacheService = imageCacheService;
        }

        public async Task<IActionResult> Index(int showArchived = 0)
        {
            if (showArchived == 0)
            {
                if (int.TryParse(HttpContext.Request.Cookies["showArchived"], out var cookieValue))
                {
                    showArchived = cookieValue;
                }
                else
                {
                    // no cookie so default to all
                    showArchived = 1;
                }
            }
            else
            {
                // specified value so set the cookie - should validate here but meh
                HttpContext.Response.Cookies.Append("showArchived", showArchived.ToString());
            }

            var tags = _context.Tags
                .Where(t => _context.VideoTags.Any(vt => vt.TagId == t.Id) ||
                            _context.PlaylistTags.Any(vt => vt.TagId == t.Id) ||
                            _context.GalleryTags.Any(gt => gt.TagId == t.Id) ||
                            _context.ContentTags.Any(gt => gt.TagId == t.Id))
                .Include(tag => tag.VideoTags)
                .Include(tag => tag.GalleryTags)
                .Include(tag => tag.PlaylistTags)
                .Include(tag => tag.ContentTags)
                .AsSplitQuery()
                .OrderBy(t => t.Name);
            //.ToListAsync();

            List<Tag> filteredTags = null;

            if (showArchived == 3)
            {
                filteredTags = await tags.Where(x => x.IsArchived == true).ToListAsync();
            }
            else if (showArchived == 2)
            {
                filteredTags = await tags.Where(x => x.IsArchived == false).ToListAsync();
            }
            else
            {
                filteredTags = await tags.ToListAsync();
            }

            // Get the SQL without executing
            //var sql = tags.ToQueryString();
            //_logger.LogInformation("Generated SQL: {SQL}", sql);

            ViewBag.IsSearch = false;
            ViewBag.ArchivedViewOptions = new SelectList(new[]
            {
                new { Value = "1", Text = "All" },
                new { Value = "2", Text = "Current" },
                new { Value = "3", Text = "Archived" }
            }, "Value", "Text", showArchived);

            return View(filteredTags);
        }

        public async Task<IActionResult> Search(string q)
        {
            ViewData["SearchQuery"] = q;

            if (string.IsNullOrWhiteSpace(q))
            {
                // Return to main page if no search query
                return RedirectToAction(nameof(Index));
            }

            var query = _context.Tags
                    .Where(t => _context.VideoTags.Any(vt => vt.TagId == t.Id) ||
                                _context.GalleryTags.Any(gt => gt.TagId == t.Id) ||
                                _context.PlaylistTags.Any(vt => vt.TagId == t.Id) ||
                            _context.ContentTags.Any(gt => gt.TagId == t.Id));

            // Add case-insensitive search filter if search phrase is provided
            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(t => EF.Functions.Like(t.Name.ToLower(), $"%{q.ToLower()}%"));
            }

            var tags = query
                .Include(tag => tag.VideoTags)
                .Include(tag => tag.GalleryTags)
                .Include(tag => tag.PlaylistTags)
                .Include(tag => tag.ContentTags)
                .AsSplitQuery()
                .OrderBy(t => t.Name);

            var showArchived = 1;

            if (int.TryParse(HttpContext.Request.Cookies["showArchived"], out var cookieValue))
            {
                showArchived = cookieValue;
            }

            List<Tag> filteredTags = null;

            if (showArchived == 3)
            {
                filteredTags = await tags.Where(x => x.IsArchived == true).ToListAsync();
            }
            else if (showArchived == 2)
            {
                filteredTags = await tags.Where(x => x.IsArchived == false).ToListAsync();
            }
            else
            {
                filteredTags = await tags.ToListAsync();
            }

            ViewBag.IsSearch = true;
            ViewBag.ArchivedViewOptions = new SelectList(new[]
            {
                new { Value = "1", Text = "All" },
                new { Value = "2", Text = "Current" },
                new { Value = "3", Text = "Archived" }
            }, "Value", "Text", showArchived);

            return View("Index", filteredTags);
        }

        public IActionResult SmallThumbnail(int id)
        {
            var tag = _context.Tags.Find(id);
            if (tag == null || string.IsNullOrEmpty(tag.ThumbnailPath))
            {
                return NotFound();
            }

            var smallThumbnailPath = _thumbnailService.GetSmallThumbnailPath(tag.ThumbnailPath);

            if (!System.IO.File.Exists(smallThumbnailPath))
            {
                return NotFound();
            }

            var cachedPath = _imageCacheService.GetCachedPath(smallThumbnailPath);

            var fileStream = new FileStream(cachedPath, FileMode.Open, FileAccess.Read);
            return File(fileStream, "image/jpeg");
        }

        public IActionResult Debug()
        {
            var data = new List<string>();

            var tempDir = "/tmp/videolibrarycache";
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            try
            {
                System.IO.File.WriteAllText(Path.Combine(tempDir, "test.tmp"), "test");
                data.Add("/tmp/videolibrarycache is writable");

            }
            catch (Exception ex)
            {
                data.Add($"Even /tmp failed: {ex.Message}");
            }

            return Json(data);
        }
    }
}

