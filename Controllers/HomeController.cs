
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.IO;
using VideoLibrary.Models;
using VideoLibrary.Models.ViewModels;
using VideoLibrary.Services;
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

        private readonly IMemoryCache _cache;
        private const string UNWATCHED_CACHE_KEY = "HomePage_UnwatchedVideos";
        private const string RANDOM_CACHE_KEY = "HomePage_RandomVideos";

        public HomeController(AppDbContext context, ILogger<HomeController> logger, ThumbnailService thumbnailService, ImageCacheService imageCacheService, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _thumbnailService = thumbnailService;
            _imageCacheService = imageCacheService;
            _cache = cache;
        }

        public async Task<IActionResult> Index(int seemore = 0)
        {
            var viewModel = new HomeViewModel();

            int recentCount = (seemore == 1) ? 30 : 10;

            // Get most recently added videos
            viewModel.RecentVideos = await _context.Videos
                .Include(v => v.VideoTags)
                .ThenInclude(vt => vt.Tag)
                .OrderByDescending(v => v.DateAdded)
                .Take(recentCount)
                .Select(v => new VideoCardViewModel
                {
                    Id = v.Id,
                    Title = v.Title,
                    ThumbnailPath = v.ThumbnailPath,
                    Tags = v.VideoTags
                        .Where(vt => !vt.Tag.IsArchived)
                        .Select(vt => new TagViewModel
                        {
                            Tag = vt.Tag
                        })
                        .ToList()
                })
                .ToListAsync();

            // Get 10 random unwatched videos (ViewCount = 0) - CACHED
            viewModel.UnwatchedVideos = (await _cache.GetOrCreateAsync(
                UNWATCHED_CACHE_KEY,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);

                    var unwatchedVideos = await _context.Videos
                        .Include(v => v.VideoTags)
                        .ThenInclude(vt => vt.Tag)
                        .Where(v => v.ViewCount == 0)
                        .ToListAsync();

                    return unwatchedVideos
                        .OrderBy(x => Guid.NewGuid())
                        .Take(10)
                        .Select(v => new VideoCardViewModel
                        {
                            Id = v.Id,
                            Title = v.Title,
                            ThumbnailPath = v.ThumbnailPath,
                            Tags = v.VideoTags
                                .Where(vt => !vt.Tag.IsArchived)
                                .Select(vt => new TagViewModel
                                {
                                    Tag = vt.Tag
                                })
                                .ToList()
                        })
                        .ToList();
                }))!;

            // Get 10 random videos (no criteria) - CACHED
            viewModel.RandomVideos = (await _cache.GetOrCreateAsync(
                RANDOM_CACHE_KEY,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);

                    var allVideos = await _context.Videos
                        .Include(v => v.VideoTags)
                        .ThenInclude(vt => vt.Tag)
                        .ToListAsync();

                    return allVideos
                        .OrderBy(x => Guid.NewGuid())
                        .Take(10)
                        .Select(v => new VideoCardViewModel
                        {
                            Id = v.Id,
                            Title = v.Title,
                            ThumbnailPath = v.ThumbnailPath,
                            Tags = v.VideoTags
                                .Where(vt => !vt.Tag.IsArchived)
                                .Select(vt => new TagViewModel
                                {
                                    Tag = vt.Tag
                                })
                                .ToList()
                        })
                        .ToList();
                }))!;

            // Get 20 most viewed videos
            viewModel.MostViewedVideos = await _context.Videos
                .OrderByDescending(v => v.ViewCount)
                .Take(20)
                .Select(v => new PopularVideoViewModel
                {
                    Id = v.Id,
                    Title = v.Title,
                    ViewCount = v.ViewCount
                })
                .ToListAsync();

            return View(viewModel);
        }

        public IActionResult ClearCache(string cacheId)
        {
            if (cacheId == "unwatched")
            {
                _cache.Remove(UNWATCHED_CACHE_KEY);
            }
            else if (cacheId == "random")
            {
                _cache.Remove(RANDOM_CACHE_KEY);
            }

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Tags(int showArchived = 0)
        {
            // 1 = all, 2 = current, 3 = archived
            if (showArchived == 0)
            {
                if (int.TryParse(HttpContext.Request.Cookies["showArchived"], out var cookieValue))
                {
                    showArchived = cookieValue;
                }
                else
                {
                    // no cookie so default to current and set cookie
                    showArchived = 2;
                    HttpContext.Response.Cookies.Append("showArchived", "2");
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
            q = q.Trim();

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

            return View("Tags", filteredTags);
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

        /// <summary>
        /// Alternative: Get tags using the TagHierarchyService
        /// This version uses the service for better separation of concerns
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllTagsHierarchicalWithService(
            [FromServices] TagHierarchyService tagHierarchyService)
        {
            try
            {
                var allTags = await tagHierarchyService.GetFullTreeAsync();

                // Convert to DTO with counts
                var tagDtos = allTags.Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.ParentId,
                    t.Level,
                    t.Left,
                    t.Right,
                    t.ThumbnailPath,
                    t.IsArchived,
                    Count = t.VideoTags.Count + t.GalleryTags.Count + t.PlaylistTags.Count,
                    HasChildren = t.Right - t.Left > 1
                }).ToList();

                // Build hierarchy
                var tagDict = tagDtos.ToDictionary(t => t.Id, t => new
                {
                    t.Id,
                    t.Name,
                    t.ParentId,
                    t.Level,
                    t.ThumbnailPath,
                    t.IsArchived,
                    t.Count,
                    t.HasChildren,
                    Children = new List<object>()
                });

                var rootTags = new List<object>();

                foreach (var tag in tagDtos)
                {
                    var tagNode = tagDict[tag.Id];

                    if (tag.ParentId.HasValue && tagDict.ContainsKey(tag.ParentId.Value))
                    {
                        ((List<object>)tagDict[tag.ParentId.Value].Children).Add(tagNode);
                    }
                    else
                    {
                        rootTags.Add(tagNode);
                    }
                }

                return Json(rootTags);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving hierarchical tags");
                return StatusCode(500, new { error = "Failed to load tags" });
            }
        }

    }
}

