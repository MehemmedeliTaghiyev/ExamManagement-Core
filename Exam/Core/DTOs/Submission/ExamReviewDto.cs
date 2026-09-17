namespace Exam.Core.DTOs.Submission
{
    public class QuestionOptionReviewDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public bool IsSelected { get; set; }
    }

    public class QuestionReviewDto
    {
        public int QuestionId { get; set; }
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
        public int? SelectedOptionId { get; set; }
        public string? SelectedText { get; set; }
        public int? CorrectOptionId { get; set; }
        public string? CorrectText { get; set; }
        public bool IsCorrect { get; set; }
        public bool Unanswered { get; set; }
        public List<QuestionOptionReviewDto> Options { get; set; } = new();
    }

    public class ExamReviewDto
    {
        public ExamResultDto Result { get; set; } = new();
        public bool ExamEnded { get; set; }
        public bool ReviewAvailable { get; set; }
        public List<QuestionReviewDto> Questions { get; set; } = new();
        public List<ExamResultDto> Leaderboard { get; set; } = new();
        public string? PdfFilePath { get; set; }
        public string? PdfFileUrl { get; set; }
    }
}
