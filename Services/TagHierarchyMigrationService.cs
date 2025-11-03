using Microsoft.EntityFrameworkCore;
using VideoLibrary.Models;

namespace VideoLibrary.Services
{
    public class TagHierarchyMigrationService
    {
        private readonly AppDbContext _context;

        public TagHierarchyMigrationService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Initialize flat tag structure with nested set values
        /// Call this once after adding the new columns
        /// </summary>
        public async Task InitializeFlatStructureAsync()
        {
            var tags = await _context.Tags.ToListAsync();
            int counter = 0;

            foreach (var tag in tags)
            {
                tag.Left = ++counter;
                tag.Right = ++counter;
                tag.Level = 0;
                tag.ParentId = null;
            }

            await _context.SaveChangesAsync();
        }
    }
}
