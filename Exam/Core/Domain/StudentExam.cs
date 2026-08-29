using Exam.Core.Enums;

namespace Exam.Core.Domain
{
    public class StudentExam
    {
        public int Id { get; set; }
        public int StudentId { get; set; } // Foreign Key to User
        public int ExamId { get; set; }    // Foreign Key to Exam
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedAt { get; set; }
        public StudentExamStatus Status { get; set; } = StudentExamStatus.InProgress;
        public int Score { get; set; } = 0;
        public int CorrectAnswersCount { get; set; } = 0; // Düzgün cavabların sayı
        public int WrongAnswersCount { get; set; } = 0;   // Səhv cavabların sayı
        public int UnansweredCount { get; set; } = 0;     // Yazılmayan (boş saxlanılan) sualların sayı
        public decimal FinalScore { get; set; } = 0;      // Yekun hesablama balı (məsələn: cərimələr çıxıldıqdan sonra)
    }
}
