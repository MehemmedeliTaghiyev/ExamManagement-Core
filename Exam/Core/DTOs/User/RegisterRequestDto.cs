namespace Exam.Core.DTOs.User
{
    public record RegisterRequestDto
    (
        string FullName,
        string Email,
        string Password,
        string Role // "Teacher" or "Student"
    );
}
