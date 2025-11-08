using FFMpegCore;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;

namespace VideoLibrary.Services
{
    public class VideoClippingService
    {
        private readonly ThumbnailService _thumbnailService;
        private readonly VideoAnalysisService _videoAnalysisService;
        private readonly AppDbContext _dbContext;

        private readonly IConfiguration _configuration;
        private readonly ILogger<VideoClippingService> _logger;
        private readonly DbLogService _dbLogger;

        public VideoClippingService(IConfiguration configuration,
            ILogger<VideoClippingService> logger,
            ThumbnailService thumbnailService,
            VideoAnalysisService videoAnalysisService,
            AppDbContext dbContext,
            DbLogService dbLogger)
        {
            _configuration = configuration;
            _logger = logger;
            _thumbnailService = thumbnailService;
            _videoAnalysisService = videoAnalysisService;
            _dbContext = dbContext;
            _dbLogger = dbLogger;
        }

        public async Task<bool> CreateClipAsync(int originalVideoId, double startSeconds, double endSeconds)
        {
            try
            {
                await _dbLogger.Log("Clip generation started", DbLogLevel.Information);

                var clipFolder = _configuration["VideoLibrary:ClipFolderPath"];

                var originalVideo = await _dbContext.Videos
                    .Include(v => v.VideoTags)
                    .ThenInclude(vt => vt.Tag)
                    .FirstOrDefaultAsync(v => v.Id == originalVideoId);

                if (originalVideo == null)
                {
                    _logger.LogError("Original video not found: {VideoId}", originalVideoId);
                    return false;
                }

                // Generate unique clip filename
                var originalFileName = Path.GetFileNameWithoutExtension(originalVideo.FilePath);
                var originalExtension = Path.GetExtension(originalVideo.FilePath);
                //var originalDirectory = Path.GetDirectoryName(originalVideo.FilePath);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var clipFileName = $"{originalFileName}_clip_{timestamp}{originalExtension}";
                var clipFilePath = Path.Combine(clipFolder!, clipFileName);

                // Create the clip using FFMpegCore
                var startTime = TimeSpan.FromSeconds(startSeconds);
                var duration = TimeSpan.FromSeconds(endSeconds - startSeconds);

                _logger.LogInformation("Creating clip: {ClipPath} from {OriginalPath}, Start: {Start}, Duration: {Duration}",
                    clipFilePath, originalVideo.FilePath, startTime, duration);

                /*
                var success = await FFMpegArguments
                    .FromFileInput(originalVideo.FilePath)
                    .OutputToFile(clipFilePath, true, options => options
                        .Seek(startTime)
                        .WithDuration(duration)
                        .WithVideoCodec("libx264")
                        .WithAudioCodec("aac"))
                .ProcessAsynchronously();
                */

                var success = FFMpeg.SubVideo(originalVideo.FilePath,
                    clipFilePath,
                    startTime,
                    TimeSpan.FromSeconds(endSeconds)
                );

                if (!success || !File.Exists(clipFilePath))
                {
                    _logger.LogError("Failed to create clip: {ClipPath}", clipFilePath);
                    return false;
                }

                // Create database entry for the clip
                var clipVideo = new Video
                {
                    Title = $"{originalVideo.Title} (Clip)",
                    FilePath = clipFilePath,
                    DateAdded = DateTime.Now
                };

                _dbContext.Videos.Add(clipVideo);
                await _dbContext.SaveChangesAsync();

                // Copy tags from original video
                foreach (var originalTag in originalVideo.VideoTags)
                {
                    _dbContext.VideoTags.Add(new VideoTag
                    {
                        VideoId = clipVideo.Id,
                        TagId = originalTag.TagId
                    });
                }

                // Add "Clips" tag
                var clipsTag = await _dbContext.Tags.FirstOrDefaultAsync(t => t.Name == "clips");
                if (clipsTag == null)
                {
                    clipsTag = new Tag { Name = "clips" };
                    _dbContext.Tags.Add(clipsTag);
                    await _dbContext.SaveChangesAsync();
                }

                _dbContext.VideoTags.Add(new VideoTag
                {
                    VideoId = clipVideo.Id,
                    TagId = clipsTag.Id
                });

                await _dbContext.SaveChangesAsync();

                try
                {
                    await _videoAnalysisService.AnalyzeVideo(clipVideo);
                    await _dbContext.SaveChangesAsync();

                    var thumbnailFolderPath = _configuration["VideoLibrary:ThumbnailFolderPath"];
                    if (!string.IsNullOrEmpty(thumbnailFolderPath))
                    {
                        Directory.CreateDirectory(thumbnailFolderPath);
                        await _thumbnailService.GenerateVideoThumbnailAtTime(clipVideo.Id, 5);
                        await _dbContext.SaveChangesAsync();
                    }

                    _logger.LogInformation("Clip processing completed: {ClipId}", clipVideo.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing clip: {ClipId}", clipVideo.Id);
                    await _dbLogger.Log(ex, "Error processing clip", DbLogLevel.Error);
                }

                _logger.LogInformation("Clip created successfully: {ClipId} - {ClipPath}", clipVideo.Id, clipFilePath);
                await _dbLogger.Log($"Clip created successfully: {clipVideo.Id} - {clipFilePath}", DbLogLevel.Information);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating clip for video: {VideoId}", originalVideoId);
                await _dbLogger.Log(ex, "Error processing clip", DbLogLevel.Error);

                return false;
            }
        }
    }
}