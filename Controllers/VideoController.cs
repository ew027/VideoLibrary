using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using VideoLibrary.Models;
using VideoLibrary.Services;

namespace VideoLibrary.Controllers
{
    [Authorize]
    public class VideoController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ThumbnailService _thumbnailService;
        private readonly VideoAnalysisService _videoAnalysisService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<VideoController> _logger;

        public VideoController(AppDbContext context, 
            ThumbnailService thumbnailService, 
            VideoAnalysisService videoAnalysisService, 
            ILogger<VideoController> logger, 
            IServiceScopeFactory serviceScopeFactory)
        {
            _context = context;
            _thumbnailService = thumbnailService;
            _videoAnalysisService = videoAnalysisService;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task<IActionResult> Details(int id, int? tagId = null)
        {
            var video = await _context.Videos
                .Include(v => v.VideoTags)
                .ThenInclude(vt => vt.Tag)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (video == null)
            {
                return NotFound();
            }

            VideoViewModel viewModel = null;

            if (tagId.HasValue && video.VideoTags.Any(x => x.TagId == tagId.Value))
            {
                viewModel = new VideoViewModel { Video = video, Tag = video.VideoTags.FirstOrDefault(x => x.TagId == tagId.Value)!.Tag };
            }
            else
            {
                viewModel = new VideoViewModel { Video = video };
            }

            return View(viewModel);
        }

        public async Task<IActionResult> ByTag(int tagId)
        {
            var tagViewModel = new TagWithContentViewModel();

            var tag = await _context.Tags
                .Include(t => t.VideoTags)
                .ThenInclude(vt => vt.Video)
                .FirstOrDefaultAsync(t => t.Id == tagId);

            if (tag == null)
            {
                return NotFound();
            }

            // Get videos for this tag
            var videos = await _context.Videos
                .Where(v => v.VideoTags.Any(vt => vt.TagId == tagId))
                .OrderBy(v => v.Title)
                .ToListAsync();

            // Get galleries for this tag
            var galleries = await _context.Galleries
                .Include(g => g.GalleryTags)
                .ThenInclude(gt => gt.Tag)
                .Where(g => g.GalleryTags.Any(gt => gt.TagId == tagId))
                .OrderBy(g => g.Name)
                .ToListAsync();

            tagViewModel.Galleries = galleries;
            tagViewModel.Videos = videos;
            tagViewModel.Tag = tag;

            return View(tagViewModel);
        }

        public async Task<IActionResult> ByMultipleTags(string tagIds)
        {
            var tagIdList = ParseTagIds(tagIds);

            var tags = await _context.Tags
                 .Where(t => tagIdList.Contains(t.Id))
                 .ToListAsync();

            // Get videos that have ANY of the selected tags
            var videos = await _context.Videos
                .Where(v => v.VideoTags.Any(vt => tagIdList.Contains(vt.TagId)))
                .Include(v => v.VideoTags)
                .ThenInclude(vt => vt.Tag)
                .OrderBy(v => v.Title)
                .ToListAsync();

            // Get galleries that have ANY of the selected tags
            var galleries = await _context.Galleries
                .Include(g => g.GalleryTags)
                .ThenInclude(gt => gt.Tag)
                .Where(g => g.GalleryTags.Any(gt => tagIdList.Contains(gt.TagId)))
                .OrderBy(g => g.Name)
                .ToListAsync();

            // Create a virtual tag for display
            var tagNames = tags.Select(t => t.Name).ToList();
            var displayName = tagNames.Count > 3
                ? $"{string.Join(", ", tagNames.Take(3))} + {tagNames.Count - 3} more"
                : string.Join(", ", tagNames);

            var virtualTag = new Tag
            {
                Id = 0,
                Name = displayName
            };

            var viewModel = new TagWithContentViewModel
            {
                Tag = virtualTag,
                Videos = videos,
                Galleries = galleries
            };

            return View("ByTag", viewModel);
        }

        private List<int> ParseTagIds(string tagIds)
        {
            if (string.IsNullOrWhiteSpace(tagIds))
                return new List<int>();

            return tagIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id.Trim(), out var parsed) ? parsed : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .Distinct()
                .ToList();
        }

        public async Task<IActionResult> Edit(int id)
        {
            var video = await _context.Videos
                .Include(v => v.VideoTags)
                .ThenInclude(vt => vt.Tag)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (video == null)
            {
                return NotFound();
            }

            ViewBag.AllTags = await _context.Tags.OrderBy(t => t.Name).ToListAsync();
            return View(video);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTags(int videoId, int[] selectedTags)
        {
            var video = await _context.Videos
                .Include(v => v.VideoTags)
                .FirstOrDefaultAsync(v => v.Id == videoId);

            if (video == null)
            {
                return NotFound();
            }

            // Remove existing tags
            _context.VideoTags.RemoveRange(video.VideoTags);

            // Add new tags
            foreach (var tagId in selectedTags)
            {
                video.VideoTags.Add(new VideoTag { VideoId = videoId, TagId = tagId });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = videoId });
        }

