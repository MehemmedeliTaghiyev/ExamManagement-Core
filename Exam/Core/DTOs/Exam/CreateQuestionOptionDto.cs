using System.Text.Json.Serialization;

namespace Exam.Core.DTOs.Exam
{
    public class CreateQuestionOptionDto
    {
        [JsonPropertyName("optionText")]
        public string OptionText { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("isCorrect")]
        public bool IsCorrect { get; set; }
    }
}
