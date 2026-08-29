namespace Exam.Core.DTOs.Submission
{
    public class SubmitExamDto
    {
        public int StudentExamId { get; set; }
        public Dictionary<int, int> Answers { get; set; } = new(); // Key: QuestionId, Value: SelectedOptionId
    }
}
