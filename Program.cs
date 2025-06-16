using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;
using VideoLibrary.Services;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

// Configure logging for systemd
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    builder.Logging.AddSystemdConsole();
}

// Load configuration from /etc/videolibrary/ on Linux
if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    builder.Configuration.AddJsonFile("/etc/videolibrary/appsettings.Production.json", optional: true, reloadOnChange: true);
}

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
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add background services
builder.Services.AddHostedService<VideoScanService>();
builder.Services.AddSingleton<ThumbnailService>();
builder.Services.AddHostedService<ThumbnailService>(provider => provider.GetService<ThumbnailService>()!);

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
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // Remove HSTS for reverse proxy scenarios
    // app.UseHsts();
}

// Remove HTTPS redirection for reverse proxy scenarios
// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Add health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
