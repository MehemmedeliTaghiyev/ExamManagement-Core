using Exam.Core.DTOs.Exam;

namespace Exam.Core.Interfaces
{
    public interface IExamService
    {
        Task<ExamCardDto?> GetExamCardByIdAsync(int examId);
        Task<IReadOnlyList<ExamCardDto>> GetStudentExamCardsAsync();
        Task<string?> UploadPdfAsync(int examId, IFormFile file);
        Task<Exam.Core.Domain.Exam?> CreateExamAsync(CreateExamDto dto);
        Task<ExamResponseDto?> GetExamByIdAsync(int id);
        Task<IEnumerable<ExamResponseDto>> GetAllExamsAsync();
    }
}
