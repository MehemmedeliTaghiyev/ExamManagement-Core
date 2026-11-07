using Exam.Core.Interfaces;
using Exam.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exam.Controllers
{
    [ApiController]
    [Route("api/question-bank")]
    [Authorize]
    public class QuestionBankController : ControllerBase
    {
        private readonly IQuestionBankService _bank;

        public QuestionBankController(IQuestionBankService bank)
        {
            _bank = bank;
        }

        /// <summary>CSV upsert by Qnum into QuestionBank (not exam Questions).</summary>
        [HttpPost("import")]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> Import(IFormFile? file, CancellationToken cancellationToken)
        {
            if (!RoleClaims.IsAdmin(User) && !RoleClaims.IsTeacher(User)) return Forbid();

            if (file is { Length: > 0 })
            {
                await using var stream = file.OpenReadStream();
                var uploaded = await _bank.ImportCsvAsync(stream, cancellationToken);
                return Ok(uploaded);
            }

            if (Request.ContentLength is > 0)
            {
                var fromBody = await _bank.ImportCsvAsync(Request.Body, cancellationToken);
                return Ok(fromBody);
            }

            return BadRequest(new { message = "CSV faylı göndərin (multipart field: file) və ya text/csv body." });
        }
    }
}
