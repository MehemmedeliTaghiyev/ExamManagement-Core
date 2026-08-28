using Exam.Core.Enums;

namespace Exam.Core.Domain
{
    public class Question
    {
        public int Id { get; set; }
        public int ExamId { get; set; } // Foreign Key to Exam
        public string Text { get; set; } = string.Empty;
        public int Points { get; set; } = 1;
        public QuestionType Type { get; set; } = QuestionType.SingleChoice;
    }
}
