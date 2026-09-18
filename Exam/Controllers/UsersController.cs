using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Exam.Core.DTOs.User;
using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Exam.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exam.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
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
            if (!RoleClaims.IsAdmin(User) && !RoleClaims.IsTeacher(User)) return Forbid();
            var actorRole = GetActorRole();
            if (!Enum.TryParse<UserRole>(role, true, out var parsed) || parsed == UserRole.Admin)
            {
                return BadRequest(new { message = "Rol Teacher və ya Student olmalıdır." });
            }

            if (actorRole == UserRole.Teacher)
            {
                parsed = UserRole.Student;
            }

            var list = await _users.GetUsersAsync(parsed, includeDeleted, GetUserId() ?? 0, actorRole);
            return Ok(list);
        }

        [HttpPost]
        [HttpPost("students")]
        [HttpPost("create-student")]
        public async Task<IActionResult> CreateStudent([FromBody] CreateStudentDto dto)
        {
            if (!RoleClaims.IsAdmin(User) && !RoleClaims.IsTeacher(User)) return Forbid();
            var actorId = GetUserId() ?? 0;
            if (actorId <= 0) return Unauthorized();
            var created = await _users.CreateStudentAsync(dto, actorId);
            if (created == null)
            {
                return BadRequest(new { message = "Tələbə yaradılmadı. E-poçt və ya istifadəçi adı artıq mövcuddur, və ya məlumatlar boşdur." });
            }
            return Ok(created);
        }

        [HttpPost("teachers")]
        [HttpPost("create-teacher")]
        public async Task<IActionResult> CreateTeacher([FromBody] CreateTeacherDto dto)
        {
            if (!RoleClaims.IsAdmin(User)) return Forbid();
            try
            {
                var created = await _users.CreateTeacherAsync(dto);
                if (created == null)
                {
                    return BadRequest(new { message = "Müəllim yaradılmadı. E-poçt və ya istifadəçi adı artıq mövcuddur, və ya ad, soyad, e-poçt, şifrə boşdur." });
                }
                return Ok(created);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Müəllim bazaya yazılmadı. API-ni yenidən işə salın.", detail = ex.InnerException?.Message ?? ex.Message });
            }
        }

        [HttpPatch("{id:int}/trial")]
        public async Task<IActionResult> UpdateTrial(int id, [FromBody] UpdateTeacherTrialDto dto)
        {
            if (!RoleClaims.IsAdmin(User)) return Forbid();
            var updated = await _users.UpdateTeacherTrialAsync(id, dto);
            if (updated == null) return NotFound(new { message = "Müəllim tapılmadı." });
            return Ok(updated);
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

        private UserRole GetActorRole() => RoleClaims.IsAdmin(User) ? UserRole.Admin : UserRole.Teacher;

        private int? GetUserId() => RoleClaims.UserId(User);
    }
}
