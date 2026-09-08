using Exam.Core.DTOs.Exam;
using Exam.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Exam.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class QuestionsController : ControllerBase
    {
        private readonly IQuestionService _questionService;

        public QuestionsController(IQuestionService questionService)
        {
            _questionService = questionService;
        }

        [HttpGet("exam/{examId}")]
        public async Task<IActionResult> GetByExamId(int examId)
        {
            var questions = await _questionService.GetQuestionsByExamIdAsync(examId);
            return Ok(questions);
        }

        [HttpPost("exam/{examId}")]
        public async Task<IActionResult> Create(int examId, [FromBody] CreateQuestionDto dto)
        {
            var question = await _questionService.CreateQuestionAsync(examId, dto);
            return Ok(question);
        }
    }
}
