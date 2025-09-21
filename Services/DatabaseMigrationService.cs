using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;

namespace VideoLibrary.Services
{
    public class DatabaseMigrationService
    {
        private readonly AppDbContext _context;

        public DatabaseMigrationService(AppDbContext context)
        {
            _context = context;
        }
        
        public async Task MigrateFromSqliteToPostgreSQL(string sqliteConnectionString)
        {
            // Create SQLite context with old configuration
            var sqliteOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(sqliteConnectionString)
                .Options;

            using var sqliteContext = new AppDbContext(sqliteOptions);

            // Ensure PostgreSQL database is created with new schema
            await _context.Database.EnsureCreatedAsync();

            // Migrate data in dependency order
            await MigrateBasicEntities(sqliteContext);
            await MigrateRelationshipEntities(sqliteContext);

            Console.WriteLine("Migration completed successfully!");
        }

        private async Task MigrateBasicEntities(AppDbContext sqliteContext)
        {
            // Migrate Tags first (no dependencies)
            var tags = await sqliteContext.Tags.ToListAsync();
            if (tags.Any())
            {
                // Clear identity tracking to avoid conflicts
                foreach (var tag in tags)
                {
                    _context.Entry(tag).State = EntityState.Detached;
                }

                await _context.Tags.AddRangeAsync(tags);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Migrated {tags.Count} tags");
            }

            // Migrate Videos
            var videos = await sqliteContext.Videos.ToListAsync();
            if (videos.Any())
            {
                foreach (var video in videos)
                {
                    _context.Entry(video).State = EntityState.Detached;
                }

                await _context.Videos.AddRangeAsync(videos);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Migrated {videos.Count} videos");
            }

            // Migrate Galleries
            var galleries = await sqliteContext.Galleries.ToListAsync();
            if (galleries.Any())
            {
                foreach (var gallery in galleries)
                {
                    _context.Entry(gallery).State = EntityState.Detached;
                }

                await _context.Galleries.AddRangeAsync(galleries);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Migrated {galleries.Count} galleries");
            }

            // Migrate Playlists
            var playlists = await sqliteContext.Playlists.ToListAsync();
            if (playlists.Any())
            {
                foreach (var playlist in playlists)
                {
                    _context.Entry(playlist).State = EntityState.Detached;
                }

                await _context.Playlists.AddRangeAsync(playlists);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Migrated {playlists.Count} playlists");
            }

            // Migrate Contents
            var contents = await sqliteContext.Contents.ToListAsync();
            if (contents.Any())
            {
                foreach (var content in contents)
                {
                    _context.Entry(content).State = EntityState.Detached;
                }

                await _context.Contents.AddRangeAsync(contents);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Migrated {contents.Count} contents");
            }

            // Migrate LogEntries
            var logEntries = await sqliteContext.LogEntries.ToListAsync();
            if (logEntries.Any())
            {
                foreach (var logEntry in logEntries)
                {
                    _context.Entry(logEntry).State = EntityState.Detached;
                }

                await _context.LogEntries.AddRangeAsync(logEntries);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Migrated {logEntries.Count} log entries");
            }

            // Migrate Transcriptions
            var transcriptions = await sqliteContext.Transcriptions.ToListAsync();
            if (transcriptions.Any())
            {
                foreach (var transcription in transcriptions)
                {
                    _context.Entry(transcription).State = EntityState.Detached;
                }

                await _context.Transcriptions.AddRangeAsync(transcriptions);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Migrated {transcriptions.Count} transcriptions");
            }
        }

        private async Task MigrateRelationshipEntities(AppDbContext sqliteContext)
        {
            // Migrate VideoTags
            var videoTags = await sqliteContext.VideoTags.ToListAsync();
            if (videoTags.Any())
            {
                foreach (var videoTag in videoTags)
                {
                    _context.Entry(videoTag).State = EntityState.Detached;
                }

                await _context.VideoTags.AddRangeAsync(videoTags);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Migrated {videoTags.Count} video tags");
            }

            // Migrate GalleryTags
            var galleryTags = await sqliteContext.GalleryTags.ToListAsync();
            if (galleryTags.Any())
            {
                foreach (var galleryTag in galleryTags)
                {
                    _context.Entry(galleryTag).State = EntityState.Detached;
                }

                await _context.GalleryTags.AddRangeAsync(galleryTags);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Migrated {galleryTags.Count} gallery tags");
            }

            // Migrate PlaylistTags
            var playlistTags = await sqliteContext.PlaylistTags.ToListAsync();
            if (playlistTags.Any())
            {
                foreach (var playlistTag in playlistTags)
                {
                    _context.Entry(playlistTag).State = EntityState.Detached;
                }

                await _context.PlaylistTags.AddRangeAsync(playlistTags);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Migrated {playlistTags.Count} playlist tags");
            }
        }

        public async Task ValidateMigration(string sqliteConnectionString)
        {
            var sqliteOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(sqliteConnectionString)
                .Options;

            using var sqliteContext = new AppDbContext(sqliteOptions);

            // Compare record counts
            var validationResults = new Dictionary<string, (int SQLite, int PostgreSQL)>
            {
                ["Videos"] = (await sqliteContext.Videos.CountAsync(), await _context.Videos.CountAsync()),
                ["Tags"] = (await sqliteContext.Tags.CountAsync(), await _context.Tags.CountAsync()),
                ["VideoTags"] = (await sqliteContext.VideoTags.CountAsync(), await _context.VideoTags.CountAsync()),
                ["Galleries"] = (await sqliteContext.Galleries.CountAsync(), await _context.Galleries.CountAsync()),
                ["GalleryTags"] = (await sqliteContext.GalleryTags.CountAsync(), await _context.GalleryTags.CountAsync()),
                ["Playlists"] = (await sqliteContext.Playlists.CountAsync(), await _context.Playlists.CountAsync()),
                ["PlaylistTags"] = (await sqliteContext.PlaylistTags.CountAsync(), await _context.PlaylistTags.CountAsync()),
                ["Contents"] = (await sqliteContext.Contents.CountAsync(), await _context.Contents.CountAsync()),
                ["LogEntries"] = (await sqliteContext.LogEntries.CountAsync(), await _context.LogEntries.CountAsync()),
                ["Transcriptions"] = (await sqliteContext.Transcriptions.CountAsync(), await _context.Transcriptions.CountAsync())
            };

            Console.WriteLine("\nMigration Validation Results:");
            Console.WriteLine("Entity".PadRight(15) + "SQLite".PadRight(10) + "PostgreSQL".PadRight(12) + "Status");
            Console.WriteLine(new string('-', 45));

            foreach (var result in validationResults)
            {
                var status = result.Value.SQLite == result.Value.PostgreSQL ? "✓ Match" : "✗ Mismatch";
                Console.WriteLine($"{result.Key.PadRight(15)}{result.Value.SQLite.ToString().PadRight(10)}{result.Value.PostgreSQL.ToString().PadRight(12)}{status}");
            }
        }
        
    }
}