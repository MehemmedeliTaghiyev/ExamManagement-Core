namespace Exam.Core.DTOs.Exam
{
    public class AnswerKeyItemDto
    {
        public int QuestionId { get; set; }
        public string CorrectLetter { get; set; } = "A";
    }

    public class AnswerKeyDto
    {
        public List<AnswerKeyItemDto> Answers { get; set; } = new();
    }
}
