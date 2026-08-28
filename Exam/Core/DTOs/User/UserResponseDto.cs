namespace Exam.Core.DTOs.User
{
    public record UserResponseDto(
        int Id,
        string FullName,
        string Email,
        string Role
    );
}
