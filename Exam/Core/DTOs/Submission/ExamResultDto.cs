namespace Exam.Core.DTOs.Submission
{
    public class ExamResultDto
    {
        public int Id { get; set; }
        public int StudentExamId { get; set; }
        public int ExamId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string ExamTitle { get; set; } = string.Empty;
        public int TotalQuestions { get; set; }
        public int CorrectAnswersCount { get; set; }
        public int WrongAnswersCount { get; set; }
        public int UnansweredCount { get; set; }
        public decimal Score { get; set; }
        public decimal Percent { get; set; }
        public int Rank { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime SubmittedAt { get; set; }
        public int DurationSeconds { get; set; }
        public bool ReviewAvailable { get; set; }
    }
}
