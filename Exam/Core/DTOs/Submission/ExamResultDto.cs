namespace Exam.Core.DTOs.Submission
{
    public class ExamResultDto
    {
        public int StudentExamId { get; set; }
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public decimal ScorePercentage { get; set; }
        public string Status { get; set; } = string.Empty; // Passed / Failed
        public DateTime SubmittedAt { get; set; }
    }
}
