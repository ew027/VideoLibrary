using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Threading.Tasks;
using VideoLibrary.Models;
using VideoLibrary.Models.ViewModels;
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
        private readonly ImageCacheService _imageCacheService;

        private static int? ClipTagId;

        public VideoController(AppDbContext context,
            ThumbnailService thumbnailService,
            VideoAnalysisService videoAnalysisService,
            ILogger<VideoController> logger,
            IServiceScopeFactory serviceScopeFactory,
            ImageCacheService imageCacheService)
        {
            _context = context;
            _thumbnailService = thumbnailService;
            _videoAnalysisService = videoAnalysisService;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _imageCacheService = imageCacheService;
        }

        public async Task<IActionResult> Details(int id, int? tagId = null)
        {
            var video = await _context.Videos
                .Include(v => v.VideoTags)
                .ThenInclude(vt => vt.Tag)
                .Include(v => v.Bookmarks)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (video == null)
            {
                return NotFound();
            }

            video.ViewCount++;

            await _context.SaveChangesAsync();

            var transcription = await _context.Transcriptions
                .FirstOrDefaultAsync(t => t.VideoId == id);

            VideoViewModel viewModel = null;

            if (tagId.HasValue && video.VideoTags.Any(x => x.TagId == tagId.Value))
            {
                viewModel = new VideoViewModel { Video = video, Transcription = transcription, Tag = video.VideoTags.FirstOrDefault(x => x.TagId == tagId.Value)!.Tag };
            }
            else
            {
                viewModel = new VideoViewModel { Video = video, Transcription = transcription };
            }

            viewModel.Playlists = await _context.Playlists.ToListAsync();

            return View(viewModel);
        }

        public async Task<IActionResult> RequestTranscript(int id, bool isXhr = false)
        {
            // check there is not already a transcription
            var transcription = await _context.Transcriptions
                .FirstOrDefaultAsync(t => t.VideoId == id);

            if (transcription != null)
            {
                if (isXhr)
                {
                    return Ok();
                }
                else
                {
                    TempData["ErrorMessage"] = "Transcription already exists for this video.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            transcription = new Transcription
            {
                VideoId = id,
                Status = TranscriptionStatus.Pending,
                DateRequested = DateTime.Now
            };

            _context.Transcriptions.Add(transcription);
            await _context.SaveChangesAsync();

            if (isXhr)
            {
                return Ok();
            }
            else
            {
                TempData["SuccessMessage"] = "Transcription request submitted successfully.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [Route("Video/ByTag")]
        [Route("Video/ByTag/{tagId}")]
        public async Task<IActionResult> ByTag(int tagId)
        {
            if (ClipTagId is null)
            {
                var clipTag = await _context.Tags.FirstOrDefaultAsync(x => x.Name == "clips");

                if (clipTag != null)
                {
                    ClipTagId = clipTag.Id;
                }
            }

            var tagViewModel = new TagWithContentViewModel();

            var tag = await _context.Tags
                //.Include(t => t.VideoTags)
                //.ThenInclude(vt => vt.Video)
                .FirstOrDefaultAsync(t => t.Id == tagId);

            if (tag == null)
            {
                return NotFound();
            }

            // Get videos for this tag
            var videos = await _context.Videos
                .Include(v => v.Transcription)
                .Include(v => v.VideoTags)
                .ThenInclude(vt => vt.Tag)
                .Where(v => v.VideoTags.Any(vt => vt.TagId == tagId))
                .OrderBy(v => v.Title)
                .ToListAsync();

            var videosWithoutClips = videos.Where(v => !v.VideoTags.Any(vt => vt.TagId == ClipTagId)).ToList();
            var videosWithClips = videos.Where(v => v.VideoTags.Any(vt => vt.TagId == ClipTagId)).ToList();

            // Get galleries for this tag
            var galleries = await _context.Galleries
                .Include(g => g.GalleryTags)
                .ThenInclude(gt => gt.Tag)
                .Where(g => g.GalleryTags.Any(gt => gt.TagId == tagId))
                .OrderBy(g => g.Name)
                .ToListAsync();

            // Get playlists for this tag
            var playlists = await _context.Playlists
                .Include(g => g.PlaylistTags)
                .ThenInclude(gt => gt.Tag)
                .Where(g => g.PlaylistTags.Any(gt => gt.TagId == tagId))
                .OrderBy(g => g.Name)
                .ToListAsync();

            // Get playlists for this tag
            var contents = await _context.Contents
                .Include(g => g.ContentTags)
                .ThenInclude(gt => gt.Tag)
                .Where(g => g.ContentTags.Any(gt => gt.TagId == tagId))
                .OrderBy(g => g.Title)
                .ToListAsync();

            tagViewModel.Galleries = galleries;
            tagViewModel.Videos = videosWithoutClips;
            tagViewModel.ClipVideos = videosWithClips;
            tagViewModel.Playlists = playlists;
            tagViewModel.Contents = contents;
            tagViewModel.AllowArchiving = true;
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

            // Get playlists for this tag
            var playlists = await _context.Playlists
                .Include(g => g.PlaylistTags)
                .ThenInclude(gt => gt.Tag)
                .Where(g => g.PlaylistTags.Any(gt => tagIdList.Contains(gt.TagId)))
                .OrderBy(g => g.Name)
                .ToListAsync();

            // Get playlists for this tag
            var contents = await _context.Contents
                .Include(g => g.ContentTags)
                .ThenInclude(gt => gt.Tag)
                .Where(g => g.ContentTags.Any(gt => tagIdList.Contains(gt.TagId)))
                .OrderBy(g => g.Title)
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
                Galleries = galleries,
                Playlists = playlists,
                Contents = contents,
                AllowArchiving = false
            };

            return View("ByTag", viewModel);
        }

        public async Task<IActionResult> GetAllTags()
        {
            var tags = await _context.Tags.Select(x => new { id = x.Id, name = x.Name }).ToListAsync();

            return Json(tags);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTitle(int id, string title)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(title))
                {
                    return Json(new { success = false, message = "Title cannot be empty" });
                }

                var video = await _context.Videos.FindAsync(id);
                if (video == null)
                {
                    return Json(new { success = false, message = "Video not found" });
                }

                video.Title = title.Trim();
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Title updated successfully" });
            }
            catch (Exception ex)
            {
                // Log the exception
                _logger.LogError(ex, "Error updating video title for ID {VideoId}", id);
                return Json(new { success = false, message = "An error occurred while updating the title" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SetArchivedStatus(int id, bool isArchived, bool isXhr = false)
        {
            var tag = await _context.Tags.FindAsync(id);

            if (tag is not null && tag.IsArchived != isArchived)
            {
                tag.IsArchived = isArchived;

                await _context.SaveChangesAsync();
            }

            if (isXhr)
            {
                return Ok();
            }
            else
            {
                return RedirectToAction("ByTag", new { tagId = id });
            }
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
        public async Task<IActionResult> UpdateTags(int videoId, string selectedTags, string newTags)
        {
            List<int> tagList = new List<int>();
            List<string> newTagList = new List<string>();

            if (!string.IsNullOrEmpty(selectedTags))
            {
                try
                {
                    tagList = selectedTags.Split(',').Select(t => Convert.ToInt32(t)).ToList();
                }
                catch (FormatException)
                {
                    return BadRequest("Invalid tag format in selected tags");
                }
            }

            if (!string.IsNullOrEmpty(newTags))
            {
                // Split new tags by comma, trim whitespace, and filter out empty strings
                newTagList = newTags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            }

            var video = await _context.Videos
                .Include(v => v.VideoTags)
                .FirstOrDefaultAsync(v => v.Id == videoId);

            if (video == null)
            {
                return NotFound();
            }

            // deal with any new tags
            foreach (var newTag in newTagList)
            {
                var existingTag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == newTag);
                if (existingTag == null)
                {
                    existingTag = new Tag { Name = newTag };
                    _context.Tags.Add(existingTag);
                    await _context.SaveChangesAsync();
                }

                tagList.Add(existingTag.Id);
            }

            // Remove existing tags
            _context.VideoTags.RemoveRange(video.VideoTags);

            // Add new tags
            foreach (var tagId in tagList)
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

        public async Task<IActionResult> Stream(int id)
        {
            var video = await _context.Videos.FindAsync(id);
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

            var cachedPath = _imageCacheService.GetCachedPath(video.ThumbnailPath);

            var fileStream = new FileStream(cachedPath, FileMode.Open, FileAccess.Read);
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

            var cachedPath = _imageCacheService.GetCachedPath(smallThumbnailPath);

            var fileStream = new FileStream(cachedPath, FileMode.Open, FileAccess.Read);
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

        /// <summary>
        /// Get all available thumbnails for a tag (from videos and galleries)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTagThumbnails(int tagId)
        {
            var thumbnails = new List<object>();

            // Get video thumbnails
            var videos = await _context.Videos
                .Include(v => v.VideoTags)
                .Where(v => v.VideoTags.Any(vt => vt.TagId == tagId) && !string.IsNullOrEmpty(v.ThumbnailPath))
                .Select(v => new
                {
                    Type = "video",
                    Id = v.Id,
                    Title = v.Title,
                    ThumbnailPath = v.ThumbnailPath
                })
                .ToListAsync();

            thumbnails.AddRange(videos);

            // Get gallery thumbnails
            var galleries = await _context.Galleries
                .Include(g => g.GalleryTags)
                .Where(g => g.GalleryTags.Any(gt => gt.TagId == tagId) && !string.IsNullOrEmpty(g.ThumbnailPath))
                .Select(g => new
                {
                    Type = "gallery",
                    Id = g.Id,
                    Title = g.Name,
                    ThumbnailPath = g.ThumbnailPath,
                    g.FolderPath
                })
                .ToListAsync();

            thumbnails.AddRange(galleries);

            return Json(new { success = true, thumbnails });
        }

        /// <summary>
        /// Set a specific thumbnail for a tag
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SetTagThumbnail(int tagId, string thumbnailPath)
        {
            try
            {
                var tag = await _context.Tags.FindAsync(tagId);
                if (tag == null)
                {
                    return Json(new { success = false, message = "Tag not found" });
                }

                tag.ThumbnailPath = thumbnailPath;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated thumbnail for tag {TagId} ({TagName}) to {ThumbnailPath}",
                    tagId, tag.Name, thumbnailPath);

                return Json(new { success = true, message = "Thumbnail updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting tag thumbnail for tag {TagId}", tagId);
                return Json(new { success = false, message = "Failed to update thumbnail" });
            }
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
