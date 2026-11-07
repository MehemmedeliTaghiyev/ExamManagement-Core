namespace Exam.Core.Domain
{
    public class QuestionBankOption
    {
        public int Id { get; set; }
        public int QuestionBankId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
