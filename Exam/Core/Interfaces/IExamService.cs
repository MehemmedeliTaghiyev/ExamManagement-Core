using Exam.Core.DTOs.Exam;
using Exam.Core.DTOs;

namespace Exam.Core.Interfaces
{
    public interface IExamService
    {
        Task<ExamCardDto?> GetExamCardByIdAsync(int examId);
        // Controller üçün DTO qaytaran metod
        Task<ExamResponseDto?> GetExamResponseByIdAsync(int id);

        // Entity qaytaran metod (Servis daxilində update/delete üçün)
        Task<ExamResponseDto?> GetExamByIdAsync(int id);
        Task<IReadOnlyList<ExamCardDto>> GetStudentExamCardsAsync();
        Task<PagedResult<ExamResponseDto>> GetAllExamsAsync(ExamQueryParameters queryParameters);
        Task<Exam.Core.Domain.Exam?> CreateExamAsync(CreateExamDto dto);
        Task<bool> UpdateExamAsync(int id, UpdateExamDto dto);
        Task<bool> DeleteExamAsync(int id);
        Task<string?> UploadPdfAsync(int examId, Microsoft.AspNetCore.Http.IFormFile file);
    }
}
