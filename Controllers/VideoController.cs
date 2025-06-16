using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;
using VideoLibrary.Services;

namespace VideoLibrary.Controllers
{
    public class VideoController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ThumbnailService _thumbnailService;

        public VideoController(AppDbContext context, ThumbnailService thumbnailService)
        {
            _context = context;
            _thumbnailService = thumbnailService;
        }

        public async Task<IActionResult> Details(int id)
        {
            var video = await _context.Videos
                .Include(v => v.VideoTags)
                .ThenInclude(vt => vt.Tag)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (video == null)
            {
                return NotFound();
            }

            return View(video);
        }

        public async Task<IActionResult> ByTag(int tagId)
        {
            var tag = await _context.Tags
                .Include(t => t.VideoTags)
                .ThenInclude(vt => vt.Video)
                .FirstOrDefaultAsync(t => t.Id == tagId);

            if (tag == null)
            {
                return NotFound();
            }

            return View(tag);
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
