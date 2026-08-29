using Exam.Core.DTOs.Submission;

namespace Exam.Core.Interfaces
{
    public interface ISubmissionService
    {
        Task<int> StartExamAsync(StartExamDto dto);
        Task<ExamResultDto?> SubmitExamAsync(SubmitExamDto dto);
        Task<ExamResultDto?> GetStudentResultAsync(int studentExamId);
        Task<IReadOnlyList<ExamResultDto>> GetStudentHistoryAsync(int studentId);
    }
}
