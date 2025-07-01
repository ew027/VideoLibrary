using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;

namespace VideoLibrary.Controllers
{
    [Authorize]
    public class PlaylistController : Controller
    {
        private readonly AppDbContext _context;

        public PlaylistController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Tag(int tagId)
        {
            var tag = await _context.Tags
                .Include(t => t.VideoTags)
                .ThenInclude(vt => vt.Video)
                .FirstOrDefaultAsync(t => t.Id == tagId);

            if (tag == null)
            {
                return NotFound();
            }

            var videos = tag.VideoTags
                .Select(vt => vt.Video)
                .OrderBy(v => v.Title)
                .ToList();

            if (!videos.Any())
            {
                TempData["ErrorMessage"] = "No videos found for this tag.";
                return RedirectToAction("ByTag", "Video", new { tagId });
            }

            var playlist = new PlaylistViewModel
            {
                TagId = tagId,
                TagName = tag.Name,
                Videos = videos,
                CurrentIndex = 0
            };

            return View(playlist);
        }

        public async Task<IActionResult> Random(int? count = null)
        {
            var videoCount = count ?? 10;

            var videos = await _context.Videos
                .FromSqlRaw("SELECT * FROM Videos ORDER BY RANDOM()")
                .Take(videoCount)
                .ToListAsync();

            if (!videos.Any())
            {
                TempData["ErrorMessage"] = "No videos available for random playlist.";
                return RedirectToAction("Index", "Home");
            }

            var playlist = new PlaylistViewModel
            {
                TagName = $"Random Playlist ({videos.Count} videos)",
                Videos = videos,
                CurrentIndex = 0,
                IsRandom = true
            };

            return View("Tag", playlist);
        }

        public async Task<IActionResult> Custom(int tagId, string videoIds, string order)
        {
            // .Where(v => videoIds.Contains(v.Id))
            var sortOrder = (order == "order") ? "id" : "RANDOM()";
            var sql = $"select * from Videos where Id in ({videoIds}) order by {sortOrder}";

            var videos = await _context.Videos
                .FromSqlRaw(sql)
                .ToListAsync();

            if (!videos.Any())
            {
                TempData["ErrorMessage"] = "No videos available for custom playlist.";
                return RedirectToAction("Index", "Home");
            }

            var tag = await _context.Tags.FirstOrDefaultAsync(x => x.Id == tagId);

            var playlist = new PlaylistViewModel
            {
                TagName = $"Custom playlist for {tag!.Name}",
                Videos = videos,
                CurrentIndex = 0,
                IsRandom = (order != "order")
            };

            return View("Tag", playlist);
        }

        public async Task<IActionResult> Recent(int? count = null)
        {
            var videoCount = count ?? 20;

            var videos = await _context.Videos
                .OrderByDescending(v => v.DateAdded)
                .Take(videoCount)
                .ToListAsync();

            if (!videos.Any())
            {
                TempData["ErrorMessage"] = "No videos available.";
                return RedirectToAction("Index", "Home");
            }

            var playlist = new PlaylistViewModel
            {
                TagName = $"Recently Added ({videos.Count} videos)",
                Videos = videos,
                CurrentIndex = 0,
                IsRecent = true
            };

            return View("Tag", playlist);
        }

        public async Task<IActionResult> All()
        {
            var videos = await _context.Videos
                .OrderBy(v => v.Title)
                .ToListAsync();

            if (!videos.Any())
            {
                TempData["ErrorMessage"] = "No videos available.";
                return RedirectToAction("Index", "Home");
            }

            var playlist = new PlaylistViewModel
            {
                TagName = $"All Videos ({videos.Count} videos)",
                Videos = videos,
                CurrentIndex = 0,
                IsAll = true
            };

            return View("Tag", playlist);
        }

        [HttpGet]
        public async Task<IActionResult> ApiPlaylist(int? tagId)
        {
            if (!tagId.HasValue)
            {
                return BadRequest();
            }

            var tag = await _context.Tags
                .Include(t => t.VideoTags)
                .ThenInclude(vt => vt.Video)
                .FirstOrDefaultAsync(t => t.Id == tagId);

            if (tag == null)
            {
                return NotFound();
            }

            var videos = tag.VideoTags
                .Select(vt => new
                {
                    id = vt.Video.Id,
                    title = vt.Video.Title,
                    duration = vt.Video.DurationFormatted,
                    streamUrl = Url.Action("Stream", "Video", new { id = vt.Video.Id }),
                    thumbnailUrl = !string.IsNullOrEmpty(vt.Video.ThumbnailPath)
                        ? Url.Action("SmallThumbnail", "Video", new { id = vt.Video.Id })
                        : null
                })
                .OrderBy(v => v.title)
                .ToList();

            return Json(videos);
        }
    }
}
