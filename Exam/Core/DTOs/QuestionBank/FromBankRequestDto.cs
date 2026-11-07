namespace Exam.Core.DTOs.QuestionBank
{
    public class FromBankRequestDto
    {
        public string? Audience { get; set; }
        public string? Grade { get; set; }
        public string? DifficultyLevel { get; set; }
        public List<string>? Topics { get; set; }
        /// <summary>weekly / teacher / buraxılış / blok (Sınaq, Buraxılış, Blok also accepted).</summary>
        public string? Kind { get; set; }
        public string? Subject { get; set; }
        public string? Language { get; set; }
        public int? McqCount { get; set; }
        public int? OpenCodedCount { get; set; }
        public int? WrittenCount { get; set; }
        public int? SituationCount { get; set; }
        public int? Count { get; set; }
    }
}
