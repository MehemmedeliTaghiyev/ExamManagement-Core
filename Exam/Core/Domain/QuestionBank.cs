using Exam.Core.Enums;

namespace Exam.Core.Domain
{
    public class QuestionBank
    {
        public int Id { get; set; }
        public string Qnum { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public QuestionType Type { get; set; } = QuestionType.SingleChoice;
        public string InputKind { get; set; } = "Choice";
        public string? CorrectText { get; set; }
        public string? DifficultyLevel { get; set; }
        public int Points { get; set; } = 1;
        public string? Grade { get; set; }
        public string? Subject { get; set; }
        public string? Audience { get; set; }
        public string? MainTopic { get; set; }
        public string? Language { get; set; }
        public string? SituationId { get; set; }
        public string? SituationTitle { get; set; }
        public string? FigureUrl { get; set; }
        /// <summary>Distinct exams that copied this Qnum (SourceQnum). Unused rows are preferred.</summary>
        public int UseCount { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }
}
