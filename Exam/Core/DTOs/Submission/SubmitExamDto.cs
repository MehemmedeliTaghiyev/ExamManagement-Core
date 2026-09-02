namespace Exam.Core.DTOs.Submission
{
    public class SubmitExamDto
    {
        public int StudentExamId { get; set; }

        // Key: QuestionId, Value: SelectedOptionId
        public Dictionary<int, int> Answers { get; set; } = new();
    }
}
