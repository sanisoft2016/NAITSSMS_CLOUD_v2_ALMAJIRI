using System.Collections.Generic;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Application.DTOs
{
    public sealed class TeacherAssignmentOptionsDto
    {
        public IList<SelectListItem> Teachers { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> Classes { get; set; } = new List<SelectListItem>();
    }

    public sealed class TeacherAssignmentArmResultDto
    {
        public IList<ClassArmDto> Arms { get; set; } = new List<ClassArmDto>();
        public string Message { get; set; } = string.Empty;
    }

    public sealed class TeacherUnassignedSubjectRecordDto
    {
        public string SubjectName { get; set; } = string.Empty;
        public CurrentClass ClassName { get; set; }
        public string ClassLabel { get; set; } = string.Empty;
        public ArmName ArmName { get; set; }
    }

    public sealed class TeacherCbtUserRecordDto
    {
        public string Id { get; set; } = string.Empty;
        public string EmployeeIdNo { get; set; } = string.Empty;
        public string EmployeeStatus { get; set; } = string.Empty;
        public TimeStatus TimeStatus { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public sealed class TeacherCbtUsersDto
    {
        public IList<TeacherCbtUserRecordDto> Employees { get; set; } = new List<TeacherCbtUserRecordDto>();
        public IList<string> UserIds { get; set; } = new List<string>();
    }
}