using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        public async Task ScanVideosAsync()
        {
            try
            {
                _logger.LogInformation("Starting video scan...");
                
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

                _logger.LogInformation("Found {Count} video files in {Path}", videoFiles.Count, videoFolderPath);

                var existingVideos = await dbContext.Videos.Select(v => v.FilePath).ToListAsync();

                _logger.LogInformation("Found {Count} existing videos in database", existingVideos.Count);

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

                        await dbContext.SaveChangesAsync();

                        if (video.FilePath.Contains("clips"))
                        {
                            var existingTag = await dbContext.Tags.FirstOrDefaultAsync(t => t.Name == "clips");
                            if (existingTag == null)
                            {
                                existingTag = new Tag { Name = "clips" };
                                dbContext.Tags.Add(existingTag);
                                await dbContext.SaveChangesAsync();
                            }

                            var existingVideoTag = await dbContext.VideoTags
                                .FirstOrDefaultAsync(vt => vt.VideoId == video.Id && vt.TagId == existingTag.Id);

                            if (existingVideoTag == null)
                            {
                                dbContext.VideoTags.Add(new VideoTag { VideoId = video.Id, TagId = existingTag.Id });
                                await dbContext.SaveChangesAsync();
                            }
                        }
                        else
                        {
                            var tags = video.Title.Split('_');

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

                                var existingVideoTag = await dbContext.VideoTags
                                    .FirstOrDefaultAsync(vt => vt.VideoId == video.Id && vt.TagId == existingTag.Id);

                                if (existingVideoTag == null)
                                {
                                    dbContext.VideoTags.Add(new VideoTag { VideoId = video.Id, TagId = existingTag.Id });
                                    await dbContext.SaveChangesAsync();
                                }
                            }
                        }
                            
                   

                    }
                }

                // now do a sweep through all existing videos to see if any have been deleted
                var currentVideos = await dbContext.Videos.ToListAsync();
                int deletedCount = 0;

                foreach (var video in currentVideos)
                {
                    if (!File.Exists(video.FilePath))
                    {
                        dbContext.Videos.Remove(video);
                        deletedCount++;

                        _logger.LogInformation($"Deleting {video.Title} as {video.FilePath} no longer exists");
                    }
                }

                _logger.LogInformation($"{deletedCount} videos removed");

                await dbContext.SaveChangesAsync();

                _logger.LogInformation("Video scanning complete");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning videos");
            }
        }
    }
}

