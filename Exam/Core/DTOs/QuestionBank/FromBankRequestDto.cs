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
        /// Teacher quizzes may still send this; empty bands fall back downward
        /// (çətin → orta → asan) unless <see cref="DifficultyStrict"/> is true.
        /// </summary>
        public string? DifficultyLevel { get; set; }
        /// <summary>When true, do not fall back to an easier band.</summary>
        public bool? DifficultyStrict { get; set; }
        public int? AsanCount { get; set; }
        public int? OrtaCount { get; set; }
        public int? CetinCount { get; set; }
        /// <summary>Qnums to treat as already used (prior weekly/daily papers).</summary>
        public List<string>? ExcludeQnums { get; set; }
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
