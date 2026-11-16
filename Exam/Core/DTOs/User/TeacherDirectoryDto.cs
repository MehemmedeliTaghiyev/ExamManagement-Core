namespace Exam.Core.DTOs.User
{
    /// <summary>
    /// Public teacher row for student «Müəllim tap». No emails, phones, passwords, or tokens.
    /// </summary>
    public class TeacherDirectoryDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? TeachingSubject { get; set; }
        public string? Subject { get; set; }
        public string? Position { get; set; }
        public string? Region { get; set; }
    }
}
