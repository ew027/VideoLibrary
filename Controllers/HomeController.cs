
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;
using VideoLibrary.Services;
using System.IO;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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

        public async Task<IActionResult> Index()
        {
            var tags = await _context.Tags
                .Where(t => _context.VideoTags.Any(vt => vt.TagId == t.Id) ||
                            _context.GalleryTags.Any(gt => gt.TagId == t.Id))
                .Include(tag => tag.VideoTags)
                .Include(tag => tag.GalleryTags)
                .AsSplitQuery()
                .OrderBy(t => t.Name)
                .ToListAsync();

            // Get the SQL without executing
            //var sql = tags.ToQueryString();
            //_logger.LogInformation("Generated SQL: {SQL}", sql);

            return View(tags);
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

