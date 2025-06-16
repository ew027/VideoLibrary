using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using VideoLibrary.Models;

namespace VideoLibrary.Services
{
    public class ThumbnailService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ThumbnailService> _logger;

        public ThumbnailService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<ThumbnailService> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait a bit before starting thumbnail generation
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await GenerateThumbnailsAsync();
                await UpdateTagThumbnailsAsync();
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }

        private async Task GenerateThumbnailsAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var videosWithoutThumbnails = await dbContext.Videos
                    .Where(v => string.IsNullOrEmpty(v.ThumbnailPath))
                    .ToListAsync();

                var thumbnailFolderPath = _configuration["VideoLibrary:ThumbnailFolderPath"];
                if (string.IsNullOrEmpty(thumbnailFolderPath))
                {
                    _logger.LogWarning("Thumbnail folder path not configured");
                    return;
                }

                Directory.CreateDirectory(thumbnailFolderPath);

                foreach (var video in videosWithoutThumbnails)
                {
                    await GenerateVideoThumbnail(video, thumbnailFolderPath);
                }

                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnails");
            }
        }

        private async Task GenerateVideoThumbnail(Video video, string thumbnailFolderPath)
        {
            try
            {
                var thumbnailFileName = $"{Path.GetFileNameWithoutExtension(video.FilePath)}_thumb.jpg";
                var thumbnailPath = Path.Combine(thumbnailFolderPath, thumbnailFileName);

                var ffmpegArgs = $"-i \"{video.FilePath}\" -ss 00:00:30 -vframes 1 -y \"{thumbnailPath}\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
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
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var video = await dbContext.Videos.FindAsync(videoId);
                if (video == null) return;

                var thumbnailFolderPath = _configuration["VideoLibrary:ThumbnailFolderPath"];
                if (string.IsNullOrEmpty(thumbnailFolderPath)) return;

                Directory.CreateDirectory(thumbnailFolderPath);

                var thumbnailFileName = $"{Path.GetFileNameWithoutExtension(video.FilePath)}_thumb.jpg";
                var thumbnailPath = Path.Combine(thumbnailFolderPath, thumbnailFileName);

                var timeSpan = TimeSpan.FromSeconds(seconds);
                var timeString = timeSpan.ToString(@"hh\:mm\:ss");

                var ffmpegArgs = $"-i \"{video.FilePath}\" -ss {timeString} -vframes 1 -y \"{thumbnailPath}\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
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
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("Updated thumbnail for: {Title} at {Seconds}s", video.Title, seconds);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail at specific time for video ID: {VideoId}", videoId);
            }
        }

        private async Task UpdateTagThumbnailsAsync()
        {
            try
            {
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
                }

                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tag thumbnails");
            }
        }
    }
}

