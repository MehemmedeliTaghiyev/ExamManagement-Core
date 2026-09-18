using Exam.Core.DTOs.Exam;
using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Exam.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Exam.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class QuestionsController : ControllerBase
    {
        private readonly IQuestionService _questionService;
        private readonly IExamService _examService;
        private readonly ExamDbContext _context;
        private readonly CurrentTenant _tenant;

        public QuestionsController(IQuestionService questionService, IExamService examService, ExamDbContext context, CurrentTenant tenant)
        {
            _questionService = questionService;
            _examService = examService;
            _context = context;
            _tenant = tenant;
        }

        [HttpGet("exam/{examId}")]
        public async Task<IActionResult> GetByExamId(int examId)
        {
            await _examService.SyncExamStatusesAsync();
            var exam = await _context.Exams.AsNoTracking().FirstOrDefaultAsync(e => e.Id == examId);
            if (!_tenant.CanAccessExam(exam))
            {
                return NotFound(new { message = "İmtahan tapılmadı." });
            }
            var examEnded = exam != null && (exam.Status == ExamStatus.Finished || (exam.EndTime.Year >= 2000 && exam.EndTime <= DateTime.UtcNow));
            var examLive = exam != null && exam.Status == ExamStatus.Live && !examEnded
                && (exam.StartTime.Year < 2000 || exam.StartTime <= DateTime.UtcNow);

            if (User.IsInRole("Student") && !examLive && !examEnded)
            {
                return Ok(Array.Empty<QuestionResponseDto>());
            }

            var questions = await _questionService.GetQuestionsByExamIdAsync(examId);

            if (User.IsInRole("Student") && !examEnded)
            {
                foreach (var question in questions)
                {
                    question.CorrectText = null;
                    foreach (var option in question.Options)
                    {
                        option.IsCorrect = false;
                    }
                }
            }

            return Ok(questions);
        }

        [HttpPost("exam/{examId}")]
        public async Task<IActionResult> Create(int examId, [FromBody] CreateQuestionDto dto)
        {
            if (!RoleClaims.IsAdmin(User) && !RoleClaims.IsTeacher(User)) return Forbid();
            var question = await _questionService.CreateQuestionAsync(examId, dto);
            var list = await _questionService.GetQuestionsByExamIdAsync(examId);
            var created = list.FirstOrDefault(q => q.Id == question.Id) ?? list.LastOrDefault();
            return Ok(created);
        }

        [HttpPut("exam/{examId}/answer-key")]
        public async Task<IActionResult> SetAnswerKey(int examId, [FromBody] AnswerKeyDto dto)
        {
            if (!RoleClaims.IsAdmin(User) && !RoleClaims.IsTeacher(User)) return Forbid();
            await _questionService.SetCorrectLettersAsync(examId, dto.Answers ?? new List<AnswerKeyItemDto>());
            var list = await _questionService.GetQuestionsByExamIdAsync(examId);
            return Ok(list);
        }

        [HttpGet("exam/{examId}/difficulty")]
        public async Task<IActionResult> GetDifficulty(int examId)
        {
            if (!RoleClaims.IsAdmin(User) && !RoleClaims.IsTeacher(User)) return Forbid();
            await _examService.SyncExamStatusesAsync();
            var list = await _questionService.GetQuestionDifficultyAsync(examId);
            return Ok(list);
        }
    }
}
