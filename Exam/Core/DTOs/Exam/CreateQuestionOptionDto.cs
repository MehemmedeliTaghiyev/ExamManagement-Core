namespace Exam.Core.DTOs.Exam
{
    public class CreateQuestionOptionDto
    {
        public string OptionText { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