        [HttpPost]
        public async Task<IActionResult> AddTag(int videoId, string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return BadRequest("Tag name is required");
            }

            var existingTag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
            if (existingTag == null)
            {
                existingTag = new Tag { Name = tagName };
                _context.Tags.Add(existingTag);
                await _context.SaveChangesAsync();
            }

            var existingVideoTag = await _context.VideoTags
                .FirstOrDefaultAsync(vt => vt.VideoId == videoId && vt.TagId == existingTag.Id);

            if (existingVideoTag == null)
            {
                _context.VideoTags.Add(new VideoTag { VideoId = videoId, TagId = existingTag.Id });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = videoId });
        }

        [HttpPost]
        public async Task<IActionResult> RefreshThumbnail(int videoId, int seconds = 30)
        {
            await _thumbnailService.GenerateVideoThumbnailAtTime(videoId, seconds);
            return RedirectToAction(nameof(Details), new { id = videoId });
        }

        public IActionResult Stream(int id)
        {
            var video = _context.Videos.Find(id);
            if (video == null || !System.IO.File.Exists(video.FilePath))
            {
                return NotFound();
            }

            var fileStream = new FileStream(video.FilePath, FileMode.Open, FileAccess.Read);
            var contentType = GetContentType(video.FilePath);

            return File(fileStream, contentType, enableRangeProcessing: true);
        }

        public IActionResult Thumbnail(int id)
        {
            var video = _context.Videos.Find(id);
            if (video == null || string.IsNullOrEmpty(video.ThumbnailPath) || !System.IO.File.Exists(video.ThumbnailPath))
            {
                return NotFound();
            }

            var fileStream = new FileStream(video.ThumbnailPath, FileMode.Open, FileAccess.Read);
            return File(fileStream, "image/jpeg");
        }

        public IActionResult SmallThumbnail(int id)
        {
            var video = _context.Videos.Find(id);
            if (video == null || string.IsNullOrEmpty(video.ThumbnailPath))
            {
                return NotFound();
            }

            var smallThumbnailPath = _thumbnailService.GetSmallThumbnailPath(video.ThumbnailPath);

            if (!System.IO.File.Exists(smallThumbnailPath))
            {
                return NotFound();
            }

            var fileStream = new FileStream(smallThumbnailPath, FileMode.Open, FileAccess.Read);
            return File(fileStream, "image/jpeg");
        }

        [HttpPost]
        public async Task<IActionResult> ReAnalyzeVideo(int videoId)
        {
            var success = await _videoAnalysisService.ReAnalyzeVideo(videoId);
            if (success)
            {
                TempData["SuccessMessage"] = "Video analysis updated successfully";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to analyze video";
            }

            return RedirectToAction(nameof(Details), new { id = videoId });
        }

        [HttpPost]
        public async Task<IActionResult> SaveNotes(int videoId, string notes)
        {
            var video = await _context.Videos.FindAsync(videoId);
            if (video == null)
            {
                return NotFound();
            }

            video.Notes = notes;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Notes saved successfully";
            return RedirectToAction(nameof(Details), new { id = videoId });
        }

        [HttpPost]
        public IActionResult CreateClip(int videoId, double startSeconds, double endSeconds)
        {
            if (startSeconds >= endSeconds)
            {
                TempData["ErrorMessage"] = "End time must be after start time";
                return RedirectToAction(nameof(Details), new { id = videoId });
            }

            if (startSeconds < 0)
            {
                TempData["ErrorMessage"] = "Start time cannot be negative";
                return RedirectToAction(nameof(Details), new { id = videoId });
            }

            // Start clip creation in background
            _ = Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var clippingService = scope.ServiceProvider.GetRequiredService<VideoClippingService>();
                var success = await clippingService.CreateClipAsync(videoId, startSeconds, endSeconds);
            });

            TempData["SuccessMessage"] = "Clip creation started in the background. The new clip will appear in your library shortly.";
            return RedirectToAction(nameof(Details), new { id = videoId });
        }

        private string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".mp4" => "video/mp4",
                ".avi" => "video/x-msvideo",
                ".mkv" => "video/x-matroska",
                ".mov" => "video/quicktime",
                ".wmv" => "video/x-ms-wmv",
                ".flv" => "video/x-flv",
                ".webm" => "video/webm",
                _ => "application/octet-stream"
            };
        }
    }
}
