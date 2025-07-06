namespace VideoLibrary.Models
{
    public class LogEntry
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public DbLogLevel LogLevel { get; set; }
        public string StackTrace { get; set; }
        public DateTime Timestamp { get; set; }
        public string LogLevelString { get { return LogLevel.ToString(); } }
    }

    public enum DbLogLevel
    {
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }

}
