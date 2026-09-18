using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Exam.Core.DTOs.Submission;
using Exam.Core.Interfaces;
using Exam.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exam.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SubmissionsController : ControllerBase
    {
        private readonly ISubmissionService _submissionService;

        public SubmissionsController(ISubmissionService submissionService)
        {
            _submissionService = submissionService;
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> StartExam([FromBody] StartExamDto dto)
        {
            dto.StudentId = GetUserId() ?? dto.StudentId;
            try
            {
                var result = await _submissionService.StartExamAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("progress")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> SaveProgress([FromBody] SubmitExamDto dto)
        {
            dto.StudentId = GetUserId() ?? dto.StudentId;
            await _submissionService.SaveProgressAsync(dto);
            return Ok();
        }

        [HttpPost("submit")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> SubmitExam([FromBody] SubmitExamDto dto)
        {
            dto.StudentId = GetUserId() ?? dto.StudentId;
            var result = await _submissionService.SubmitExamAsync(dto);

            if (result == null)
            {
                return BadRequest(new { message = "İmtahan tapılmadı və ya artıq təhvil verilib." });
            }

            return Ok(result);
        }

        [HttpGet("result/{studentExamId:int}")]
        public async Task<IActionResult> GetResult(int studentExamId)
        {
            var result = await _submissionService.GetStudentResultAsync(studentExamId);
            if (result == null) return NotFound(new { message = "Nəticə tapılmadı." });

            if (!IsStaff() && result.StudentId != GetUserId())
            {
                return Forbid();
            }

            return Ok(result);
        }

        [HttpGet("review/{studentExamId:int}")]
        public async Task<IActionResult> GetReview(int studentExamId)
        {
            var userId = GetUserId() ?? 0;
            var review = await _submissionService.GetExamReviewAsync(studentExamId, userId, IsStaff());
            if (review == null) return NotFound(new { message = "Nəticə tapılmadı." });
            return Ok(review);
        }

        [HttpGet("exam/{examId:int}")]
        public async Task<IActionResult> GetExamSubmissions(int examId)
        {
            if (!IsStaff())
            {
                var review = await _submissionService.GetExamReviewByExamAsync(examId, GetUserId() ?? 0, false);
                if (review == null) return NotFound();
                if (!review.ExamEnded)
                {
                    return Ok(Array.Empty<ExamResultDto>());
                }
                return Ok(review.Leaderboard);
            }

            var list = await _submissionService.GetExamSubmissionsAsync(examId);
            return Ok(list);
        }

        [HttpGet("exam/{examId:int}/review")]
        public async Task<IActionResult> GetExamReview(int examId)
        {
            var review = await _submissionService.GetExamReviewByExamAsync(examId, GetUserId() ?? 0, IsStaff());
            if (review == null) return NotFound(new { message = "Nəticə tapılmadı." });
            return Ok(review);
        }

        [HttpGet("history/me")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyHistory()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var history = await _submissionService.GetStudentHistoryAsync(userId.Value);
            return Ok(history);
        }

        [HttpGet("history/{studentId:int}")]
        public async Task<IActionResult> GetStudentHistory(int studentId)
        {
            var userId = GetUserId();
            if (!IsStaff() && userId != studentId)
            {
                studentId = userId ?? studentId;
            }

            var history = await _submissionService.GetStudentHistoryAsync(studentId);
            return Ok(history);
        }

        private bool IsStaff() => RoleClaims.IsAdmin(User) || RoleClaims.IsTeacher(User);

        private int? GetUserId()
        {
            var candidates = new[]
            {
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindFirstValue(JwtRegisteredClaimNames.Sub),
                User.FindFirstValue("sub"),
                User.FindFirstValue("nameid"),
                User.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value
            };

            foreach (var raw in candidates)
            {
                if (int.TryParse(raw, out var id) && id > 0) return id;
            }

            return null;
        }
    }
}
