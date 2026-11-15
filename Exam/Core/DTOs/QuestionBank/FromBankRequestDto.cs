namespace Exam.Core.DTOs.QuestionBank
{
    public class FromBankRequestDto
    {
        public string? Audience { get; set; }
        /// <summary>One or more of 9/10/11 (comma-separated). Matches any tagged grade on the bank row.</summary>
        public string? Grade { get; set; }
        /// <summary>
        /// Question difficulty filter (Sadə/Orta/Çətin). Ignored for weekly papers:
        /// quizzes have no exam difficulty — one mixed paper per window.
        /// Teacher quizzes may still send this to filter questions.
        /// </summary>
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
