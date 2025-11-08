namespace VideoLibrary.Services
{
    public class ImageCacheService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImageCacheService> _logger;
        private readonly string _cacheFolder;

        public ImageCacheService(IConfiguration configuration, ILogger<ImageCacheService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _cacheFolder = _configuration["ImageCacheFolder"] ?? "ImageCache";
        }

        public string GetCachedPath(string originalPath)
        {
            var filename = Path.GetFileName(originalPath);

            var cachedPath = Path.Combine(_cacheFolder, filename);

            if (!Directory.Exists(_cacheFolder))
            {
                Directory.CreateDirectory(_cacheFolder);
            }

            if (!File.Exists(cachedPath))
            {
                try
                {
                    Directory.CreateDirectory(_cacheFolder);
                    byte[] fileData = File.ReadAllBytes(originalPath);
                    File.WriteAllBytes(cachedPath, fileData);
                    _logger.LogInformation("Cached image: {OriginalPath} to {CachedPath}", originalPath, cachedPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cache image: {OriginalPath}", originalPath);
                    return originalPath; // Fallback to original path if caching fails
                }
            }

            return cachedPath;
        }

        public void ClearCache(string originalPath)
        {
            var filename = Path.GetFileName(originalPath);

            var cachedPath = Path.Combine(_cacheFolder, filename);

            try
            {
                if (File.Exists(cachedPath))
                {
                    File.Delete(cachedPath);
                    _logger.LogInformation("Cleared image cache for {CachedFile}", cachedPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear image cache at {CachedFile}", cachedPath);
            }
        }
    }
}
