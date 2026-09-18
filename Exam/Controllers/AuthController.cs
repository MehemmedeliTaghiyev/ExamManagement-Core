using Exam.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static Exam.Core.DTOs.Auth.AuthDTOs;

namespace Exam.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
        {
            try
            {
                var result = await _authService.RegisterAsync(dto);
                if (result == null)
                    return BadRequest(new { message = "Bu e-poçt artıq qeydiyyatdadır." });

                return Ok(result);
            }
            catch (InvalidOperationException ex) when (ex.Message == "STUDENT_VIA_TEACHER")
            {
                return BadRequest(new { message = "Tələbəni yalnız müəllim qeydiyyata sala bilər." });
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            try
            {
                var result = await _authService.LoginAsync(dto);
                if (result == null)
                    return Unauthorized(new { message = "Invalid email or password." });

                return Ok(result);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("ACCESS_CLOSED"))
            {
                var msg = ex.Message.StartsWith("ACCESS_CLOSED|")
                    ? ex.Message["ACCESS_CLOSED|".Length..]
                    : "Hesabınız bağlanıb. Giriş üçün adminə müraciət edin.";
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    code = "ACCESS_CLOSED",
                    message = msg
                });
            }
        }
    }
}
