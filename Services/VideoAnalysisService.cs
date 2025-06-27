using FFMpegCore;
using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;

namespace VideoLibrary.Services
{
    public class VideoAnalysisService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<VideoAnalysisService> _logger;

        public VideoAnalysisService(IServiceProvider serviceProvider, ILogger<VideoAnalysisService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait a bit before starting analysis
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await AnalyzeVideosAsync();
                await Task.Delay(TimeSpan.FromMinutes(20), stoppingToken); // Run every 20 minutes
            }
        }

        public async Task AnalyzeVideosAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Get videos that haven't been analyzed yet
                var videosToAnalyze = await dbContext.Videos
                    .Where(v => !v.DurationSeconds.HasValue || !v.Width.HasValue || !v.FileSizeBytes.HasValue)
                    .ToListAsync();

                foreach (var video in videosToAnalyze)
                {
                    await AnalyzeVideo(video);
                }

                if (videosToAnalyze.Any())
                {
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("Analyzed {Count} videos", videosToAnalyze.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing videos");
            }
        }

        public async Task<bool> AnalyzeVideo(Video video)
        {
            try
            {
                if (!File.Exists(video.FilePath))
                {
                    _logger.LogWarning("Video file not found: {FilePath}", video.FilePath);
                    return false;
                }

                // Get file size
                var fileInfo = new FileInfo(video.FilePath);
                video.FileSizeBytes = fileInfo.Length;

                // Analyze video with FFMpegCore
                var mediaInfo = await FFProbe.AnalyseAsync(video.FilePath);

                // Extract video stream info
                var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
                if (videoStream != null)
                {
                    video.Width = videoStream.Width;
                    video.Height = videoStream.Height;
                    //video.VideoCodec = videoStream.CodecName;
                    //video.BitRate = mediaInfo.Format.BitRate;
                }

                // Duration
                video.DurationSeconds = mediaInfo.Duration.TotalSeconds;

                _logger.LogInformation("Analyzed video: {Title} - {Duration}s, {Resolution}, {FileSize}",
                    video.Title,
                    video.DurationSeconds,
                    video.ResolutionFormatted,
                    video.FileSizeFormatted);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing video: {Title}", video.Title);
                return false;
            }
        }

        public async Task<bool> ReAnalyzeVideo(int videoId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var video = await dbContext.Videos.FindAsync(videoId);
                if (video == null) return false;

                var success = await AnalyzeVideo(video);
                if (success)
                {
                    await dbContext.SaveChangesAsync();
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error re-analyzing video ID: {VideoId}", videoId);
                return false;
            }
        }
    }
}