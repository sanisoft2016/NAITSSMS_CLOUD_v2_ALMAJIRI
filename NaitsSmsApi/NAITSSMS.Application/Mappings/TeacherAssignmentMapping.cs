using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Application.Mappings
{
    internal static class TeacherAssignmentMapping
    {
        public static TeacherSubjectArmAssignmentDto MapSubjectArmAssignment(TeachTxerSubjectArmAssignment assignment)
        {
            return new TeacherSubjectArmAssignmentDto
            {
                Id = assignment.Id,
                SubjectId = assignment.SubjectId,
                EmployeeId = assignment.EmployeeId,
                ArmId = assignment.ArmId,
                ClassArmLabel = BuildClassArmLabel(assignment.Arm),
                Employee = MapEmployee(assignment.Employee),
                Subject = assignment.Subject == null
                    ? null
                    : new SubjectDto
                    {
                        Id = assignment.Subject.Id,
                        SubjectName = assignment.Subject.SubjectName,
                        TimeStatus = assignment.Subject.TimeStatus,
                        SubjectCategoryId = assignment.Subject.SubjectCategoryId,
                    },
                Arm = MapArm(assignment.Arm),
            };
        }

        public static TeacherClassArmAssignmentDto MapClassArmAssignment(TeacherClassArmTxAssignment assignment)
        {
            return new TeacherClassArmAssignmentDto
            {
                Id = assignment.Id,
                EmployeeId = assignment.EmployeeId,
                ArmId = assignment.ArmId,
                ClassArmLabel = BuildClassArmLabel(assignment.Arm),
                ArmTermlyAssessmentStatus = assignment.ArmTermlyAssessmentStatus,
                Employee = MapEmployee(assignment.Employee),
                Arm = MapArm(assignment.Arm),
            };
        }

        private static ClassArmDto? MapArm(AxClassArm? arm)
        {
            if (arm == null)
            {
                return null;
            }

            return new ClassArmDto
            {
                Id = arm.Id,
                SNo = arm.SNo,
                ClassId = arm.ClassId,
                ArmName = arm.ArmName,
                FormMaster = arm.FormMaster,
                Classs = arm.Classs == null
                    ? null
                    : new ClassDto
                    {
                        Id = arm.Classs.Id,
                        SNo = arm.Classs.SNo,
                        ClassName = arm.Classs.ClassName,
                        ClassLabel = arm.Classs.ClassLabel,
                        SchoolInfoId = arm.Classs.SchoolInfoId,
                    },
            };
        }

        private static string BuildClassArmLabel(AxClassArm? arm)
        {
            if (arm?.Classs == null)
            {
                return string.Empty;
            }

            var armName = arm.ArmName == ArmName.NoArm
                ? string.Empty
                : arm.ArmName.ToString();

            return $"{arm.Classs.ClassLabel} {armName}".Trim();
        }

        private static EmployeeDto? MapEmployee(TxEmployee? employee)
        {
            if (employee == null)
            {
                return null;
            }

            return new EmployeeDto
            {
                Id = employee.Id,
                UserId = employee.UserId,
                EmployeeIdNo = employee.EmployeeIdNo,
                EmployeeStatus = employee.EmployeeStatus.ToString(),
                TimeStatus = employee.TimeStatus,
                SchoolId = employee.SchoolId,
                User = MapUser(employee.User),
            };
        }

        private static UserDto? MapUser(ApplicationUser? user)
        {
            if (user == null)
            {
                return null;
            }

            return new UserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                MiddleName = user.MiddleName ?? string.Empty,
                SurName = user.SurName ?? string.Empty,
                AccessStatus = user.AccessStatus,
                Gender = user.Gender,
                UserStatus = user.UserStatus,
            };
        }
    }
}
