namespace Exam.Core.Domain
{
    public class StudentAnswer
    {
        public int Id { get; set; }
        public int StudentExamId { get; set; }   // Foreign Key to StudentExam
        public int QuestionId { get; set; }      // Foreign Key to Question
        public int SelectedOptionId { get; set; } // Foreign Key to QuestionOption
        public string? TextAnswer { get; set; }     // Açıq suallar üçün tələbənin yazdığı mətn
    }
}
