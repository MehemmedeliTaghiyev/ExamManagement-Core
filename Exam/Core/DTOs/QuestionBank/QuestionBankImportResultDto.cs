namespace Exam.Core.DTOs.QuestionBank
{
    public class QuestionBankImportResultDto
    {
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
