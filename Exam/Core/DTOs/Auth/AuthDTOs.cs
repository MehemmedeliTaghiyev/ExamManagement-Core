using Exam.Core.Enums;

namespace Exam.Core.DTOs.Auth
{
    public class AuthDTOs
    {
        public record RegisterRequestDto(string FullName, string Email, string Password, UserRole Role = UserRole.Student);

        public record LoginRequestDto(string Email, string Password);

        public record AuthResponseDto(int Id, string FullName, string Email, string Role, string Token);
    }
}
