using Exam.Core.DTOs.Exam;
using Exam.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Exam.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExamsController : ControllerBase
    {
        private readonly IExamService _examService;

        public ExamsController(IExamService examService)
        {
            _examService = examService;
        }

        // GET: api/exams/1
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ExamCardDto>> GetExamCard(int id)
        {
            var examCard = await _examService.GetExamCardByIdAsync(id);

            if (examCard == null)
                return NotFound(new { message = "İmtahan tapılmadı." });

            return Ok(examCard);
        }

        // GET: api/exams
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<ExamCardDto>>> GetAllExamCards()
        {
            var cards = await _examService.GetStudentExamCardsAsync();
            return Ok(cards);
        }

        [HttpPost]
        public async Task<IActionResult> CreateExam([FromBody] CreateExamDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var createdExam = await _examService.CreateExamAsync(dto);

            if (createdExam == null)
            {
                return BadRequest(new { message = "Seçilən fənn (SubjectId) sistemdə tapılmadı." });
            }

            // CreatedAtAction: 1-ci parametr action adı, 2-ci parametr route id-si, 3-cü parametr body-də qayıdan obyekt
            return CreatedAtAction(
                nameof(_examService.GetExamByIdAsync),
                new { id = createdExam.Id },
                createdExam
            );
        }

        [HttpGet]
        public async Task<IActionResult> GetAllExams()
        {
            var exams = await _examService.GetAllExamsAsync();
            return Ok(exams);
        }



        [HttpPost("{id}/upload-pdf")]
        public async Task<IActionResult> UploadPdf(int id, IFormFile file)
        {
            // 1. Validation (HTTP spesifik yoxlamalar)
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Fayl seçilməyib." });

            if (Path.GetExtension(file.FileName).ToLower() != ".pdf")
                return BadRequest(new { message = "Yalnız PDF formatında fayllar qəbul edilir." });

            // 2. Service-ə müraciət
            var resultPath = await _examService.UploadPdfAsync(id, file);

            if (resultPath == null)
                return NotFound(new { message = "İmtahan tapılmadı." });

            return Ok(new { message = "PDF uğurla yükləndi.", filePath = resultPath });
        }
    }
}
