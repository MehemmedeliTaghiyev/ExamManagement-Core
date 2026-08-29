namespace Exam.Core.DTOs.Exam
{
    public class ExamQueryParameters
    {
        private const int MaxPageSize = 50;

        public string? Search { get; set; }        // İmtahan adına görə axtarış
        public int? SubjectId { get; set; }        // Fənnə görə filtr
        public string? Status { get; set; }       // Statusa görə filtr (məs: Live, Completed)

        public int PageNumber { get; set; } = 1;

        private int _pageSize = 10;
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
        }
    }
}
