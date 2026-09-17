using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Exam.Core.DTOs.User;
using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exam.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Teacher")]
    public class UsersController : ControllerBase
    {
        private readonly IUserAdminService _users;

        public UsersController(IUserAdminService users)
        {
            _users = users;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string role = "Student", [FromQuery] bool includeDeleted = true)
        {
            var actorRole = GetActorRole();
            if (!Enum.TryParse<UserRole>(role, true, out var parsed) || parsed == UserRole.Admin)
            {
                return BadRequest(new { message = "Rol Teacher və ya Student olmalıdır." });
            }

            if (actorRole == UserRole.Teacher)
            {
                parsed = UserRole.Student;
            }

            var list = await _users.GetUsersAsync(parsed, includeDeleted);
            return Ok(list);
        }

        [HttpPatch("{id:int}/access")]
        public async Task<IActionResult> SetAccess(int id, [FromBody] SetAccessDto dto)
        {
            var actorId = GetUserId() ?? 0;
            var updated = await _users.SetAccessAsync(id, dto.Enabled, actorId, GetActorRole());
            if (updated == null) return BadRequest(new { message = "İstifadəçi tapılmadı və ya bu əməliyyat icazəli deyil." });
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var actorId = GetUserId() ?? 0;
            var updated = await _users.SoftDeleteAsync(id, actorId, GetActorRole());
            if (updated == null) return BadRequest(new { message = "İstifadəçi tapılmadı və ya silinə bilməz." });
            return Ok(updated);
        }

        [HttpPost("{id:int}/restore")]
        public async Task<IActionResult> Restore(int id)
        {
            var actorId = GetUserId() ?? 0;
            var updated = await _users.RestoreAsync(id, actorId, GetActorRole());
            if (updated == null) return BadRequest(new { message = "İstifadəçi tapılmadı." });
            return Ok(updated);
        }

        private UserRole GetActorRole() => User.IsInRole("Admin") ? UserRole.Admin : UserRole.Teacher;

        private int? GetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}
