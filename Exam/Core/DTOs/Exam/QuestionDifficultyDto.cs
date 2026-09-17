namespace Exam.Core.DTOs.Exam
{
    public class QuestionDifficultyDto
    {
        public int QuestionId { get; set; }
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Participants { get; set; }
        public int WrongCount { get; set; }
        public int Difficulty { get; set; }
    }
}
