using Exam.Core.DTOs.Submission;

namespace Exam.Core.Interfaces
{
    public interface ISubmissionService
    {
        Task<StartExamResponseDto> StartExamAsync(StartExamDto dto);
        Task<ExamResultDto?> GetStudentResultAsync(int studentExamId);
        Task<IReadOnlyList<ExamResultDto>> GetStudentHistoryAsync(int studentId);
        Task<ExamResultDto?> SubmitExamAsync(SubmitExamDto dto);
        Task<IReadOnlyList<ExamResultDto>> GetExamSubmissionsAsync(int examId);
        Task SaveProgressAsync(SubmitExamDto dto);
        Task FinalizeExpiredSessionsAsync(int? studentId = null, int? examId = null);
        Task<ExamReviewDto?> GetExamReviewAsync(int studentExamId, int requesterId, bool isStaff);
        Task<ExamReviewDto?> GetExamReviewByExamAsync(int examId, int requesterId, bool isStaff);
    }
}
