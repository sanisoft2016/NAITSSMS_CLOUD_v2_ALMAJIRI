namespace NAITSSMS.Application.DTOs
{
    public class TeacherSubjectArmAssignmentDto
    {
        public string Id { get; set; } = string.Empty;

        public string SubjectId { get; set; } = string.Empty;
        public SubjectDto? Subject { get; set; }

        public string EmployeeId { get; set; } = string.Empty;
        public EmployeeDto? Employee { get; set; }

        public string ArmId { get; set; } = string.Empty;
        public ClassArmDto? Arm { get; set; }

        /// <summary>Legacy display: ClassLabel + ArmName (e.g. "JSS 1 A").</summary>
        public string ClassArmLabel { get; set; } = string.Empty;
    }
}
