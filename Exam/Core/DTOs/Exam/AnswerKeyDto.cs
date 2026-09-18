namespace Exam.Core.DTOs.Exam
{
    public class AnswerKeyItemDto
    {
        public int QuestionId { get; set; }
        public string CorrectLetter { get; set; } = "A";
        public string? CorrectText { get; set; }
        public string? InputKind { get; set; }
        public string? Type { get; set; }
    }

    public class AnswerKeyDto
    {
        public List<AnswerKeyItemDto> Answers { get; set; } = new();
    }
}
