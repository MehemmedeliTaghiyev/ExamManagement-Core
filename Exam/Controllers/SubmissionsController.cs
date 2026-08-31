using Exam.Core.DTOs.Submission;
using Exam.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Exam.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Student")] // Secures the entire controller for Students
    public class SubmissionsController : ControllerBase
    {
        private readonly ISubmissionService _submissionService;

        public SubmissionsController(ISubmissionService submissionService)
        {
            _submissionService = submissionService;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartExam([FromBody] StartExamDto dto)
        {
            var studentExamId = await _submissionService.StartExamAsync(dto);
            return Ok(new { studentExamId, message = "İmtahan uğurla başladıldı." });
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitExam([FromBody] SubmitExamDto dto)
        {
            var result = await _submissionService.SubmitExamAsync(dto);

            if (result == null)
            {
                return BadRequest(new { message = "İmtahan tapılmadı və ya artıq təhvil verilib." });
            }

            return Ok(result);
        }

        [HttpGet("result/{studentExamId}")]
        public async Task<IActionResult> GetResult(int studentExamId)
        {
            var result = await _submissionService.GetStudentResultAsync(studentExamId);

            if (result == null)
            {
                return NotFound(new { message = "Nəticə tapılmadı." });
            }

            return Ok(result);
        }

        [HttpGet("history/{studentId}")]
        public async Task<IActionResult> GetStudentHistory(int studentId)
        {
            var history = await _submissionService.GetStudentHistoryAsync(studentId);
            return Ok(history);
        }
    }
}
