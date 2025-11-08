using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ImageProcessor
{
    class Program
    {
        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };

        static void Main(string[] args)
        {
            Console.WriteLine("Image Processing Console App");
            Console.WriteLine("============================");

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: ImageProcessor.exe <root_folder_path>");
                return;
            }

            string rootFolder = args[0];
            if (!Directory.Exists(rootFolder))
            {
                Console.WriteLine($"Error: Directory '{rootFolder}' does not exist.");
                return;
            }

            while (true)
            {
                ShowMenu();
                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        ListFoldersWithSize(rootFolder);
                        break;
                    case "2":
                        ResizeSelectedFolders(rootFolder);
                        break;
                    case "3":
                        CreateThumbnailsAndMediumImages(rootFolder);
                        break;
                    case "4":
                        Console.WriteLine("Goodbye!");
                        return;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }

                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }

        static void ShowMenu()
        {
            Console.WriteLine("\nChoose an option:");
            Console.WriteLine("1) List all folders and disk space consumed by images");
            Console.WriteLine("2) Resize images in selected folders to max 2000px");
            Console.WriteLine("3) Create thumbnails (250px) and medium images (1000px) for all folders");
            Console.WriteLine("4) Exit");
            Console.Write("Enter your choice (1-4): ");
        }

        static void ListFoldersWithSize(string rootFolder)
        {
            Console.WriteLine("\nFolder Analysis:");
            Console.WriteLine("================");

            var folders = Directory.GetDirectories(rootFolder);
            long totalSize = 0;

            foreach (var folder in folders)
            {
                var imageFiles = GetImageFiles(folder);
                long folderSize = imageFiles.Sum(file => new FileInfo(file).Length);
                totalSize += folderSize;

                Console.WriteLine($"{Path.GetFileName(folder)}: {imageFiles.Count} images, {FormatBytes(folderSize)}");
            }

            Console.WriteLine($"\nTotal: {FormatBytes(totalSize)}");
        }

        static void ResizeSelectedFolders(string rootFolder)
        {
            var folders = Directory.GetDirectories(rootFolder);

            Console.WriteLine("\nAvailable folders:");
            for (int i = 0; i < folders.Length; i++)
            {
                Console.WriteLine($"{i + 1}) {Path.GetFileName(folders[i])}");
            }

            Console.Write("\nEnter folder numbers to resize (comma-separated, e.g., 1,3,5): ");
            string input = Console.ReadLine();

            var selectedIndices = ParseFolderSelection(input, folders.Length);
            if (selectedIndices.Count == 0)
            {
                Console.WriteLine("No valid folders selected.");
                return;
            }

            foreach (int index in selectedIndices)
            {
                string folder = folders[index - 1];
                ResizeImagesInFolder(folder, 3000);
            }
        }

        static void CreateThumbnailsAndMediumImages(string rootFolder)
        {
            var folders = Directory.GetDirectories(rootFolder);

            foreach (var folder in folders)
            {
                Console.WriteLine($"\nProcessing folder: {Path.GetFileName(folder)}");

                // Create subdirectories for thumbnails and medium images
                string thumbDir = Path.Combine(folder, "thumbnails");
                string mediumDir = Path.Combine(folder, "medium");
                Directory.CreateDirectory(thumbDir);
                Directory.CreateDirectory(mediumDir);

                var imageFiles = GetImageFiles(folder);
                int processed = 0;

                foreach (var imageFile in imageFiles)
                {
                    try
                    {
                        string fileName = Path.GetFileNameWithoutExtension(imageFile);
                        string extension = Path.GetExtension(imageFile);

                        string thumbPath = Path.Combine(thumbDir, $"{fileName}_thumb{extension}");
                        string mediumPath = Path.Combine(mediumDir, $"{fileName}_medium{extension}");

                        // Create thumbnail (250px max)
                        CreateResizedImage(imageFile, thumbPath, 350);

                        // Create medium image (1000px max)
                        CreateResizedImage(imageFile, mediumPath, 1500);

                        processed++;
                        if (processed % 10 == 0 || processed == imageFiles.Count)
                        {
                            Console.Write($"\rProcessed {processed}/{imageFiles.Count} images");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\nError processing {imageFile}: {ex.Message}");
                    }
                }
                Console.WriteLine(); // New line after progress
            }

            Console.WriteLine("Thumbnail and medium image creation completed!");
        }

        static void ResizeImagesInFolder(string folder, int maxSize)
        {
            Console.WriteLine($"\nResizing images in: {Path.GetFileName(folder)}");

            var imageFiles = GetImageFiles(folder);
            int processed = 0;

            foreach (var imageFile in imageFiles)
            {
                try
                {
                    ResizeImageInPlace(imageFile, maxSize);
                    processed++;

                    if (processed % 10 == 0 || processed == imageFiles.Count)
                    {
                        Console.Write($"\rResized {processed}/{imageFiles.Count} images");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nError resizing {imageFile}: {ex.Message}");
                }
            }
            Console.WriteLine(); // New line after progress
        }

        static void ResizeImageInPlace(string imagePath, int maxSize)
        {
            var encoder = new JpegEncoder { Quality = 90 };

            using var image = Image.Load(imagePath);

            if (image.Width <= maxSize && image.Height <= maxSize)
                return; // No resize needed

            var (newWidth, newHeight) = CalculateNewDimensions(image.Width, image.Height, maxSize);

            image.Mutate(x => x.Resize(newWidth, newHeight));
            image.Save(imagePath, encoder);
        }

        static void CreateResizedImage(string sourcePath, string destinationPath, int maxSize)
        {
            var encoder = new JpegEncoder { Quality = 90 };

            using var image = Image.Load(sourcePath);
            var (newWidth, newHeight) = CalculateNewDimensions(image.Width, image.Height, maxSize);

            image.Mutate(x => x.Resize(newWidth, newHeight));
            image.Save(destinationPath, encoder);
        }

        static (int width, int height) CalculateNewDimensions(int originalWidth, int originalHeight, int maxSize)
        {
            if (originalWidth <= maxSize && originalHeight <= maxSize)
                return (originalWidth, originalHeight);

            double ratio = Math.Min((double)maxSize / originalWidth, (double)maxSize / originalHeight);
            return ((int)(originalWidth * ratio), (int)(originalHeight * ratio));
        }

        static List<string> GetImageFiles(string folderPath)
        {
            return Directory.GetFiles(folderPath)
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();
        }

        static List<int> ParseFolderSelection(string input, int maxFolders)
        {
            var selectedIndices = new List<int>();

            if (string.IsNullOrWhiteSpace(input))
                return selectedIndices;

            var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                if (int.TryParse(part.Trim(), out int index) && index >= 1 && index <= maxFolders)
                {
                    selectedIndices.Add(index);
                }
            }

            return selectedIndices.Distinct().OrderBy(x => x).ToList();
        }

        static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (bytes >= GB)
                return $"{bytes / (double)GB:F2} GB";
            if (bytes >= MB)
                return $"{bytes / (double)MB:F2} MB";
            if (bytes >= KB)
                return $"{bytes / (double)KB:F2} KB";

            return $"{bytes} bytes";
        }
    }
}