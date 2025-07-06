using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;
using VideoLibrary.Services;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore.Infrastructure;
using FFMpegCore;

var builder = WebApplication.CreateBuilder(args);

// Configure logging for systemd
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add systemd integration
if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    builder.Logging.AddSystemdConsole();
    builder.Services.AddSystemd();
}
else
{
    GlobalFFOptions.Configure(options => options.BinaryFolder = "C:\\Utils\\ffmpeg\\bin");
}

// Load configuration from /etc/videolibrary/ on Linux
if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    builder.Configuration.AddJsonFile("/etc/videolibrary/appsettings.Production.json", optional: true, reloadOnChange: true);
}

// Configure authentication settings
builder.Services.Configure<AuthConfig>(builder.Configuration.GetSection("Authentication"));

// Configure authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

// Configure Kestrel for production
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = null; // Allow large video files
});

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add Entity Framework
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));

    // Disable verbose logging in production
    if (builder.Environment.IsProduction())
    {
        options.EnableSensitiveDataLogging(false);
        options.EnableDetailedErrors(false);
    }
});

// Add background services
//builder.Services.AddHostedService<VideoScanService>();
builder.Services.AddScoped<ThumbnailService>();
builder.Services.AddScoped<VideoAnalysisService>();
builder.Services.AddScoped<VideoScanService>();
builder.Services.AddScoped<VideoClippingService>();
builder.Services.AddScoped<DbLogService>();
builder.Services.AddSingleton<GalleryService>();
//builder.Services.AddHostedService<ThumbnailService>(provider => provider.GetService<ThumbnailService>()!);

var app = builder.Build();

// Ensure data directory exists
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Data Source="))
{
    var dbPath = connectionString.Split("Data Source=")[1].Split(';')[0];
    var dbDirectory = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrEmpty(dbDirectory))
    {
        Directory.CreateDirectory(dbDirectory);
    }
}

// Create database if it doesn't exist
try
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Database initialized successfully");

    // Add video metadata columns if they don't exist
    await AddVideoMetadataColumnsIfNeeded(context);
    await AddVideoNotesColumnIfNeeded(context);
    await AddGalleryDbStructure(context);
    await AddGalleryThumbnailColumn(context);
    await AddLogEntryDbStructure(context);
    await AddPlaylistDbStructure(context);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while initializing the database");
    throw;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Add health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}));

// Log startup completion
app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Video Library application started successfully");
});

app.Run();

static async Task AddVideoMetadataColumnsIfNeeded(AppDbContext context)
{
    try
    {
        // Try to query a new column - if it fails, we need to add the columns
        await context.Database.ExecuteSqlRawAsync("SELECT FileSizeBytes FROM Videos LIMIT 1");
    }
    catch
    {
        // Columns don't exist, add them
        await context.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE Videos ADD COLUMN FileSizeBytes INTEGER;
            ALTER TABLE Videos ADD COLUMN Width INTEGER;
            ALTER TABLE Videos ADD COLUMN Height INTEGER;
            ALTER TABLE Videos ADD COLUMN DurationSeconds REAL;
            ALTER TABLE Videos ADD COLUMN VideoCodec TEXT;
            ALTER TABLE Videos ADD COLUMN AudioCodec TEXT;
            ALTER TABLE Videos ADD COLUMN BitRate INTEGER;
        ");

        var logger = context.GetService<ILogger<Program>>();
        logger?.LogInformation("Added video metadata columns to existing database");
    }
}

static async Task AddVideoNotesColumnIfNeeded(AppDbContext context)
{
    try
    {
        // Try to query a new column - if it fails, we need to add the columns
        await context.Database.ExecuteSqlRawAsync("SELECT Notes FROM Videos LIMIT 1");
    }
    catch
    {
        // Columns don't exist, add them
        await context.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE Videos ADD COLUMN Notes TEXT;
        ");

        var logger = context.GetService<ILogger<Program>>();
        logger?.LogInformation("Added video notes column to existing database");
    }
}

static async Task AddGalleryDbStructure(AppDbContext context)
{
    // Check if Galleries table exists, if not create gallery structure
    try
    {
        await context.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM Galleries LIMIT 1");
    }
    catch
    {
        // Create gallery tables
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE Galleries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                FolderPath TEXT NOT NULL,
                ThumbnailPath TEXT NOT NULL,
                ImageCount INTEGER,
                Description TEXT,
                DateAdded DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE GalleryTags (
                GalleryId INTEGER NOT NULL,
                TagId INTEGER NOT NULL,
                PRIMARY KEY (GalleryId, TagId),
                FOREIGN KEY (GalleryId) REFERENCES Galleries(Id) ON DELETE CASCADE,
                FOREIGN KEY (TagId) REFERENCES Tags(Id) ON DELETE CASCADE
            );

            CREATE INDEX IX_GalleryTags_GalleryId ON GalleryTags(GalleryId);
            CREATE INDEX IX_GalleryTags_TagId ON GalleryTags(TagId);
            CREATE INDEX IX_Galleries_FolderPath ON Galleries(FolderPath);
        ");

        var logger = context.GetService<ILogger<Program>>();
        logger?.LogInformation("Added gallery tables to existing database");
    }
}

static async Task AddGalleryThumbnailColumn(AppDbContext context)
{
    try
    {
        // Try to query a new column - if it fails, we need to add the columns
        await context.Database.ExecuteSqlRawAsync("SELECT ThumbnailPath FROM Galleries LIMIT 1");
    }
    catch
    {
        // Columns don't exist, add them
        await context.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE Galleries ADD COLUMN ThumbnailPath TEXT;
        ");

        var logger = context.GetService<ILogger<Program>>();
        logger?.LogInformation("Added new gallery columns to existing database");
    }
}

static async Task AddLogEntryDbStructure(AppDbContext context)
{
    // Check if LogEntries table exists, if not create
    try
    {
        await context.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM LogEntries LIMIT 1");
    }
    catch
    {
        // Create gallery tables
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE LogEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Message TEXT NOT NULL,
                StackTrace TEXT NULL,
                LogLevel INTEGER,
                Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
        ");

        var logger = context.GetService<ILogger<Program>>();
        logger?.LogInformation("Added logentries tables to existing database");
    }
}

static async Task AddPlaylistDbStructure(AppDbContext context)
{
    // Check if Playlists table exists, if not create
    try
    {
        await context.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM Playlists LIMIT 1");
    }
    catch
    {
        // Create gallery tables
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE Playlists (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT,
                VideoIds TEXT NOT NULL DEFAULT '',
                IsShuffled INTEGER NOT NULL DEFAULT 0,
                DateCreated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                DateLastPlayed DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PlayCount INTEGER NOT NULL DEFAULT 0
            );
        ");

        var logger = context.GetService<ILogger<Program>>();
        logger?.LogInformation("Added logentries tables to existing database");
    }
}