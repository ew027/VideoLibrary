using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace VideoLibrary.Services
{
    public class GalleryService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GalleryService> _logger;
        private readonly ConcurrentDictionary<int, List<GalleryImage>> _imageCache = new();
        private readonly ConcurrentDictionary<int, DateTime> _cacheTimestamps = new();
        private readonly string[] _imageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff" };
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);
        private readonly IServiceProvider _serviceProvider;

        public GalleryService(IConfiguration configuration, ILogger<GalleryService> logger, IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<List<GalleryImage>> GetGalleryImagesAsync(Gallery gallery)
        {
            // Check cache first
            if (_imageCache.TryGetValue(gallery.Id, out var cachedImages) &&
                _cacheTimestamps.TryGetValue(gallery.Id, out var cacheTime) &&
                DateTime.Now - cacheTime < _cacheExpiry)
            {
                return cachedImages;
            }

            // Scan directory for images
            var images = await Task.Run(() => ScanGalleryDirectory(gallery));

            // Update cache
            _imageCache[gallery.Id] = images;
            _cacheTimestamps[gallery.Id] = DateTime.Now;

            return images;
        }

        private List<GalleryImage> ScanGalleryDirectory(Gallery gallery)
        {
            var images = new List<GalleryImage>();

            try
            {
                if (!Directory.Exists(gallery.FolderPath))
                {
                    _logger.LogWarning("Gallery folder does not exist: {FolderPath}", gallery.FolderPath);
                    return images;
                }

                var thumbnailFolder = Path.Combine(gallery.FolderPath, "thumbnails");
                var mediumFolder = Path.Combine(gallery.FolderPath, "medium");

                var imageFiles = Directory.GetFiles(gallery.FolderPath)
                    .Where(file => _imageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .OrderBy(f => f)
                    .ToList();

                for (int i = 0; i < imageFiles.Count; i++)
                {
                    var imageFile = imageFiles[i];
                    var fileName = Path.GetFileName(imageFile);
                    var fileInfo = new FileInfo(imageFile);

                    var thumbnailPath = Path.Combine(thumbnailFolder, fileName);
                    var mediumPath = Path.Combine(mediumFolder, fileName);

                    images.Add(new GalleryImage
                    {
                        FileName = fileName,
                        FullPath = imageFile,
                        ThumbnailPath = File.Exists(thumbnailPath) ? thumbnailPath : imageFile,
                        MediumPath = File.Exists(mediumPath) ? mediumPath : imageFile,
                        LastModified = fileInfo.LastWriteTime,
                        FileSize = fileInfo.Length,
                        Index = i
                    });
                }

                _logger.LogDebug("Found {Count} images in gallery: {GalleryName}", images.Count, gallery.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning gallery directory: {FolderPath}", gallery.FolderPath);
            }

            return images;
        }

        public void ClearCache(int? galleryId = null)
        {
            if (galleryId.HasValue)
            {
                _imageCache.TryRemove(galleryId.Value, out _);
                _cacheTimestamps.TryRemove(galleryId.Value, out _);
            }
            else
            {
                _imageCache.Clear();
                _cacheTimestamps.Clear();
            }
        }

        public async Task ScanForNewGalleriesAsync()
        {
            try
            {
                var galleryRootPath = _configuration["VideoLibrary:GalleryRootPath"];
                if (string.IsNullOrEmpty(galleryRootPath) || !Directory.Exists(galleryRootPath))
                {
                    _logger.LogWarning("Gallery root path not configured or doesn't exist: {Path}", galleryRootPath);
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var existingGalleries = await dbContext.Galleries.Select(g => g.FolderPath).ToListAsync();
                var directories = Directory.GetDirectories(galleryRootPath);

                foreach (var directory in directories)
                {
                    if (!existingGalleries.Contains(directory))
                    {
                        var directoryName = Path.GetFileName(directory);
                        var gallery = new Gallery
                        {
                            Name = directoryName,
                            FolderPath = directory,
                            DateAdded = DateTime.Now
                        };

                        (gallery.ThumbnailPath, gallery.ImageCount) = GetFirstThumbnailAndCount(directory);
                        
                        dbContext.Galleries.Add(gallery);
                        await dbContext.SaveChangesAsync();

                        var tags = gallery.Name.Split('_');

                        foreach (var tagName in tags)
                        {
                            // check it's not a number
                            if (int.TryParse(tagName, out _))
                                continue;

                            var existingTag = await dbContext.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
                            if (existingTag == null)
                            {
                                existingTag = new Tag { Name = tagName };
                                dbContext.Tags.Add(existingTag);
                                await dbContext.SaveChangesAsync();
                            }

                            var existingGalleryTag = await dbContext.GalleryTags
                                .FirstOrDefaultAsync(gt => gt.GalleryId == gallery.Id && gt.TagId == existingTag.Id);

                            if (existingGalleryTag == null)
                            {
                                dbContext.GalleryTags.Add(new GalleryTag { GalleryId = gallery.Id, TagId = existingTag.Id });
                                await dbContext.SaveChangesAsync();
                            }
                        }

                        _logger.LogInformation("Added new gallery: {Name}", gallery.Name);
                    }
                }

                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning for new galleries");
            }
        }

        private (string, int) GetFirstThumbnailAndCount(string directory)
        {
            var thumbPath = Path.Combine(directory, "thumbnails");

            var files = Directory.GetFiles(thumbPath);

            return (files.FirstOrDefault() ?? string.Empty, files.Length);
        }
    }
}