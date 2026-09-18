using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Exam.Infrastructure
{
    public static class RoleClaims
    {
        public static bool IsAdmin(ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true) return false;
            if (user.IsInRole("Admin") || user.IsInRole("0")) return true;
            return user.Claims.Any(c =>
                IsRoleType(c.Type) && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsTeacher(ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true) return false;
            if (user.IsInRole("Teacher") || user.IsInRole("1")) return true;
            return user.Claims.Any(c =>
                IsRoleType(c.Type) && c.Value.Equals("Teacher", StringComparison.OrdinalIgnoreCase));
        }

        public static int? UserId(ClaimsPrincipal? user)
        {
            var raw = user?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? user?.FindFirstValue("sub");
            return int.TryParse(raw, out var id) && id > 0 ? id : null;
        }

        private static bool IsRoleType(string type) =>
            type == ClaimTypes.Role
            || type.Equals("role", StringComparison.OrdinalIgnoreCase)
            || type.EndsWith("/role", StringComparison.OrdinalIgnoreCase);
    }
}
