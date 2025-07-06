using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoLibrary.Models;

namespace VideoLibrary.Services
{
    public class DbLogService
    {
        private readonly AppDbContext _context;

        public DbLogService(AppDbContext context)
        {
            _context = context;
        }

        public async Task Log(string message, DbLogLevel logLevel)
        {
            _context.LogEntries.Add(new LogEntry { 
                LogLevel = logLevel, 
                Message = message,
                StackTrace = string.Empty,
                Timestamp = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }

        public async Task Log(Exception ex, string message, DbLogLevel logLevel)
        {
            _context.LogEntries.Add(new LogEntry
            {
                LogLevel = logLevel,
                Message = message,
                Timestamp = DateTime.Now,
                StackTrace = ex.StackTrace ?? string.Empty
            });

            await _context.SaveChangesAsync();
        }

    }
}
