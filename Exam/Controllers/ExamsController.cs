using Exam.Core.DTOs.Exam;
using Exam.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Exam.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Requires any valid JWT token for all endpoints by default
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
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> CreateExam([FromBody] CreateExamDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var createdExam = await _examService.CreateExamAsync(dto);

            if (createdExam == null)
            {
                return BadRequest(new { message = "Seçilən fənn (SubjectId) sistemdə tapılmadı." });
            }

            // Point directly to the Controller action method name
            return CreatedAtAction(
                nameof(GetExamById),
                new { id = createdExam.Id },
                createdExam
            );
        }

        // GET: api/exams?search=math&subjectId=2&pageNumber=1&pageSize=10
        [HttpGet]
        public async Task<IActionResult> GetAllExams([FromQuery] ExamQueryParameters queryParameters)
        {
            var pagedExams = await _examService.GetAllExamsAsync(queryParameters);
            return Ok(pagedExams);
        }

        // GET: api/exams/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetExamById(int id)
        {
            var exam = await _examService.GetExamByIdAsync(id);
            if (exam == null) return NotFound();
            return Ok(exam);
        }

        [HttpPost("{id}/upload-pdf")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> UploadExamPdf(int id, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Fayl seçilməyib və ya boşdur." });
            }

            // Ensure only PDF files are allowed
            if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Yalnız PDF formatında fayllar qəbul edilir." });
            }

            var exam = await _examService.GetExamByIdAsync(id);
            if (exam == null)
            {
                return NotFound(new { message = $"ID-si {id} olan imtahan tapılmadı." });
            }

            // Create wwwroot/uploads directory if it doesn't exist
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Generate a unique filename to prevent overwriting existing files
            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Relative path to store in database
            var relativePath = $"/uploads/{uniqueFileName}";

            // Update entity in database via service
            var updated = await _examService.UpdateExamPdfPathAsync(id, relativePath);
            if (!updated)
            {
                return StatusCode(500, new { message = "Fayl saxlanıldı, lakin məlumat bazası yenilənmədi." });
            }

            return Ok(new
            {
                message = "PDF uğurla yükləndi.",
                pdfFilePath = relativePath
            });
        }

        // PUT: api/exams/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExam(int id, [FromBody] UpdateExamDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var isUpdated = await _examService.UpdateExamAsync(id, dto);

            if (!isUpdated)
            {
                return NotFound(new { message = $"ID-si {id} olan imtahan tapılmadı və ya daxil edilən SubjectId yanlışdır." });
            }

            return NoContent(); // 204 NoContent - Uğurlu yeniləmə cavabı
        }

        // DELETE: api/exams/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExam(int id)
        {
            var isDeleted = await _examService.DeleteExamAsync(id);

            if (!isDeleted)
            {
                return NotFound(new { message = $"ID-si {id} olan imtahan tapılmadı." });
            }

            return Ok(new { message = "İmtahan uğurla silindi." });
        }

        [HttpPost("{id}/questions")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> AddQuestion(int id, [FromBody] CreateQuestionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var createdQuestion = await _examService.AddQuestionToExamAsync(id, dto);
            if (createdQuestion == null)
            {
                return NotFound(new { message = $"ID-si {id} olan imtahan tapılmadı." });
            }

            return Ok(createdQuestion);
        }
    }
}
