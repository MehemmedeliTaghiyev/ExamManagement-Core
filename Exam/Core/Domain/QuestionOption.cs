namespace Exam.Core.Domain
{
    public class QuestionOption
    {
        public int Id { get; set; }
        public int QuestionId { get; set; } // Foreign Key to Question
        public string OptionText { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
