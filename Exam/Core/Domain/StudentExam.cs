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
    }
}
