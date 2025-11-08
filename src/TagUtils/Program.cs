using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VideoLibrary.Models;
using VideoLibrary.Services;

namespace VideoLibrary.Tools.BulkTagMover
{
    /// <summary>
    /// Console application to bulk move tags to a new parent using TagHierarchyService
    /// Usage: BulkTagMover <tag-ids-file> <new-parent-id>
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("=== Video Library Bulk Tag Mover ===\n");

            // Validate arguments
            if (args.Length < 2)
            {
                ShowUsage();
                return 1;
            }

            string tagIdsFile = args[0];
            string newParentIdStr = args[1];

            // Validate file exists
            if (!File.Exists(tagIdsFile))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: File '{tagIdsFile}' not found.");
                Console.ResetColor();
                return 1;
            }

            // Parse and validate parent ID
            if (!int.TryParse(newParentIdStr, out int newParentId))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Invalid parent ID '{newParentIdStr}'. Must be an integer.");
                Console.ResetColor();
                return 1;
            }

            // Special case: -1 means move to root (null parent)
            int? parentId = newParentId == -1 ? null : newParentId;

            // Setup dependency injection and configuration
            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            try
            {
                // Read tag IDs from file
                var tagIds = await ReadTagIdsFromFileAsync(tagIdsFile);

                if (tagIds.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Warning: No tag IDs found in file.");
                    Console.ResetColor();
                    return 0;
                }

                Console.WriteLine($"Found {tagIds.Count} tag IDs to process.");
                Console.WriteLine($"Target parent: {(parentId.HasValue ? $"Tag ID {parentId.Value}" : "Root (no parent)")}");
                Console.WriteLine();

                // Confirm with user
                Console.Write("Do you want to proceed? (y/n): ");
                var confirm = Console.ReadLine()?.Trim().ToLower();

                if (confirm != "y" && confirm != "yes")
                {
                    Console.WriteLine("Operation cancelled.");
                    return 0;
                }

                Console.WriteLine();

                // Get services
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var tagHierarchyService = scope.ServiceProvider.GetRequiredService<TagHierarchyService>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                // Validate parent exists (if not root)
                if (parentId.HasValue)
                {
                    var parentTag = await context.Tags.FindAsync(parentId.Value);
                    if (parentTag == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error: Parent tag with ID {parentId.Value} not found.");
                        Console.ResetColor();
                        return 1;
                    }

                    Console.WriteLine($"Parent tag: '{parentTag.Name}' (Level {parentTag.Level})");
                    Console.WriteLine();
                }

                // Process each tag
                int successCount = 0;
                int errorCount = 0;
                int skippedCount = 0;
                var errors = new List<(int tagId, string error)>();

                foreach (var tagId in tagIds)
                {
                    try
                    {
                        // Check if tag exists
                        var tag = await context.Tags.FindAsync(tagId);
                        if (tag == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[SKIP] Tag ID {tagId}: Not found");
                            Console.ResetColor();
                            skippedCount++;
                            continue;
                        }

                        // Check if already at target parent
                        if (tag.ParentId == parentId)
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"[SKIP] Tag ID {tagId} ('{tag.Name}'): Already at target parent");
                            Console.ResetColor();
                            skippedCount++;
                            continue;
                        }

                        // Check for circular reference (moving to own descendant)
                        if (parentId.HasValue && await tagHierarchyService.IsDescendantOfAsync(parentId.Value, tagId))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[ERROR] Tag ID {tagId} ('{tag.Name}'): Cannot move to own descendant");
                            Console.ResetColor();
                            errors.Add((tagId, "Circular reference - target is descendant"));
                            errorCount++;
                            continue;
                        }

                        // Perform the move
                        Console.Write($"[MOVING] Tag ID {tagId} ('{tag.Name}')... ");

                        await tagHierarchyService.MoveTagAsync(tagId, parentId);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("OK");
                        Console.ResetColor();

                        successCount++;

                        // Small delay to avoid overwhelming the database
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"FAILED");
                        Console.WriteLine($"  Error: {ex.Message}");
                        Console.ResetColor();

                        logger.LogError(ex, "Failed to move tag {TagId}", tagId);
                        errors.Add((tagId, ex.Message));
                        errorCount++;
                    }
                }

                // Summary
                Console.WriteLine();
                Console.WriteLine("=== Summary ===");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Successfully moved: {successCount}");
                Console.ResetColor();

                if (skippedCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"Skipped: {skippedCount}");
                    Console.ResetColor();
                }

                if (errorCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed: {errorCount}");
                    Console.ResetColor();
                }

                // Show errors if any
                if (errors.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine("=== Errors ===");
                    foreach (var (tagId, error) in errors)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Tag ID {tagId}: {error}");
                        Console.ResetColor();
                    }

                    // Optionally write errors to file
                    var errorFile = Path.ChangeExtension(tagIdsFile, ".errors.txt");
                    await File.WriteAllLinesAsync(errorFile,
                        errors.Select(e => $"{e.tagId}\t{e.error}"));

                    Console.WriteLine();
                    Console.WriteLine($"Errors also written to: {errorFile}");
                }

                Console.WriteLine();
                Console.WriteLine("Operation completed.");

                return errorCount > 0 ? 2 : 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nFatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                return 1;
            }
        }

        static void ShowUsage()
        {
            Console.WriteLine("Usage: BulkTagMover <tag-ids-file> <new-parent-id>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  tag-ids-file    Path to file containing tag IDs (one per line)");
            Console.WriteLine("  new-parent-id   ID of the new parent tag (use -1 for root level)");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  BulkTagMover tags.txt 42");
            Console.WriteLine("  BulkTagMover tags.txt -1     (move to root)");
            Console.WriteLine();
            Console.WriteLine("File format (tags.txt):");
            Console.WriteLine("  123");
            Console.WriteLine("  456");
            Console.WriteLine("  789");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  - Lines starting with # are ignored (comments)");
            Console.WriteLine("  - Empty lines are ignored");
            Console.WriteLine("  - Invalid IDs are skipped with a warning");
            Console.WriteLine("  - Circular references are detected and prevented");
        }

        static async Task<List<int>> ReadTagIdsFromFileAsync(string filePath)
        {
            var tagIds = new List<int>();
            var lines = await File.ReadAllLinesAsync(filePath);
            int lineNumber = 0;

            foreach (var line in lines)
            {
                lineNumber++;
                var trimmedLine = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                // Try to parse as integer
                if (int.TryParse(trimmedLine, out int tagId))
                {
                    tagIds.Add(tagId);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: Line {lineNumber} contains invalid tag ID: '{trimmedLine}'");
                    Console.ResetColor();
                }
            }

            return tagIds;
        }

        static void ConfigureServices(IServiceCollection services)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            services.AddSingleton<IConfiguration>(configuration);

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Warning); // Only show warnings and errors
            });

            // Add DbContext
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString)
                       .UseSnakeCaseNamingConvention());

            // Add TagHierarchyService
            services.AddScoped<TagHierarchyService>();
        }
    }
}