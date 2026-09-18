using Exam.Core.Domain;
using Exam.Core.DTOs.Auth;
using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using static Exam.Core.DTOs.Auth.AuthDTOs;

namespace Exam.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly ExamDbContext _context;
        private readonly IConfiguration _config;
        private readonly IAdminNotificationService _notify;
        public AuthService(ExamDbContext context, IConfiguration config, IAdminNotificationService notify)
        {
            _context = context;
            _config = config;
            _notify = notify;
        }
        public async Task<AuthDTOs.AuthResponseDto?> LoginAsync(AuthDTOs.LoginRequestDto dto)
        {
            var login = (dto.Email ?? string.Empty).Trim();
            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u =>
                u.Email == login || (u.UserName != null && u.UserName == login));
            if (user == null || user.IsDeleted || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return null;

            if (user.Role != UserRole.Admin && !user.IsAccessEnabled)
            {
                throw new InvalidOperationException("ACCESS_CLOSED|" + (string.IsNullOrWhiteSpace(user.TrialMessage)
                    ? "Hesabınız bağlanıb. Giriş üçün adminə müraciət edin."
                    : user.TrialMessage));
            }

            if (user.Role == UserRole.Teacher
                && user.TrialEndsAt.HasValue
                && user.TrialEndsAt.Value <= DateTime.UtcNow)
            {
                if (user.IsAccessEnabled)
                {
                    user.IsAccessEnabled = false;
                    await _context.SaveChangesAsync();
                }

                var msg = string.IsNullOrWhiteSpace(user.TrialMessage)
                    ? "14 günlük sınaq bitib. Davam etmək üçün adminə müraciət edin."
                    : user.TrialMessage;
                throw new InvalidOperationException("ACCESS_CLOSED|" + msg);
            }

            var token = GenerateJwtToken(user);
            return new AuthResponseDto(user.Id, user.FullName, user.Email, user.Role.ToString(), token, user.TeacherId, user.UserName);
        }

        public async Task<AuthDTOs.AuthResponseDto?> RegisterAsync(AuthDTOs.RegisterRequestDto dto)
        {
            if (dto.Role != UserRole.Teacher)
            {
                throw new InvalidOperationException("STUDENT_VIA_TEACHER");
            }

            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return null;

            var parts = (dto.FullName ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var user = new User
            {
                FullName = dto.FullName,
                FirstName = parts.Length > 0 ? parts[0] : dto.FullName,
                LastName = parts.Length > 1 ? parts[1] : null,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = UserRole.Teacher,
                IsAccessEnabled = true,
                TrialEndsAt = DateTime.UtcNow.AddDays(14),
                TrialMessage = "Free trial bitdi."
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            try { await _notify.NotifyTeacherRegisteredAsync(user); }
            catch { /* ignore mail errors */ }

            var token = GenerateJwtToken(user);
            return new AuthResponseDto(user.Id, user.FullName, user.Email, user.Role.ToString(), token, user.Id, user.UserName);
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var teacherScope = user.Role == UserRole.Teacher
                ? user.Id
                : user.Role == UserRole.Student ? user.TeacherId : null;

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };
            if (teacherScope.HasValue && teacherScope.Value > 0)
            {
                claims.Add(new Claim("teacherScope", teacherScope.Value.ToString()));
            }

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpiryInMinutes"] ?? "120")),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
