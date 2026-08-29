using Exam.Core.Enums;

namespace Exam.Core.Domain
{
    public class Exam
    {
        public int Id { get; set; }
        public int SubjectId { get; set; } // Foreign Key to Subject
        public string Title { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public ExamStatus Status { get; set; }
        public int SubmissionsCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? PdfFilePath {  get; set; }   
    }
}
