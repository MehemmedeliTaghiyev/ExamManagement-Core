namespace Exam.Core.DTOs.Submission
{
    public class SubmitExamDto
    {
        public int StudentExamId { get; set; }
        public int ExamId { get; set; }
        public int StudentId { get; set; }
        public List<StudentAnswerDto> Answers { get; set; } = new();
    }
}
