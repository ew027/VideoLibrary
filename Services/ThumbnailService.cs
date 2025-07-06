using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using VideoLibrary.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using static System.Net.Mime.MediaTypeNames;
using Image = SixLabors.ImageSharp.Image;

namespace VideoLibrary.Services
{
    public class ThumbnailService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ThumbnailService> _logger;
        private readonly DbLogService _dbLogger;

        public ThumbnailService(IServiceProvider serviceProvider, 
            IConfiguration configuration, 
            ILogger<ThumbnailService> logger, 
            DbLogService dbLogger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _dbLogger = dbLogger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait a bit before starting thumbnail generation
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await GenerateThumbnailsAsync();
                await UpdateTagThumbnailsAsync();
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }

        public async Task GenerateThumbnailsAsync()
        {
            try
            {
                _logger.LogInformation("Starting thumbnail generation...");
                await _dbLogger.Log("Starting thumbnail generation...", DbLogLevel.Information);

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var videosWithoutThumbnails = await dbContext.Videos
                    .Where(v => string.IsNullOrEmpty(v.ThumbnailPath))
                    .ToListAsync();

                var thumbnailFolderPath = _configuration["VideoLibrary:ThumbnailFolderPath"];
                var ffmpegPath = _configuration["FFmpegPath"];

                if (string.IsNullOrEmpty(thumbnailFolderPath) || string.IsNullOrEmpty(ffmpegPath))
                {
                    _logger.LogWarning("Thumbnail folder path or ffmpeg path not configured");
                    return;
                }

                Directory.CreateDirectory(thumbnailFolderPath);

                foreach (var video in videosWithoutThumbnails)
                {
                    await GenerateVideoThumbnail(video, thumbnailFolderPath, ffmpegPath, ThumbnailMode.New);
                    await dbContext.SaveChangesAsync();
                }

                _logger.LogInformation("Thumbnail generation complete");
                await _dbLogger.Log("Thumbnail generation complete", DbLogLevel.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnails");
                await _dbLogger.Log(ex, "Error generating thumbnails", DbLogLevel.Error);
            }
        }

        private async Task GenerateVideoThumbnail(Video video, string thumbnailFolderPath, string ffmpegPath, ThumbnailMode mode, int timePoint = 30)
        {
            try
            {
                var baseFileName = Path.GetFileNameWithoutExtension(video.FilePath);
                
                var thumbnailFileName = $"{baseFileName}_thumb.jpg";
                var thumbnailPath = Path.Combine(thumbnailFolderPath, thumbnailFileName);

                var smallThumbnailFileName = $"{baseFileName}_thumb_small.jpg";
                var smallThumbnailPath = Path.Combine(thumbnailFolderPath, smallThumbnailFileName);

                if (File.Exists(thumbnailPath) && File.Exists(smallThumbnailPath) && mode == ThumbnailMode.New)
                {
                    _logger.LogInformation("Thumbnail already exists for: {Title}", video.Title);
                    video.ThumbnailPath = thumbnailPath;
                    return;
                }

                if (File.Exists(thumbnailPath) && !File.Exists(smallThumbnailPath) && mode == ThumbnailMode.New)
                {
                    _logger.LogInformation("Generating small thumbnail for: {Title}", video.Title);
                    video.ThumbnailPath = thumbnailPath;

                    await GenerateSmallThumbnail(thumbnailPath, smallThumbnailPath);
                    
                    return;
                }

                if (video.FilePath.Contains("clips") && mode == ThumbnailMode.New)
                {
                    timePoint = 5; // Special case for clips
                }

                var thumbPoint = TimeSpan.FromSeconds(timePoint).ToString(@"hh\:mm\:ss");

                var ffmpegArgs = $"-i \"{video.FilePath}\" -ss {thumbPoint} -vframes 1 -y \"{thumbnailPath}\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = ffmpegArgs,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0 && File.Exists(thumbnailPath))
                    {
                        video.ThumbnailPath = thumbnailPath;
                        _logger.LogInformation("Generated thumbnail for: {Title}", video.Title);

                        // Generate small thumbnail from the full-size one
                        await GenerateSmallThumbnail(thumbnailPath, smallThumbnailPath);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to generate thumbnail for: {Title}", video.Title);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail for video: {Title}", video.Title);
            }
        }

        public async Task GenerateVideoThumbnailAtTime(int videoId, int seconds)
        {
            try
            {
                _logger.LogInformation($"Generating thumbnail for video ID {videoId} at {seconds} seconds");

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var video = await dbContext.Videos.FindAsync(videoId);
                if (video == null) return;

                var thumbnailFolderPath = _configuration["VideoLibrary:ThumbnailFolderPath"];
                var ffmpegPath = _configuration["FFmpegPath"];

                if (string.IsNullOrEmpty(thumbnailFolderPath) || string.IsNullOrEmpty(ffmpegPath)) return;

                await GenerateVideoThumbnail(video, thumbnailFolderPath, ffmpegPath, ThumbnailMode.Recreate, seconds);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail at specific time for video ID: {VideoId}", videoId);
            }
        }

        public string GetSmallThumbnailPath(string thumbnailPath)
        {
            if (string.IsNullOrEmpty(thumbnailPath))
                return string.Empty;

            var directory = Path.GetDirectoryName(thumbnailPath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(thumbnailPath);
            var extension = Path.GetExtension(thumbnailPath);

            var smallThumb = Path.Combine(directory!, $"{fileNameWithoutExt}_small{extension}");

            return File.Exists(smallThumb) ? smallThumb : thumbnailPath;
        }

        private async Task GenerateSmallThumbnail(string sourcePath, string destinationPath)
        {
            try
            {
                using var image = await Image.LoadAsync(sourcePath);

                // Calculate new dimensions maintaining aspect ratio
                var maxWidth = 400;
                var aspectRatio = (double)image.Height / image.Width;
                var newWidth = Math.Min(image.Width, maxWidth);
                var newHeight = (int)(newWidth * aspectRatio);

                // Resize and save
                image.Mutate(x => x.Resize(newWidth, newHeight));
                await image.SaveAsJpegAsync(destinationPath);

                _logger.LogDebug("Generated small thumbnail: {Path}", destinationPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating small thumbnail from: {SourcePath}", sourcePath);
            }
        }

        public async Task UpdateTagThumbnailsAsync()
        {
            try
            {
                await _dbLogger.Log("Tag thumbnail generation started", DbLogLevel.Information);

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var tagsWithoutThumbnails = await dbContext.Tags
                    .Where(t => string.IsNullOrEmpty(t.ThumbnailPath))
                    .Include(t => t.VideoTags)
                    .ThenInclude(vt => vt.Video)
                    .ToListAsync();

                foreach (var tag in tagsWithoutThumbnails)
                {
                    var firstVideoWithThumbnail = tag.VideoTags
                        .Select(vt => vt.Video)
                        .FirstOrDefault(v => !string.IsNullOrEmpty(v.ThumbnailPath));

                    if (firstVideoWithThumbnail != null)
                    {
                        tag.ThumbnailPath = firstVideoWithThumbnail.ThumbnailPath;
                    }
                    else
                    {
                        // check to see if we can get a thumbnail from a gallery instead
                        var galleryTag = await dbContext.GalleryTags
                            .Where(gt => gt.TagId == tag.Id)
                            .Include(gt => gt.Gallery)
                            .FirstOrDefaultAsync();

                        if (galleryTag != null && !string.IsNullOrEmpty(galleryTag.Gallery!.ThumbnailPath))
                        {
                            tag.ThumbnailPath = galleryTag.Gallery.ThumbnailPath;
                        }
                    }
                }

                await dbContext.SaveChangesAsync();

                _logger.LogInformation("Tag thumbnails updated");
                await _dbLogger.Log("Tag thumbnail generation complete", DbLogLevel.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tag thumbnails");
                await _dbLogger.Log(ex, "Error generating tab thumbnails", DbLogLevel.Error);
            }
        }
    }

    public enum ThumbnailMode
    {
        New,
        Recreate
    }
}

