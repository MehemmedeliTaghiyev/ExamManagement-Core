namespace Exam.Core.DTOs.QuestionBank
{
    public class FromBankResultDto
    {
        public int ExamId { get; set; }
        public int Added { get; set; }
        public int Mcq { get; set; }
        public int OpenCoded { get; set; }
        public int Written { get; set; }
        public int Situation { get; set; }
        public List<string> CopiedQnums { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
