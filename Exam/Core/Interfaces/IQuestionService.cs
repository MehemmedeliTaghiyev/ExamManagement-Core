using Exam.Core.Domain;
using Exam.Core.DTOs.Exam;

namespace Exam.Core.Interfaces
{
    public interface IQuestionService
    {
        Task<List<QuestionResponseDto>> GetQuestionsByExamIdAsync(int examId);
        Task<Question> CreateQuestionAsync(int examId, CreateQuestionDto dto);
        Task<int> EnsureChoiceSlotsAsync(int examId, int count);
        Task SetCorrectLettersAsync(int examId, IReadOnlyList<AnswerKeyItemDto> items);
        Task<List<QuestionDifficultyDto>> GetQuestionDifficultyAsync(int examId);
    }
}
