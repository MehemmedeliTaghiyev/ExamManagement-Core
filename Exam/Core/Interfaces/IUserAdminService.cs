using Exam.Core.DTOs.User;
using Exam.Core.Enums;

namespace Exam.Core.Interfaces
{
    public interface IUserAdminService
    {
        Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(UserRole role, bool includeDeleted);
        Task<AdminUserDto?> SetAccessAsync(int userId, bool enabled, int actorId, UserRole actorRole);
        Task<AdminUserDto?> SoftDeleteAsync(int userId, int actorId, UserRole actorRole);
        Task<AdminUserDto?> RestoreAsync(int userId, int actorId, UserRole actorRole);
        Task<bool> IsAccessAllowedAsync(int userId);
    }
}
