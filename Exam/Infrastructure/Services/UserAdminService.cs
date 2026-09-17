using Exam.Core.DTOs.User;
using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Exam.Infrastructure.Services
{
    public class UserAdminService : IUserAdminService
    {
        private readonly ExamDbContext _context;

        public UserAdminService(ExamDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(UserRole role, bool includeDeleted)
        {
            var query = _context.Users.IgnoreQueryFilters().Where(u => u.Role == role);
            if (!includeDeleted)
            {
                query = query.Where(u => !u.IsDeleted);
            }

            var rows = await query
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return rows.Select(Map).ToList();
        }

        private static bool CanManage(UserRole actorRole, Core.Domain.User target)
        {
            if (target.Role == UserRole.Admin) return false;
            if (actorRole == UserRole.Admin) return target.Role is UserRole.Teacher or UserRole.Student;
            return actorRole == UserRole.Teacher && target.Role == UserRole.Student;
        }

        public async Task<AdminUserDto?> SetAccessAsync(int userId, bool enabled, int actorId, UserRole actorRole)
        {
            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || user.Id == actorId) return null;
            if (!CanManage(actorRole, user)) return null;

            user.IsAccessEnabled = enabled;
            await _context.SaveChangesAsync();
            return Map(user);
        }

        public async Task<AdminUserDto?> SoftDeleteAsync(int userId, int actorId, UserRole actorRole)
        {
            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || user.Id == actorId) return null;
            if (!CanManage(actorRole, user)) return null;

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
            if (!CanManage(actorRole, user)) return null;

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
            DeletedAt = u.DeletedAt
        };
    }
}
