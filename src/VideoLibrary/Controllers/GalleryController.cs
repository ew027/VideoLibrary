using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using VideoLibrary.Models;
using VideoLibrary.Models.ViewModels;
using VideoLibrary.Services;

namespace VideoLibrary.Controllers
{
    [Authorize]
    public class GalleryController : Controller
    {
        private readonly AppDbContext _context;
        private readonly GalleryService _galleryService;

        public GalleryController(AppDbContext context, GalleryService galleryService)
        {
            _context = context;
            _galleryService = galleryService;
        }

        public async Task<IActionResult> Details(int id)
        {
            var gallery = await _context.Galleries
                .Include(g => g.GalleryTags)
                .ThenInclude(gt => gt.Tag)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gallery == null)
            {
                return NotFound();
            }

            var images = await _galleryService.GetGalleryImagesAsync(gallery);

            var viewModel = new GalleryListViewModel
            {
                Gallery = gallery,
                Images = images,
                ImageCount = images.Count,
                CoverImage = images.FirstOrDefault()
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Image(int galleryId, int imageIndex)
        {
            var gallery = await _context.Galleries
                .Include(g => g.GalleryTags)
                .ThenInclude(gt => gt.Tag)
                .FirstOrDefaultAsync(g => g.Id == galleryId);

            if (gallery == null)
            {
                return NotFound();
            }

            var images = await _galleryService.GetGalleryImagesAsync(gallery);

            if (imageIndex < 0 || imageIndex >= images.Count)
            {
                return NotFound();
            }

            var viewModel = new GalleryDetailViewModel
            {
                Gallery = gallery,
                Images = images,
                CurrentImageIndex = imageIndex,
                CurrentImage = images[imageIndex],
                PreviousImage = imageIndex > 0 ? images[imageIndex - 1] : null,
                NextImage = imageIndex < images.Count - 1 ? images[imageIndex + 1] : null
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var gallery = await _context.Galleries
                .Include(g => g.GalleryTags)
                .ThenInclude(gt => gt.Tag)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gallery == null)
            {
                return NotFound();
            }

            ViewBag.AllTags = await _context.Tags.OrderBy(t => t.Name).ToListAsync();
            return View(gallery);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTags(int galleryId, int[] selectedTags)
        {
            var gallery = await _context.Galleries
                .Include(g => g.GalleryTags)
                .FirstOrDefaultAsync(g => g.Id == galleryId);

            if (gallery == null)
            {
                return NotFound();
            }

            // Remove existing tags
            _context.GalleryTags.RemoveRange(gallery.GalleryTags);

            // Add new tags
            foreach (var tagId in selectedTags)
            {
                gallery.GalleryTags.Add(new GalleryTag { GalleryId = galleryId, TagId = tagId });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = galleryId });
        }

        [HttpPost]
        public async Task<IActionResult> AddTag(int galleryId, string tagName)
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

            var existingGalleryTag = await _context.GalleryTags
                .FirstOrDefaultAsync(gt => gt.GalleryId == galleryId && gt.TagId == existingTag.Id);

            if (existingGalleryTag == null)
            {
                _context.GalleryTags.Add(new GalleryTag { GalleryId = galleryId, TagId = existingTag.Id });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = galleryId });
        }

        public IActionResult Thumbnail(int galleryId, string fileName)
        {
            var gallery = _context.Galleries.Find(galleryId);
            if (gallery == null)
            {
                return NotFound();
            }

            var filenameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);

            var thumbnailPath = Path.Combine(gallery.FolderPath, "thumbnails", $"{filenameWithoutExtension}_thumb{extension}");
            var fullImagePath = Path.Combine(gallery.FolderPath, fileName);

            var imagePath = System.IO.File.Exists(thumbnailPath) ? thumbnailPath : fullImagePath;

            if (!System.IO.File.Exists(imagePath))
            {
                return NotFound();
            }

            var contentType = GetImageContentType(imagePath);
            var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);

            return File(fileStream, contentType);
        }

        [HttpPost]
        public async Task<IActionResult> SetThumbnail(int galleryId, string fileName)
        {
            var gallery = await _context.Galleries.FindAsync(galleryId);
            if (gallery == null)
            {
                return NotFound();
            }

            var filenameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);

            var largeThumbnailPath = Path.Combine(gallery.FolderPath, "thumbnails", $"{filenameWithoutExtension}_lgthumb{extension}");
            
            if (!System.IO.File.Exists(largeThumbnailPath))
            {
                var mediumPath = Path.Combine(gallery.FolderPath, "medium", $"{filenameWithoutExtension}_medium{extension}");

                CreateResizedImage(mediumPath, largeThumbnailPath, 750);
            }
            
            gallery.ThumbnailPath = largeThumbnailPath;
            await _context.SaveChangesAsync();
            
            return RedirectToAction(nameof(Details), new { id = galleryId });
        }

        public IActionResult Medium(int galleryId, string fileName)
        {
            var gallery = _context.Galleries.Find(galleryId);
            if (gallery == null)
            {
                return NotFound();
            }

            var filenameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);

            var mediumPath = Path.Combine(gallery.FolderPath, "medium", $"{filenameWithoutExtension}_medium{extension}");
            var fullImagePath = Path.Combine(gallery.FolderPath, fileName);

            var imagePath = System.IO.File.Exists(mediumPath) ? mediumPath : fullImagePath;

            if (!System.IO.File.Exists(imagePath))
            {
                return NotFound();
            }

            var contentType = GetImageContentType(imagePath);
            var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);

            return File(fileStream, contentType);
        }

        public IActionResult Full(int galleryId, string fileName)
        {
            var gallery = _context.Galleries.Find(galleryId);
            if (gallery == null)
            {
                return NotFound();
            }

            var fullImagePath = Path.Combine(gallery.FolderPath, fileName);

            if (!System.IO.File.Exists(fullImagePath))
            {
                return NotFound();
            }

            var contentType = GetImageContentType(fullImagePath);
            var fileStream = new FileStream(fullImagePath, FileMode.Open, FileAccess.Read);

            return File(fileStream, contentType);
        }

        private string GetImageContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".tiff" or ".tif" => "image/tiff",
                _ => "application/octet-stream"
            };
        }

        private void CreateResizedImage(string sourcePath, string destinationPath, int maxSize)
        {
            var encoder = new JpegEncoder { Quality = 90 };

            using var image = SixLabors.ImageSharp.Image.Load(sourcePath);
            var (newWidth, newHeight) = CalculateNewDimensions(image.Width, image.Height, maxSize);

            image.Mutate(x => x.Resize(newWidth, newHeight));
            image.Save(destinationPath, encoder);
        }
        private (int width, int height) CalculateNewDimensions(int originalWidth, int originalHeight, int maxSize)
        {
            if (originalWidth <= maxSize && originalHeight <= maxSize)
                return (originalWidth, originalHeight);

            double ratio = Math.Min((double)maxSize / originalWidth, (double)maxSize / originalHeight);
            return ((int)(originalWidth * ratio), (int)(originalHeight * ratio));
        }
    }
}