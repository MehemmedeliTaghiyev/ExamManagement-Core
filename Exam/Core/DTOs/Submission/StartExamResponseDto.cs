namespace Exam.Core.DTOs.Submission
{
    public class StartExamResponseDto
    {
        public int StudentExamId { get; set; }
        public int ExamId { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool AlreadySubmitted { get; set; }
    }
}
