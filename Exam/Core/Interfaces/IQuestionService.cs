using Exam.Core.Domain;
using Exam.Core.DTOs.Exam;

namespace Exam.Core.Interfaces
{
    public interface IQuestionService
    {
        Task<List<QuestionResponseDto>> GetQuestionsByExamIdAsync(int examId);
        Task<Question> CreateQuestionAsync(int examId, CreateQuestionDto dto);
    }
}
