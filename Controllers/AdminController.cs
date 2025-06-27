using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;
using VideoLibrary.Services;

namespace VideoLibrary.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AdminController> _logger;

        public AdminController(AppDbContext context, IServiceProvider serviceProvider, ILogger<AdminController> logger)
        {
            _context = context;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var stats = new AdminDashboardViewModel
            {
                TotalVideos = await _context.Videos.CountAsync(),
                VideosWithThumbnails = await _context.Videos.CountAsync(v => !string.IsNullOrEmpty(v.ThumbnailPath)),
                VideosWithAnalysis = await _context.Videos.CountAsync(v => v.DurationSeconds.HasValue),
                TotalTags = await _context.Tags.CountAsync(),
                TagsWithThumbnails = await _context.Tags.CountAsync(t => !string.IsNullOrEmpty(t.ThumbnailPath))
            };

            return View(stats);
        }

        [HttpPost]
        public IActionResult ScanVideos()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var scanService = scope.ServiceProvider.GetRequiredService<VideoScanService>();
                    await scanService.ScanVideosAsync();
                    _logger.LogInformation("Manual video scan completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during manual video scan");
                }
            });

            TempData["SuccessMessage"] = "Video scan started in the background";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult GenerateThumbnails()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var thumbnailService = scope.ServiceProvider.GetRequiredService<ThumbnailService>();
                    await thumbnailService.GenerateThumbnailsAsync();
                    _logger.LogInformation("Manual thumbnail generation completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during manual thumbnail generation");
                }
            });

            TempData["SuccessMessage"] = "Thumbnail generation started in the background";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult AnalyzeVideos()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var analysisService = scope.ServiceProvider.GetRequiredService<VideoAnalysisService>();
                    await analysisService.AnalyzeVideosAsync();
                    _logger.LogInformation("Manual video analysis completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during manual video analysis");
                }
            });

            TempData["SuccessMessage"] = "Video analysis started in the background";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult UpdateTagThumbnails()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var thumbnailService = scope.ServiceProvider.GetRequiredService<ThumbnailService>();
                    await thumbnailService.UpdateTagThumbnailsAsync();
                    _logger.LogInformation("Manual tag thumbnail update completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during manual tag thumbnail update");
                }
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
                    using var scope = _serviceProvider.CreateScope();

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
    }
}