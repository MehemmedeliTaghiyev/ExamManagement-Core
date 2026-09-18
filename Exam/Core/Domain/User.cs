using Exam.Core.Enums;

namespace Exam.Core.Domain
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Student;
        public int? TeacherId { get; set; }
        public string? UserName { get; set; }
        public string? GroupName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public DateTime? TrialEndsAt { get; set; }
        public string? TrialMessage { get; set; }
        public DateTime? TrialNotifiedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsAccessEnabled { get; set; } = true;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
