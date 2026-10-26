using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Application.DTOs
{
    /// <summary>
    /// Result DTO for automatic student promotion operations
    /// </summary>
    public class AutoPromotionResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        
        public CurrentClass SourceClass { get; set; }
        public CurrentClass TargetClass { get; set; }
        public ClassCategoryStatus TargetCategory { get; set; }
        
        public List<PromotedStudentInfo> PromotedStudents { get; set; } = new List<PromotedStudentInfo>();
        public List<string> Errors { get; set; } = new List<string>();
        
        public int TotalPromoted => PromotedStudents.Count;
        public bool HasErrors => Errors.Any();
    }

    /// <summary>
    /// Information about a promoted student
    /// </summary>
    public class PromotedStudentInfo
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string FromClass { get; set; } = string.Empty;
        public string ToClass { get; set; } = string.Empty;
        public string ToArm { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}
