using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VideoLibrary.Models;
using VideoLibrary.Services;

namespace VideoLibrary.Migration
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Video Library Database Migration...");

            // Configuration
            var sqliteConnectionString = "Data Source=videolibrary.db"; // Your SQLite database path
            var postgresConnectionString = "Host=;Database=videolibrary_dev;Username=vidlibusr;Password=";

            // Create host with dependency injection
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // Configure PostgreSQL context for target database
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql(postgresConnectionString)
                               .UseSnakeCaseNamingConvention()); // If using naming conventions package

                    services.AddScoped<DatabaseMigrationService>();
                })
                .Build();

            try
            {
                using var scope = host.Services.CreateScope();
                
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var migrationService = scope.ServiceProvider.GetRequiredService<DatabaseMigrationService>();
                

                Console.WriteLine("Starting migration process...");

                // Perform the migration
                await migrationService.MigrateFromSqliteToPostgreSQL(sqliteConnectionString);

                Console.WriteLine("\nValidating migration...");

                // Validate the migration
                await migrationService.ValidateMigration(sqliteConnectionString);

                await ResetKnownSequences(context);

                Console.WriteLine("\nMigration completed successfully!");



                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Migration failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        public static async Task ResetKnownSequences(AppDbContext context)
        {
            var resetCommands = new[]
            {
        "SELECT setval(pg_get_serial_sequence('videos', 'id'), COALESCE(MAX(id), 1)) FROM videos;",
        "SELECT setval(pg_get_serial_sequence('tags', 'id'), COALESCE(MAX(id), 1)) FROM tags;",
        "SELECT setval(pg_get_serial_sequence('galleries', 'id'), COALESCE(MAX(id), 1)) FROM galleries;",
        "SELECT setval(pg_get_serial_sequence('playlists', 'id'), COALESCE(MAX(id), 1)) FROM playlists;",
        "SELECT setval(pg_get_serial_sequence('contents', 'id'), COALESCE(MAX(id), 1)) FROM contents;",
        "SELECT setval(pg_get_serial_sequence('log_entries', 'id'), COALESCE(MAX(id), 1)) FROM log_entries;",
        "SELECT setval(pg_get_serial_sequence('transcriptions', 'id'), COALESCE(MAX(id), 1)) FROM transcriptions;"
    };

            foreach (var command in resetCommands)
            {
                try
                {
                    await context.Database.ExecuteSqlRawAsync(command);
                    Console.WriteLine($"Executed: {command}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing {command}: {ex.Message}");
                }
            }
        }
    }
}