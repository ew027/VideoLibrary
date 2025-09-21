using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VideoLibrary.Models;
using VideoLibrary.Models.ViewModels;

namespace VideoLibrary.Controllers
{
    [Authorize]
    public class VideoEditingController : Controller
    {
        private readonly AppDbContext _context;

        public VideoEditingController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int videoId)
        {
            var video = await _context.Videos
                .Include(v => v.VideoTags)
                .ThenInclude(vt => vt.Tag)
                .FirstOrDefaultAsync(v => v.Id == videoId);

            if (video == null)
            {
                return NotFound();
            }

            var viewModel = new VideoEditingViewModel
            {
                Video = video,
                Clips = new List<ClipData>()
            };

            return View(viewModel);
        }

        [HttpPost]
        public IActionResult ExportClips([FromBody] ClipExportData exportData)
        {
            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var fileName = $"{Path.GetFileNameWithoutExtension(exportData.VideoName)}_clips.json";

            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
        }
    }
}