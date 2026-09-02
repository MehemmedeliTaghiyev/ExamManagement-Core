namespace Exam.Core.DTOs.Exam
{
    public class QuestionOptionResponseDto
    {
        public int Id { get; set; }
        public int QuestionId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
