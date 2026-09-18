namespace Exam.Core.DTOs.Exam
{
    public class ExamCardDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int TotalQuestions { get; set; }
        public int DurationMinutes { get; set; }
        public int SubmissionsCount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = string.Empty; // "Live", "Scheduled", "Finished"
        public string? PdfFilePath { get; set; }
        public string? PdfFileUrl { get; set; }
        public int? TeacherId { get; set; }
        public string? TeacherName { get; set; }
    }
}
