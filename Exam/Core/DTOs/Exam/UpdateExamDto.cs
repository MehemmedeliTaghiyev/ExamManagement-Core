using System.ComponentModel.DataAnnotations;

namespace Exam.Core.DTOs.Exam
{
    public class UpdateExamDto
    {
        [Required(ErrorMessage = "Fənn ID-si mütləqdir.")]
        public int SubjectId { get; set; }

        [Required(ErrorMessage = "İmtahan adı mütləqdir.")]
        [StringLength(200, ErrorMessage = "İmtahan adı maksimum 200 simvol ola bilər.")]
        public string Title { get; set; } = string.Empty;

        [Range(1, 600, ErrorMessage = "Müddət 1 ilə 600 dəqiqə arasında olmalıdır.")]
        public int DurationMinutes { get; set; }

        [Range(1, 200, ErrorMessage = "Sual sayı 1 ilə 200 arasında olmalıdır.")]
        public int TotalQuestions { get; set; }

        [Required(ErrorMessage = "Başlama tarixi mütləqdir.")]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "Bitmə tarixi mütləqdir.")]
        public DateTime EndTime { get; set; }

        public string Status { get; set; } = "Live";

        public bool? IsPublic { get; set; }

        public string? Visibility { get; set; }

        public string? GroupName { get; set; }

        public string? ShareGroupName { get; set; }

        public List<int>? StudentIds { get; set; }

        public string? Topic { get; set; }
    }
}
