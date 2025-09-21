namespace VideoLibrary.Models
{
    public class Transcription
    {
        public int Id { get; set; }
        public int VideoId { get; set; }
        public int? ContentId { get; set; }
        public TranscriptionStatus Status { get; set; }
        public DateTime DateRequested { get; set; } = DateTime.Now;
        public DateTime? DateCompleted { get; set; }
    }

    public enum TranscriptionStatus
    {
        Pending = 0,
        Completed = 1,
    }
}
