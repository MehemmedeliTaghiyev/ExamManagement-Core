using Exam.Core.Enums;

namespace Exam.Core.DTOs.Exam
{
    public class CreateQuestionDto
    {
        public string Text { get; set; } = string.Empty;
        public int Points { get; set; } = 1;
        public QuestionType Type { get; set; } = QuestionType.SingleChoice;
        public string? InputKind { get; set; }
        public string? CorrectText { get; set; }
        public List<CreateQuestionOptionDto> Options { get; set; } = new();
    }
}
