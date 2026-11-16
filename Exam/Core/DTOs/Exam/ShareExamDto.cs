namespace Exam.Core.DTOs.Exam
{
    public class ShareExamDto
    {
        public bool? IsPublic { get; set; }
        public string? Visibility { get; set; }
        public string? GroupName { get; set; }
        public string? ShareGroupName { get; set; }
        public List<int>? StudentIds { get; set; }
    }
}
