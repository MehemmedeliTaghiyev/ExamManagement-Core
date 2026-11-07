using System.Text.Json.Serialization;
using Exam.Core.Enums;

namespace Exam.Core.DTOs.Exam
{
    public class QuestionResponseDto
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Points { get; set; }
        public QuestionType Type { get; set; }
        public string InputKind { get; set; } = "Choice";
        public string? CorrectText { get; set; }
        public string? FigureUrl { get; set; }
        /// <summary>Same URL as FigureUrl — TakeExam parseQuestionImage / imageUrl.</summary>
        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }
        [JsonPropertyName("figure")]
        public string? Figure { get; set; }
        [JsonPropertyName("figure_path")]
        public string? FigurePath { get; set; }
        public string? SourceQnum { get; set; }
        public List<QuestionOptionResponseDto> Options { get; set; } = new();

        public void AttachFigure(string? url)
        {
            FigureUrl = url;
            ImageUrl = url;
            Figure = url;
            FigurePath = url;
        }
    }
}
