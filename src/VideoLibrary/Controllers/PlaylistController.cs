using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;
using VideoLibrary.Models.ViewModels;

namespace VideoLibrary.Controllers
{
    [Authorize]
    public class PlaylistController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PlaylistController> _logger;

        public PlaylistController(AppDbContext context, ILogger<PlaylistController> logger)
        {
            _context = context;
            _logger = logger;
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

            var playlist = new PlaylistPlayViewModel
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

            var playlist = new PlaylistPlayViewModel
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
            // Validate and parse video IDs
            var selectedVideoIds = ParseVideoIds(videoIds);

            if (!selectedVideoIds.Any())
            {
                TempData["ErrorMessage"] = "No valid video IDs provided";
                return RedirectToAction("ByTag", "Video", new { tagId });
            }

            // Get selected videos
            var videos = await _context.Videos
                .Where(v => selectedVideoIds.Contains(v.Id))
                .Include(v => v.VideoTags)
                .ThenInclude(vt => vt.Tag)
                .ToListAsync();

            // Order the videos to match the selection order or shuffle
            if (order == "shuffle")
            {
                videos = videos.OrderBy(v => Guid.NewGuid()).ToList();
            }
            else
            {
                // Maintain the order from the original selection
                videos = videos.OrderBy(v => selectedVideoIds.IndexOf(v.Id)).ToList();
            }

            if (!videos.Any())
            {
                TempData["ErrorMessage"] = "No videos available for custom playlist.";
                return RedirectToAction("Index", "Home");
            }

            var tag = await _context.Tags.FirstOrDefaultAsync(x => x.Id == tagId);
            var tagName = (tag is null) ? "Custom playlist" : $"Custom playlist for {tag.Name}";

            var playlist = new PlaylistPlayViewModel
            {
                TagName = tagName,
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

            var playlist = new PlaylistPlayViewModel
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

            var playlist = new PlaylistPlayViewModel
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

        // get all the saved playlists to display
        public async Task<IActionResult> Saved()
        {
            var savedPlaylists = await _context.Playlists
                .Include(v => v.PlaylistTags)
                .ThenInclude(vt => vt.Tag)
                .OrderByDescending(p => p.DateLastPlayed)
                .ToListAsync();

            var playlistViewModels = new List<SavedPlaylistViewModel>();

            foreach (var playlist in savedPlaylists)
            {
                playlistViewModels.Add(new SavedPlaylistViewModel
                {
                    Playlist = playlist
                });
            }

            var viewModel = new SavedPlaylistListViewModel
            {
                Playlists = playlistViewModels
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Play(int id)
        {
            var savedPlaylist = await _context.Playlists
                .Include(v => v.PlaylistTags)
                .ThenInclude(vt => vt.Tag)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (savedPlaylist == null)
            {
                return NotFound();
            }

            // Update play statistics
            savedPlaylist.PlayCount++;
            savedPlaylist.DateLastPlayed = DateTime.Now;
            await _context.SaveChangesAsync();

            var videoIds = savedPlaylist.GetVideoIdList();
            var videos = await _context.Videos
                .Where(v => videoIds.Contains(v.Id))
                .Include(v => v.VideoTags)
                .ThenInclude(vt => vt.Tag)
                .ToListAsync();

            // Apply ordering
            if (savedPlaylist.IsShuffled)
            {
                videos = videos.OrderBy(v => Guid.NewGuid()).ToList();
            }
            else
            {
                videos = videos.OrderBy(v => videoIds.IndexOf(v.Id)).ToList();
            }

            var playlist = new PlaylistPlayViewModel
            {
                PlaylistId = savedPlaylist.Id,
                TagName = savedPlaylist.Name,
                Videos = videos,
                CurrentIndex = 0,
                IsRandom = savedPlaylist.IsShuffled,
                PlaylistTags = savedPlaylist.PlaylistTags.ToList()
            };

            return View("Tag", playlist);
        }

        [HttpGet]
        public IActionResult Create(int? tagId, string? videoIds, string? order)
        {
            var viewModel = new CreatePlaylistViewModel
            {
                VideoIds = videoIds ?? string.Empty,
                IsShuffled = order == "shuffle",
                TagId = tagId
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreatePlaylistViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Validate and parse video IDs
            var selectedVideoIds = ParseVideoIds(model.VideoIds);

            if (!selectedVideoIds.Any())
            {
                TempData["ErrorMessage"] = "No valid video IDs provided";
                return View(model);
            }

            // Get selected videos
            var videos = await _context.Videos
                .Where(v => selectedVideoIds.Contains(v.Id))
                .Include(v => v.VideoTags)
                .ThenInclude(vt => vt.Tag)
                .ToListAsync();

            // now get a unique list of tags from these videos
            var uniqueTags = videos
                .SelectMany(v => v.VideoTags)
                .Select(vt => vt.Tag)
                .DistinctBy(t => t.Id)
                .OrderBy(t => t.Name)
                .ToList();

            var savedPlaylist = new Playlist
            {
                Name = model.Name,
                Description = model.Description,
                VideoIds = model.VideoIds,
                IsShuffled = model.IsShuffled,
                DateCreated = DateTime.Now,
                DateLastPlayed = DateTime.Now,
                ThumbnailPath = videos.FirstOrDefault(v => !string.IsNullOrEmpty(v.ThumbnailPath))?.ThumbnailPath ?? string.Empty
            };

            _context.Playlists.Add(savedPlaylist);
            await _context.SaveChangesAsync();

            foreach (var tag in uniqueTags)
            {
                savedPlaylist.PlaylistTags.Add(new PlaylistTag
                {
                    TagId = tag.Id,
                    Playlist = savedPlaylist
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Play), new { id = savedPlaylist.Id });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var playlist = await _context.Playlists.FindAsync(id);
            if (playlist != null)
            {
                _context.Playlists.Remove(playlist);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Saved));
        }

        [HttpPost]
        public async Task<IActionResult> AddVideo(int videoId, int playlistId)
        {
            var playlist = await _context.Playlists.FindAsync(playlistId);
            if (playlist != null)
            {
                var videoIds = playlist.GetVideoIdList();

                if (!videoIds.Contains(videoId))
                {
                    videoIds.Add(videoId);
                    playlist.SetVideoIds(videoIds);

                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Video added to playlist!";
            }

            return RedirectToAction("Details", "Video", new { id = videoId });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrder(int playlistId, string videoIds)
        {
            var playlist = await _context.Playlists.FindAsync(playlistId);
            if (playlist != null)
            {
                playlist.VideoIds = videoIds;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Playlist updated";
            }

            return RedirectToAction("Play", new { id = playlistId });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTags(int playlistId, string selectedTags, string newTags)
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

            var playlist = await _context.Playlists
                .Include(v => v.PlaylistTags)
                .FirstOrDefaultAsync(v => v.Id == playlistId);

            if (playlist == null)
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
            _context.PlaylistTags.RemoveRange(playlist.PlaylistTags);

            // Add new tags
            foreach (var tagId in tagList)
            {
                playlist.PlaylistTags.Add(new PlaylistTag { PlaylistId = playlistId, TagId = tagId });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Play", new { id = playlistId });
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

                var playlist = await _context.Playlists.FindAsync(id);
                if (playlist == null)
                {
                    return Json(new { success = false, message = "Playlist not found" });
                }

                playlist.Name = title.Trim();
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

        private List<int> ParseVideoIds(string videoIds)
        {
            if (string.IsNullOrWhiteSpace(videoIds))
                return new List<int>();

            return videoIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id.Trim(), out var parsed) ? parsed : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .Distinct()
                .ToList();
        }
    }
}
