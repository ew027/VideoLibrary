using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Eventing.Reader;
using VideoLibrary.Models;
using VideoLibrary.Models.ViewModels;
using VideoLibrary.Services;

namespace VideoLibrary.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<AdminController> _logger;
        private readonly GalleryService _galleryService;
        private readonly TagHierarchyMigrationService _tagHierarchyMigrationService;
        private readonly TagHierarchyService _tagHierarchyService;

        public AdminController(AppDbContext context,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<AdminController> logger,
            GalleryService galleryService,
            TagHierarchyMigrationService tagHierarchyMigrationService,
            TagHierarchyService tagHierarchyService)
        {
            _context = context;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _galleryService = galleryService;
            _tagHierarchyMigrationService = tagHierarchyMigrationService;
            _tagHierarchyService = tagHierarchyService;
        }

        public async Task<IActionResult> Index()
        {
            var stats = new AdminDashboardViewModel
            {
                TotalVideos = await _context.Videos.CountAsync(),
                VideosWithThumbnails = await _context.Videos.CountAsync(v => !string.IsNullOrEmpty(v.ThumbnailPath)),
                VideosWithAnalysis = await _context.Videos.CountAsync(v => v.DurationSeconds.HasValue),
                TotalTags = await _context.Tags.CountAsync(),
                TagsWithThumbnails = await _context.Tags.CountAsync(t => !string.IsNullOrEmpty(t.ThumbnailPath)),
                TotalGalleries = await _context.Galleries.CountAsync(),
                GalleriesWithTags = await _context.Galleries.CountAsync(g => g.GalleryTags.Any())
            };

            return View(stats);
        }

        [HttpPost]
        public IActionResult ScanVideos()
        {
            _ = Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var scanService = scope.ServiceProvider.GetRequiredService<VideoScanService>();
                await scanService.ScanVideosAsync();
            });

            TempData["SuccessMessage"] = "Video scan started in the background";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult GenerateThumbnails()
        {
            _ = Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var thumbnailService = scope.ServiceProvider.GetRequiredService<ThumbnailService>();
                await thumbnailService.GenerateThumbnailsAsync();
                _logger.LogInformation("Manual thumbnail generation completed");
            });

            TempData["SuccessMessage"] = "Thumbnail generation started in the background";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult AnalyzeVideos()
        {
            _ = Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var analysisService = scope.ServiceProvider.GetRequiredService<VideoAnalysisService>();
                await analysisService.AnalyzeVideosAsync();
            });

            TempData["SuccessMessage"] = "Video analysis started in the background";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult UpdateTagThumbnails()
        {
            _ = Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var thumbnailService = scope.ServiceProvider.GetRequiredService<ThumbnailService>();
                await thumbnailService.UpdateTagThumbnailsAsync();
            });

            TempData["SuccessMessage"] = "Tag thumbnail update started in the background";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult RunAllTasks()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();

                    _logger.LogInformation("Starting complete maintenance cycle");

                    // 1. Scan for new videos
                    var scanService = scope.ServiceProvider.GetRequiredService<VideoScanService>();
                    await scanService.ScanVideosAsync();
                    _logger.LogInformation("Video scan completed");

                    // 2. Generate thumbnails
                    var thumbnailService = scope.ServiceProvider.GetRequiredService<ThumbnailService>();
                    await thumbnailService.GenerateThumbnailsAsync();
                    _logger.LogInformation("Thumbnail generation completed");

                    // 3. Analyze videos
                    var analysisService = scope.ServiceProvider.GetRequiredService<VideoAnalysisService>();
                    await analysisService.AnalyzeVideosAsync();
                    _logger.LogInformation("Video analysis completed");

                    // 4. Update tag thumbnails
                    await thumbnailService.UpdateTagThumbnailsAsync();
                    _logger.LogInformation("Tag thumbnail update completed");

                    _logger.LogInformation("Complete maintenance cycle finished");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during complete maintenance cycle");
                }
            });

            TempData["SuccessMessage"] = "Complete maintenance cycle started in the background";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> RecentVideos()
        {
            var recentVideos = await _context.Videos
                .OrderByDescending(v => v.DateAdded)
                .Take(20)
                .ToListAsync();

            return View(recentVideos);
        }

        public async Task<IActionResult> Statistics()
        {
            var stats = new AdminStatisticsViewModel
            {
                VideosByCodec = await _context.Videos
                    .Where(v => !string.IsNullOrEmpty(v.VideoCodec))
                    .GroupBy(v => v.VideoCodec)
                    .Select(g => new CodecStat { Codec = g.Key!, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync(),

                VideosByResolution = await _context.Videos
                    .Where(v => v.Width.HasValue && v.Height.HasValue)
                    .GroupBy(v => new { v.Width, v.Height })
                    .Select(g => new ResolutionStat
                    {
                        Resolution = $"{g.Key.Width}x{g.Key.Height}",
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync(),

                TotalFileSize = await _context.Videos
                    .Where(v => v.FileSizeBytes.HasValue)
                    .SumAsync(v => v.FileSizeBytes.Value),

                TotalDuration = await _context.Videos
                    .Where(v => v.DurationSeconds.HasValue)
                    .SumAsync(v => v.DurationSeconds.Value),

                VideosNeedingThumbnails = await _context.Videos
                    .CountAsync(v => string.IsNullOrEmpty(v.ThumbnailPath)),

                VideosNeedingAnalysis = await _context.Videos
                    .CountAsync(v => !v.DurationSeconds.HasValue)
            };

            return View(stats);
        }

        [HttpPost]
        public IActionResult ScanGalleries()
        {
            _ = Task.Run(async () =>
            {
                await _galleryService.ScanForNewGalleriesAsync();
            });

            TempData["SuccessMessage"] = "Gallery scan started in the background";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult ClearGalleryCache()
        {
            _ = Task.Run(() =>
            {
                _galleryService.ClearCache();
            });

            TempData["SuccessMessage"] = "Gallery cache cleared";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> LogViewer(int logLevel)
        {
            List<LogEntry> logEntries = new List<LogEntry>();

            if (logLevel > 0)
            {
                logEntries = await _context.LogEntries
                    .OrderByDescending(p => p.Id)
                    .Take(50)
                    .ToListAsync();
            }
            else
            {
                logEntries = await _context.LogEntries
                    .Where(x => (int)x.LogLevel >= logLevel)
                    .OrderByDescending(p => p.Id)
                    .Take(50)
                    .ToListAsync();
            }

            return View(logEntries);
        }

        [HttpGet]
        public async Task<IActionResult> MigrateTags()
        {
            await _tagHierarchyMigrationService.InitializeFlatStructureAsync();
            TempData["SuccessMessage"] = "Tags have been migrated";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Tag Management page
        /// </summary>
        public async Task<IActionResult> TagManagement()
        {
            var viewModel = new TagManagementViewModel
            {
                TotalTags = await _context.Tags.CountAsync(),
                EmptyTags = await _context.Tags.CountAsync(t =>
                    !t.VideoTags.Any() &&
                    !t.GalleryTags.Any() &&
                    !t.PlaylistTags.Any() &&
                    !t.ContentTags.Any()),
                TagsWithContent = await _context.Tags.CountAsync(t =>
                    t.VideoTags.Any() ||
                    t.GalleryTags.Any() ||
                    t.PlaylistTags.Any() ||
                    t.ContentTags.Any())
            };

            return View(viewModel);
        }

        /// <summary>
        /// Get all tags in hierarchical structure with content counts
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTagsHierarchical()
        {
            try
            {
                var allTags = await _context.Tags
                    .Include(t => t.VideoTags)
                    .Include(t => t.GalleryTags)
                    .Include(t => t.PlaylistTags)
                    .Include(t => t.ContentTags)
                    .OrderBy(t => t.Left)
                    .Select(t => new TagHierarchyDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        ParentId = t.ParentId,
                        Level = t.Level,
                        Left = t.Left,
                        Right = t.Right,
                        ContentCount = t.VideoTags.Count + t.GalleryTags.Count +
                                      t.PlaylistTags.Count + t.ContentTags.Count,
                        IsEmpty = !t.VideoTags.Any() && !t.GalleryTags.Any() &&
                                 !t.PlaylistTags.Any() && !t.ContentTags.Any(),
                        IsArchived = t.IsArchived,
                        ThumbnailPath = t.ThumbnailPath
                    })
                    .ToListAsync();

                // Build hierarchy
                var tagDict = allTags.ToDictionary(t => t.Id);
                var rootTags = new List<TagHierarchyDto>();

                foreach (var tag in allTags)
                {
                    if (tag.ParentId.HasValue && tagDict.ContainsKey(tag.ParentId.Value))
                    {
                        tagDict[tag.ParentId.Value].Children.Add(tag);
                    }
                    else
                    {
                        rootTags.Add(tag);
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

        /// <summary>
        /// Get detailed information about a specific tag
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTagDetails(int id)
        {
            try
            {
                var tag = await _context.Tags
                    .Include(t => t.Parent)
                    .Include(t => t.VideoTags)
                    .Include(t => t.GalleryTags)
                    .Include(t => t.PlaylistTags)
                    .Include(t => t.ContentTags)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (tag == null)
                    return NotFound(new { error = "Tag not found" });

                var details = new TagDetailViewModel
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    ParentId = tag.ParentId,
                    ParentName = tag.Parent?.Name,
                    Level = tag.Level,
                    VideoCount = tag.VideoTags.Count,
                    GalleryCount = tag.GalleryTags.Count,
                    PlaylistCount = tag.PlaylistTags.Count,
                    ContentCount = tag.ContentTags.Count,
                    TotalContentCount = tag.VideoTags.Count + tag.GalleryTags.Count +
                                       tag.PlaylistTags.Count + tag.ContentTags.Count,
                    ChildCount = await _context.Tags.CountAsync(t => t.ParentId == id),
                    DescendantCount = tag.GetDescendantCount(),
                    IsEmpty = !tag.VideoTags.Any() && !tag.GalleryTags.Any() &&
                             !tag.PlaylistTags.Any() && !tag.ContentTags.Any(),
                    ThumbnailPath = tag.ThumbnailPath,
                    IsArchived = tag.IsArchived
                };

                return Json(details);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tag details for tag {TagId}", id);
                return StatusCode(500, new { error = "Failed to load tag details" });
            }
        }

        /// <summary>
        /// Get all tags that have no content associated with them
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetEmptyTags()
        {
            try
            {
                var emptyTags = await _context.Tags
                    .Include(t => t.Parent)
                    .Where(t => !t.VideoTags.Any() &&
                               !t.GalleryTags.Any() &&
                               !t.PlaylistTags.Any() &&
                               !t.ContentTags.Any())
                    .OrderBy(t => t.Name)
                    .Select(t => new
                    {
                        t.Id,
                        t.Name,
                        t.ParentId,
                        ParentName = t.Parent != null ? t.Parent.Name : null,
                        t.Level,
                        ChildCount = t.Children.Count
                    })
                    .ToListAsync();

                return Json(emptyTags);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving empty tags");
                return StatusCode(500, new { error = "Failed to load empty tags" });
            }
        }

        /// <summary>
        /// Add a new tag
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddTag([FromBody] AddTagRequest request,
            [FromServices] TagHierarchyService tagHierarchyService)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { message = "Tag name is required" });

                // Check for duplicate name at same level
                var existingTag = await _context.Tags
                    .FirstOrDefaultAsync(t => t.Name == request.Name && t.ParentId == request.ParentId);

                if (existingTag != null)
                    return BadRequest(new { message = "A tag with this name already exists at this level" });

                var newTag = await tagHierarchyService.InsertTagAsync(request.Name, request.ParentId);

                _logger.LogInformation("Added new tag '{Name}' with ID {Id}", newTag.Name, newTag.Id);

                return Json(new
                {
                    success = true,
                    id = newTag.Id,
                    message = "Tag added successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding tag '{Name}'", request.Name);
                return StatusCode(500, new { message = "Failed to add tag" });
            }
        }

        /// <summary>
        /// Rename a tag
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RenameTag([FromBody] RenameTagRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.NewName))
                    return BadRequest(new { message = "Tag name is required" });

                var tag = await _context.Tags.FindAsync(request.TagId);
                if (tag == null)
                    return NotFound(new { message = "Tag not found" });

                // Check for duplicate name at same level
                var existingTag = await _context.Tags
                    .FirstOrDefaultAsync(t => t.Name == request.NewName &&
                                             t.ParentId == tag.ParentId &&
                                             t.Id != tag.Id);

                if (existingTag != null)
                    return BadRequest(new { message = "A tag with this name already exists at this level" });

                var oldName = tag.Name;
                tag.Name = request.NewName;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Renamed tag {TagId} from '{OldName}' to '{NewName}'",
                    tag.Id, oldName, request.NewName);

                return Json(new { success = true, message = "Tag renamed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming tag {TagId}", request.TagId);
                return StatusCode(500, new { message = "Failed to rename tag" });
            }
        }

        /// <summary>
        /// Move a tag to a new parent
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MoveTag([FromBody] MoveTagRequest request,
            [FromServices] TagHierarchyService tagHierarchyService)
        {
            try
            {
                var tag = await _context.Tags.FindAsync(request.TagId);
                if (tag == null)
                    return NotFound(new { message = "Tag not found" });

                if (request.NewParentId.HasValue)
                {
                    var newParent = await _context.Tags.FindAsync(request.NewParentId.Value);
                    if (newParent == null)
                        return NotFound(new { message = "New parent tag not found" });

                    // Check for circular reference
                    if (await tagHierarchyService.IsDescendantOfAsync(request.NewParentId.Value, request.TagId))
                        return BadRequest(new { message = "Cannot move tag to its own descendant" });
                }

                await tagHierarchyService.MoveTagAsync(request.TagId, request.NewParentId);

                _logger.LogInformation("Moved tag {TagId} to parent {ParentId}",
                    request.TagId, request.NewParentId);

                return Json(new { success = true, message = "Tag moved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving tag {TagId} to parent {ParentId}",
                    request.TagId, request.NewParentId);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delete a tag and optionally its descendants
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteTag([FromBody] DeleteTagRequest request,
            [FromServices] TagHierarchyService tagHierarchyService)
        {
            try
            {
                var tag = await _context.Tags
                    .Include(t => t.VideoTags)
                    .Include(t => t.GalleryTags)
                    .Include(t => t.PlaylistTags)
                    .Include(t => t.ContentTags)
                    .FirstOrDefaultAsync(t => t.Id == request.TagId);

                if (tag == null)
                    return NotFound(new { message = "Tag not found" });

                // Check if tag has content
                var hasContent = tag.VideoTags.Any() || tag.GalleryTags.Any() ||
                                tag.PlaylistTags.Any() || tag.ContentTags.Any();

                if (hasContent)
                {
                    _logger.LogWarning("Deleting tag {TagId} '{Name}' which has {Count} content items",
                        tag.Id, tag.Name,
                        tag.VideoTags.Count + tag.GalleryTags.Count + tag.PlaylistTags.Count + tag.ContentTags.Count);
                }

                await tagHierarchyService.DeleteTagAsync(request.TagId, request.DeleteDescendants);

                _logger.LogInformation("Deleted tag {TagId} '{Name}' (deleteDescendants: {DeleteDescendants})",
                    request.TagId, tag.Name, request.DeleteDescendants);

                return Json(new { success = true, message = "Tag deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tag {TagId}", request.TagId);
                return StatusCode(500, new { message = "Failed to delete tag" });
            }
        }

        /// <summary>
        /// Rebuild the entire tag tree structure
        /// Useful after manual database modifications
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RebuildTagTree([FromServices] TagHierarchyService tagHierarchyService)
        {
            try
            {
                await tagHierarchyService.RebuildTreeAsync();

                _logger.LogInformation("Tag tree structure rebuilt successfully");

                TempData["SuccessMessage"] = "Tag tree structure rebuilt successfully";
                return RedirectToAction(nameof(TagManagement));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rebuilding tag tree");
                TempData["ErrorMessage"] = "Failed to rebuild tag tree structure";
                return RedirectToAction(nameof(TagManagement));
            }
        }

        /// <summary>
        /// Validate tag tree integrity
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ValidateTagTree([FromServices] TagHierarchyService tagHierarchyService)
        {
            try
            {
                var errors = await tagHierarchyService.ValidateTreeAsync();

                if (errors.Count == 0)
                {
                    return Json(new
                    {
                        valid = true,
                        message = "Tag tree structure is valid",
                        errors = new List<string>()
                    });
                }

                return Json(new
                {
                    valid = false,
                    message = $"Found {errors.Count} validation error(s)",
                    errors = errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating tag tree");
                return StatusCode(500, new { error = "Failed to validate tag tree" });
            }
        }

    }
}