using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;
using VideoLibrary.Services;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore.Infrastructure;
using FFMpegCore;
using Microsoft.VisualBasic;

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

// In Program.cs - Update your database configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

/*
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
*/
// Add background services
//builder.Services.AddHostedService<VideoScanService>();
builder.Services.AddScoped<ThumbnailService>();
builder.Services.AddScoped<VideoAnalysisService>();
builder.Services.AddScoped<VideoScanService>();
builder.Services.AddScoped<VideoClippingService>();
builder.Services.AddScoped<DbLogService>();
builder.Services.AddSingleton<ImageCacheService>();
builder.Services.AddSingleton<GalleryService>();
//builder.Services.AddHostedService<ThumbnailService>(provider => provider.GetService<ThumbnailService>()!);

var app = builder.Build();

await ApplyDatabaseMigrations(app);

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

static async Task ApplyDatabaseMigrations(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Check if database exists and can connect
        var canConnect = await context.Database.CanConnectAsync();
        if (!canConnect)
        {
            logger.LogError("Cannot connect to database. Please check connection string.");
            throw new InvalidOperationException("Database connection failed");
        }

        if (environment.IsProduction())
        {
            logger.LogInformation("Production environment detected. Checking for pending migrations...");

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();

            logger.LogInformation($"Applied migrations: {appliedMigrations.Count()}");
            logger.LogInformation($"Pending migrations: {pendingMigrations.Count()}");

            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying pending migrations:");
                foreach (var migration in pendingMigrations)
                {
                    logger.LogInformation($"  - {migration}");
                }

                logger.LogInformation("Starting database migration...");
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migration completed successfully");
            }
            else
            {
                logger.LogInformation("Database is up to date. No migrations needed.");
            }
        }
        else
        {
            // Development/Staging - just log the status, don't auto-migrate
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();

            logger.LogInformation($"Development environment - Applied migrations: {appliedMigrations.Count()}");

            if (pendingMigrations.Any())
            {
                logger.LogWarning($"Pending migrations detected ({pendingMigrations.Count()}). Run 'dotnet ef database update' to apply them:");
                foreach (var migration in pendingMigrations)
                {
                    logger.LogWarning($"  - {migration}");
                }
            }
            else
            {
                logger.LogInformation("Database is up to date.");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while checking/applying database migrations");

        // In production, you might want to fail fast
        if (environment.IsProduction())
        {
            throw;
        }

        // In development, just warn and continue
        logger.LogWarning("Continuing startup despite migration error in development environment");
    }
}

#region Old manual migrations

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

static async Task AddContentsDbStructure(AppDbContext context)
{
    try
    {
        // Try to query the Contents table to see if it exists
        await context.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM Contents LIMIT 1");
    }
    catch (Exception)
    {
        // Table doesn't exist, create it
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE Contents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                ContentText TEXT,
                DateCreated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                DateLastUpdated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            )");
    }
}

static async Task AddPlaylisTagsDbStructure(AppDbContext context)
{
    // Check if Galleries table exists, if not create gallery structure
    try
    {
        await context.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM PlaylistTags LIMIT 1");
    }
    catch
    {
        // Create gallery tables
        await context.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE Playlists ADD COLUMN ThumbnailPath TEXT;

            CREATE TABLE PlaylistTags (
                PlaylistId INTEGER NOT NULL,
                TagId INTEGER NOT NULL,
                PRIMARY KEY (PlaylistId, TagId),
                FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE,
                FOREIGN KEY (TagId) REFERENCES Tags(Id) ON DELETE CASCADE
            );

            CREATE INDEX IX_PlaylistTags_PlaylistId ON PlaylistTags(PlaylistId);
            CREATE INDEX IX_PlaylistTags_TagId ON PlaylistTags(TagId);
        ");

        var logger = context.GetService<ILogger<Program>>();
        logger?.LogInformation("Added gallery tables to existing database");
    }
}

static async Task AddTranscriptionDbStructure(AppDbContext context)
{
    // check if Transcriptions table exists, if not create
    try
    {
        await context.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM Transcriptions LIMIT 1");
    }
    catch
    {
        var createTableSql = @"
            CREATE TABLE Transcriptions (
                Id INTEGER NOT NULL CONSTRAINT PK_Transcriptions PRIMARY KEY AUTOINCREMENT,
                VideoId INTEGER NOT NULL,
                ContentId INTEGER,
                Status INTEGER NOT NULL,
                DateRequested TEXT NOT NULL DEFAULT (datetime('now')),
                DateCompleted TEXT
            );

            CREATE INDEX IX_Transcriptions_VideoId 
            ON Transcriptions (VideoId);

            CREATE INDEX IX_Transcriptions_Status 
            ON Transcriptions (Status);

            CREATE INDEX IX_Transcriptions_DateRequested 
            ON Transcriptions (DateRequested);
        ";

        await context.Database.ExecuteSqlRawAsync(createTableSql);

        var logger = context.GetService<ILogger<Program>>();
        logger?.LogInformation("Added transcription table to existing database");
    }
}

static async Task AddTagArchivedColumn(AppDbContext context)
{
    try
    {
        // Try to query a new column - if it fails, we need to add the columns
        await context.Database.ExecuteSqlRawAsync("SELECT IsArchived FROM Tags LIMIT 1");
    }
    catch
    {
        // Columns don't exist, add them
        await context.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE Tags ADD COLUMN IsArchived INTEGER;
            UPDATE Tags SET IsArchived=0;
        ");

        var logger = context.GetService<ILogger<Program>>();
        logger?.LogInformation("Added new gallery columns to existing database");
    }
}

#endregion