using Exam.Core.DTOs.QuestionBank;

namespace Exam.Core.Interfaces
{
    public interface IQuestionBankService
    {
        Task<QuestionBankImportResultDto> ImportCsvAsync(Stream csvStream, CancellationToken cancellationToken = default);
        Task<FromBankResultDto> CopyFromBankAsync(int examId, FromBankRequestDto quota, CancellationToken cancellationToken = default);
    }
}
