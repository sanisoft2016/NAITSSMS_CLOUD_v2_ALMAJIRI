using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Application.DTOs
{
    /// <summary>
    /// The same as Form Master
    /// </summary>
    public class TeacherClassArmAssignmentDto
    {
        public string Id { get; set; } = string.Empty;

        public string EmployeeId { get; set; } = string.Empty;
        public EmployeeDto? Employee { get; set; }

        public string ArmId { get; set; } = string.Empty;
        public ClassArmDto? Arm { get; set; }

        /// <summary>Legacy display: ClassLabel + ArmName (e.g. "JSS 1 A").</summary>
        public string ClassArmLabel { get; set; } = string.Empty;

        public ArmTermlyAssessmentStatus ArmTermlyAssessmentStatus { get; set; }
    }
}
