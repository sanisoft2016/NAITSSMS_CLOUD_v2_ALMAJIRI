using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Entities;
using System.Collections.Generic;
using System.Linq;

namespace NAITSSMS.Application.Mappings
{
    /// <summary>
    /// Manual assessment mapping — avoids AutoMapper failures on incompatible
    /// entity/DTO shapes (e.g. YearlyAndTermlyStatus string Id vs int DTO Id).
    /// </summary>
    public static class StudentAssessmentMapping
    {
        public static List<StudentsAccessmentDto> MapList(IEnumerable<StudenXtAccessment> students)
            => students?.Select(Map).Where(dto => dto != null).ToList() ?? new List<StudentsAccessmentDto>();

        public static StudentsAccessmentDto? Map(StudenXtAccessment source)
        {
            if (source == null)
            {
                return null;
            }

            return new StudentsAccessmentDto
            {
                Id = source.Id,
                LastUpdateDate = source.LastUpdateDate,
                YearlyAndTermlyStatusId = source.YearlyAndTermlyStatusId,
                EmployeeId = source.EmployeeId,
                SubjectId = source.SubjectId,
                StudentId = source.StudentId,
                ClassArmId = source.ClassArmId,
                Ca1 = source.Ca1,
                Ca2 = source.Ca2,
                Ca3 = source.Ca3,
                Exams = source.Exams,
                YearlyAndTermlyStatus = source.YearlyAndTermlyStatus == null
                    ? null
                    : MapYearlyAndTermlyStatus(source.YearlyAndTermlyStatus),
                Subject = source.Subject == null
                    ? null
                    : new SubjectDto
                    {
                        Id = source.Subject.Id,
                        SubjectName = source.Subject.SubjectName,
                        TimeStatus = source.Subject.TimeStatus,
                        SubjectCategoryId = source.Subject.SubjectCategoryId,
                    },
                Student = MapStudent(source.Student),
                ClassArm = MapClassArm(source.ClassArm),
                Employee = source.Employee == null
                    ? null
                    : new EmployeeDto
                    {
                        Id = source.Employee.Id,
                        User = source.Employee.User == null
                            ? null
                            : new UserDto
                            {
                                Id = source.Employee.User.Id,
                                FirstName = source.Employee.User.FirstName,
                                MiddleName = source.Employee.User.MiddleName,
                                SurName = source.Employee.User.SurName,
                                UserName = source.Employee.User.UserName,
                            },
                    },
            };
        }

        private static StudentDetailsDto? MapStudent(TxStudentDetail? student)
        {
            if (student == null)
            {
                return null;
            }

            return new StudentDetailsDto
            {
                Id = student.Id,
                SNo = student.SNo,
                IdNo = student.IdNo,
                Passport = student.Passport,
                FirstName = student.User?.FirstName ?? string.Empty,
                MiddleName = student.User?.MiddleName ?? string.Empty,
                SurName = student.User?.SurName ?? string.Empty,
                CurrentClass = student.CurrentClass,
                ClassArmId = student.ClassArmId,
                UserId = student.UserId,
                User = student.User == null
                    ? null
                    : new ApplicationUserDto
                    {
                        Id = student.User.Id,
                        FirstName = student.User.FirstName,
                        MiddleName = student.User.MiddleName,
                        SurName = student.User.SurName,
                        UserName = student.User.UserName,
                        Gender = student.User.Gender,
                    },
            };
        }

        private static ClassArmDto? MapClassArm(AxClassArm? classArm)
        {
            if (classArm == null)
            {
                return null;
            }

            return new ClassArmDto
            {
                Id = classArm.Id,
                SNo = classArm.SNo,
                ClassId = classArm.ClassId,
                ArmName = classArm.ArmName,
                Classs = classArm.Classs == null
                    ? null
                    : new ClassDto
                    {
                        Id = classArm.Classs.Id,
                        SNo = classArm.Classs.SNo,
                        ClassName = classArm.Classs.ClassName,
                        ClassLabel = classArm.Classs.ClassLabel,
                        SchoolInfoId = classArm.Classs.SchoolInfoId,
                    },
            };
        }

        public static List<StudentsAttendanceDto> MapAttendanceList(
            IEnumerable<StudentsAtteXzndanceAndCommentsReport> attendance)
            => attendance?.Select(MapAttendance).Where(dto => dto != null).ToList()
               ?? new List<StudentsAttendanceDto>();

        public static StudentsAttendanceDto? MapAttendance(StudentsAtteXzndanceAndCommentsReport source)
        {
            if (source == null)
            {
                return null;
            }

            return new StudentsAttendanceDto
            {
                Id = source.Id,
                Attendance = source.Attendance,
                TeacherComments = source.TeacherComments,
                SchoolHeadComments = source.SchoolHeadComments,
                StudentId = source.StudentId,
                ClassArmId = int.TryParse(source.ClassArmId, out var classArmId) ? classArmId : 0,
                YearlyAndTermlyStatusId = int.TryParse(source.YearlyAndTermlyStatusId, out var yearlyId) ? yearlyId : 0,
                YearlyAndTermlyStatus = source.YearlyAndTermlyStatus == null
                    ? null
                    : MapYearlyAndTermlyStatus(source.YearlyAndTermlyStatus),
                Student = MapStudent(source.Student),
                ClassArm = MapClassArm(source.ClassArm),
            };
        }

        public static AffectiveAndPsychomotorDto? MapPsychomotor(AffectiveXzAndPsychomotor? source)
        {
            if (source == null)
            {
                return null;
            }

            return new AffectiveAndPsychomotorDto
            {
                Id = source.Id,
                ItemsName = source.ItemsName,
            };
        }

        public static CasSettingDto? ResolveCasSetting(IReadOnlyList<StudentsAccessmentDto> mapped)
        {
            var yearly = mapped.LastOrDefault()?.YearlyAndTermlyStatus;
            if (yearly == null)
            {
                return null;
            }

            return new CasSettingDto
            {
                PerCasValue = yearly.PerCasValue,
                CasCategory = yearly.CasCategory,
            };
        }

        private static YearlyAndTermlyStatusDto MapYearlyAndTermlyStatus(TxYearlyAndTermlyStatus term)
            => new()
            {
                CreatedDate = term.CreatedDate,
                TermlyAccessmentStatus = term.TermlyAccessmentStatus,
                TermStatus = term.TermStatus,
                CasCategory = term.CasCategory,
                PerCasValue = term.PerCasValue,
                MinPassMark = term.MinPassMark,
                NextTermBeginsDate = term.NextTermBeginsDate,
                AssessmtEndDate = term.AssessmtEndDate,
                TotalAttendance = term.TotalAttendance,
                Session = term.Session == null
                    ? null
                    : new AcademicSessionDto
                    {
                        Id = term.Session.Id,
                        Session = term.Session.Session,
                        SchoolInfoId = term.Session.SchoolInfoId,
                        Status = term.Session.Status,
                    },
            };
    }
}
