using Exam.Core.Enums;

namespace Exam.Core.DTOs.Exam
{
    public class QuestionResponseDto
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Points { get; set; }
        public QuestionType Type { get; set; }
        public List<QuestionOptionResponseDto> Options { get; set; } = new();
    }
}
