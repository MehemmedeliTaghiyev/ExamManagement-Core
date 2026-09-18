using Exam.Core.DTOs.User;
using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Exam.Infrastructure.Services
{
    public class UserAdminService : IUserAdminService
    {
        private readonly ExamDbContext _context;
        private readonly IAdminNotificationService _notify;

        public UserAdminService(ExamDbContext context, IAdminNotificationService notify)
        {
            _context = context;
            _notify = notify;
        }

        public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(UserRole role, bool includeDeleted, int actorId, UserRole actorRole)
        {
            var query = _context.Users.IgnoreQueryFilters().Where(u => u.Role == role);
            if (!includeDeleted)
            {
                query = query.Where(u => !u.IsDeleted);
            }

            if (actorRole == UserRole.Teacher)
            {
                query = query.Where(u => u.TeacherId == actorId);
            }

            var rows = await query
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return rows.Select(Map).ToList();
        }

        public async Task<AdminUserDto?> CreateStudentAsync(CreateStudentDto dto, int teacherId)
        {
            var email = (dto.Email ?? string.Empty).Trim();
            var userName = (dto.UserName ?? string.Empty).Trim();
            var fullName = (dto.FullName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return null;
            }

            var exists = await _context.Users.IgnoreQueryFilters().AnyAsync(u =>
                u.Email == email || (u.UserName != null && u.UserName == userName));
            if (exists) return null;

            var user = new Core.Domain.User
            {
                FullName = fullName,
                Email = email,
                UserName = userName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = UserRole.Student,
                TeacherId = teacherId,
                GroupName = string.IsNullOrWhiteSpace(dto.GroupName) ? null : dto.GroupName.Trim(),
                IsAccessEnabled = !dto.CloseAccess
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Map(user);
        }

        private static DateTime ToUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        public async Task<AdminUserDto?> CreateTeacherAsync(CreateTeacherDto dto)
        {
            var first = (dto.FirstName ?? string.Empty).Trim();
            var last = (dto.LastName ?? string.Empty).Trim();
            var email = (dto.Email ?? string.Empty).Trim();
            var phone = (dto.Phone ?? string.Empty).Trim();
            var userName = (dto.UserName ?? string.Empty).Trim();
            var fullName = $"{first} {last}".Trim();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return null;
            }

            var exists = await _context.Users.IgnoreQueryFilters().AnyAsync(u =>
                u.Email == email || (!string.IsNullOrWhiteSpace(userName) && u.UserName == userName));
            if (exists) return null;

            var trialEnd = dto.TrialEndsAt.HasValue ? ToUtc(dto.TrialEndsAt.Value) : DateTime.UtcNow.AddDays(14);
            var user = new Core.Domain.User
            {
                FirstName = first,
                LastName = last,
                FullName = fullName,
                Email = email,
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                UserName = string.IsNullOrWhiteSpace(userName) ? null : userName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = UserRole.Teacher,
                IsAccessEnabled = true,
                TrialEndsAt = trialEnd,
                TrialMessage = string.IsNullOrWhiteSpace(dto.TrialMessage)
                    ? "Free trial bitdi."
                    : dto.TrialMessage.Trim()
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            try { await _notify.NotifyTeacherRegisteredAsync(user); }
            catch { /* registration should not fail if mail fails */ }
            return Map(user);
        }

        public async Task<AdminUserDto?> UpdateTeacherTrialAsync(int teacherId, UpdateTeacherTrialDto dto)
        {
            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == teacherId && u.Role == UserRole.Teacher);
            if (user == null) return null;

            if (!string.IsNullOrWhiteSpace(dto.FirstName)) user.FirstName = dto.FirstName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.LastName)) user.LastName = dto.LastName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Phone)) user.Phone = dto.Phone.Trim();
            if (!string.IsNullOrWhiteSpace(user.FirstName) || !string.IsNullOrWhiteSpace(user.LastName))
            {
                user.FullName = $"{user.FirstName} {user.LastName}".Trim();
            }
            if (dto.TrialMessage != null) user.TrialMessage = dto.TrialMessage.Trim();
            if (dto.TrialEndsAt.HasValue)
            {
                user.TrialEndsAt = ToUtc(dto.TrialEndsAt.Value);
                user.TrialNotifiedAt = null;
            }
            await _context.SaveChangesAsync();
            return Map(user);
        }

        private static bool CanManage(UserRole actorRole, Core.Domain.User target, int actorId)
        {
            if (target.Role == UserRole.Admin) return false;
            if (actorRole == UserRole.Admin) return target.Role is UserRole.Teacher or UserRole.Student;
            return actorRole == UserRole.Teacher && target.Role == UserRole.Student && target.TeacherId == actorId;
        }

        public async Task<AdminUserDto?> SetAccessAsync(int userId, bool enabled, int actorId, UserRole actorRole)
        {
            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || user.Id == actorId) return null;
            if (!CanManage(actorRole, user, actorId)) return null;

            user.IsAccessEnabled = enabled;
            await _context.SaveChangesAsync();
            return Map(user);
        }

        public async Task<AdminUserDto?> SoftDeleteAsync(int userId, int actorId, UserRole actorRole)
        {
            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || user.Id == actorId) return null;
            if (!CanManage(actorRole, user, actorId)) return null;

            user.IsDeleted = true;
            user.IsAccessEnabled = false;
            user.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Map(user);
        }

        public async Task<AdminUserDto?> RestoreAsync(int userId, int actorId, UserRole actorRole)
        {
            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || user.Id == actorId) return null;
            if (!CanManage(actorRole, user, actorId)) return null;

            user.IsDeleted = false;
            user.DeletedAt = null;
            user.IsAccessEnabled = true;
            await _context.SaveChangesAsync();
            return Map(user);
        }

        public async Task<bool> IsAccessAllowedAsync(int userId)
        {
            var user = await _context.Users.IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || user.IsDeleted) return false;
            if (user.Role == UserRole.Admin) return true;
            if (user.Role == UserRole.Teacher
                && user.TrialEndsAt.HasValue
                && user.TrialEndsAt.Value <= DateTime.UtcNow)
            {
                return false;
            }
            return user.IsAccessEnabled;
        }

        private static AdminUserDto Map(Core.Domain.User u) => new()
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            Role = u.Role.ToString(),
            IsAccessEnabled = u.IsAccessEnabled,
            IsDeleted = u.IsDeleted,
            CreatedAt = u.CreatedAt,
            DeletedAt = u.DeletedAt,
            TeacherId = u.TeacherId,
            UserName = u.UserName,
            GroupName = u.GroupName,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Phone = u.Phone,
            TrialEndsAt = u.TrialEndsAt,
            TrialMessage = u.TrialMessage,
            TrialNotifiedAt = u.TrialNotifiedAt
        };
    }
}
