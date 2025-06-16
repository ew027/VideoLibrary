using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using VideoLibrary.Models;

namespace VideoLibrary.Services
{
    public class VideoScanService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VideoScanService> _logger;
        private readonly string[] _videoExtensions = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm" };

        public VideoScanService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<VideoScanService> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial scan on startup
            await ScanVideosAsync();

            // Then scan every 30 minutes
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                await ScanVideosAsync();
            }
        }

        private async Task ScanVideosAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var videoFolderPath = _configuration["VideoLibrary:VideoFolderPath"];
                if (string.IsNullOrEmpty(videoFolderPath) || !Directory.Exists(videoFolderPath))
                {
                    _logger.LogWarning("Video folder path not configured or doesn't exist: {Path}", videoFolderPath);
                    return;
                }

                var videoFiles = Directory.GetFiles(videoFolderPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => _videoExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToList();

                var existingVideos = await dbContext.Videos.Select(v => v.FilePath).ToListAsync();

                foreach (var videoFile in videoFiles)
                {
                    if (!existingVideos.Contains(videoFile))
                    {
                        var video = new Video
                        {
                            Title = Path.GetFileNameWithoutExtension(videoFile),
                            FilePath = videoFile,
                            DateAdded = DateTime.Now
                        };

                        dbContext.Videos.Add(video);
                        _logger.LogInformation("Added new video: {Title}", video.Title);
                    }
                }

                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning videos");
            }
        }
    }
}

