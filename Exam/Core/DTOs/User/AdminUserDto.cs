namespace Exam.Core.DTOs.User
{
    public class AdminUserDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsAccessEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int? TeacherId { get; set; }
        public string? UserName { get; set; }
        public string? GroupName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public DateTime? TrialEndsAt { get; set; }
        public string? TrialMessage { get; set; }
        public DateTime? TrialNotifiedAt { get; set; }
    }

    public class SetAccessDto
    {
        public bool Enabled { get; set; }
    }

    public class CreateStudentDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? GroupName { get; set; }
        public bool CloseAccess { get; set; }
    }

    public class CreateTeacherDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public DateTime? TrialEndsAt { get; set; }
        public string? TrialMessage { get; set; }
    }

    public class UpdateTeacherTrialDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public DateTime? TrialEndsAt { get; set; }
        public string? TrialMessage { get; set; }
    }
}
