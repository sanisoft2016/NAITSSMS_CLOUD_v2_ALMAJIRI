using System;
using System.Linq.Expressions;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Entities;

namespace NAITSSMS.Application.Mappings
{
    /// <summary>
    /// EF-translatable projections for student-result report payloads.
    /// Projects straight to DTOs so entity graphs are never materialized.
    /// </summary>
    public static class StudentResultsReportProjections
    {
        public static Expression<Func<StudenXtAccessment, StudentsAccessmentDto>> Assessment =>
            s => new StudentsAccessmentDto
            {
                Id = s.Id,
                LastUpdateDate = s.LastUpdateDate,
                YearlyAndTermlyStatusId = s.YearlyAndTermlyStatusId,
                EmployeeId = s.EmployeeId,
                SubjectId = s.SubjectId,
                StudentId = s.StudentId,
                ClassArmId = s.ClassArmId,
                Ca1 = s.Ca1,
                Ca2 = s.Ca2,
                Ca3 = s.Ca3,
                Exams = s.Exams,
                Subject = s.Subject == null
                    ? null
                    : new SubjectDto
                    {
                        Id = s.Subject.Id,
                        SubjectName = s.Subject.SubjectName,
                        TimeStatus = s.Subject.TimeStatus,
                        SubjectCategoryId = s.Subject.SubjectCategoryId,
                    },
                Student = s.Student == null
                    ? null
                    : new StudentDetailsDto
                    {
                        Id = s.StudentId,
                        SNo = s.Student.SNo,
                        IdNo = s.Student.IdNo,
                        Passport = s.Student.Passport,
                        FirstName = s.Student.User != null ? s.Student.User.FirstName : string.Empty,
                        MiddleName = s.Student.User != null ? s.Student.User.MiddleName : string.Empty,
                        SurName = s.Student.User != null ? s.Student.User.SurName : string.Empty,
                        CurrentClass = s.Student.CurrentClass,
                        ClassArmId = s.Student.ClassArmId,
                        UserId = s.Student.UserId,
                    },
                ClassArm = s.ClassArm == null
                    ? null
                    : new ClassArmDto
                    {
                        Id = s.ClassArm.Id,
                        SNo = s.ClassArm.SNo,
                        ClassId = s.ClassArm.ClassId,
                        ArmName = s.ClassArm.ArmName,
                        Classs = s.ClassArm.Classs == null
                            ? null
                            : new ClassDto
                            {
                                Id = s.ClassArm.Classs.Id,
                                SNo = s.ClassArm.Classs.SNo,
                                ClassName = s.ClassArm.Classs.ClassName,
                                ClassLabel = s.ClassArm.Classs.ClassLabel,
                                SchoolInfoId = s.ClassArm.Classs.SchoolInfoId,
                            },
                    },
                YearlyAndTermlyStatus = s.YearlyAndTermlyStatus == null
                    ? null
                    : new YearlyAndTermlyStatusDto
                    {
                        CreatedDate = s.YearlyAndTermlyStatus.CreatedDate,
                        TermlyAccessmentStatus = s.YearlyAndTermlyStatus.TermlyAccessmentStatus,
                        TermStatus = s.YearlyAndTermlyStatus.TermStatus,
                        CasCategory = s.YearlyAndTermlyStatus.CasCategory,
                        PerCasValue = s.YearlyAndTermlyStatus.PerCasValue,
                        MinPassMark = s.YearlyAndTermlyStatus.MinPassMark,
                        NextTermBeginsDate = s.YearlyAndTermlyStatus.NextTermBeginsDate,
                        AssessmtEndDate = s.YearlyAndTermlyStatus.AssessmtEndDate,
                        TotalAttendance = s.YearlyAndTermlyStatus.TotalAttendance,
                        Session = s.YearlyAndTermlyStatus.Session == null
                            ? null
                            : new AcademicSessionDto
                            {
                                Id = s.YearlyAndTermlyStatus.Session.Id,
                                Session = s.YearlyAndTermlyStatus.Session.Session,
                                SchoolInfoId = s.YearlyAndTermlyStatus.Session.SchoolInfoId,
                                Status = s.YearlyAndTermlyStatus.Session.Status,
                            },
                    },
            };

        public static Expression<Func<StudentsAtteXzndanceAndCommentsReport, StudentsAttendanceDto>> Attendance =>
            a => new StudentsAttendanceDto
            {
                Id = a.Id,
                Attendance = a.Attendance,
                TeacherComments = a.TeacherComments,
                SchoolHeadComments = a.SchoolHeadComments,
                StudentId = a.StudentId,
                ClassArmId = 0,
                YearlyAndTermlyStatusId = 0,
            };

        public static Expression<Func<AffectiveAndPsychomoTxtorAssessment, AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto>> Affective =>
            f => new AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto
            {
                ClassArmId = f.ClassArmId,
                FormMasterId = f.FormMasterId,
                StudentId = f.StudentId,
                YearlyAndTermlyAssessmentStatusId = f.YearlyAndTermlyStatusId,
                ItemId = f.ItemId,
                Score = f.Score,
                Student = f.Student == null
                    ? null
                    : new StudentFielExtractionDto
                    {
                        Id = f.StudentId,
                        FullName = f.Student.User == null
                            ? string.Empty
                            : ((f.Student.User.FirstName ?? string.Empty) + " " +
                               (f.Student.User.MiddleName ?? string.Empty) + " " +
                               (f.Student.User.SurName ?? string.Empty)),
                        CurrentClass = f.ClassArm != null && f.ClassArm.Classs != null
                            ? f.ClassArm.Classs.ClassName
                            : default,
                        ArmName = f.ClassArm != null ? f.ClassArm.ArmName : default,
                        CurrentClassLabel = f.Student.ClassArm != null && f.Student.ClassArm.Classs != null
                            ? f.Student.ClassArm.Classs.ClassLabel
                            : string.Empty,
                    },
                YearlyAndTermlyAssessmentStatus = f.YearlyAndTermlyStatus == null
                    ? null
                    : new YearlyAndTermlyStatusDto
                    {
                        NextTermBeginsDate = f.YearlyAndTermlyStatus.NextTermBeginsDate,
                        TotalAttendance = f.YearlyAndTermlyStatus.TotalAttendance,
                        TermStatus = f.YearlyAndTermlyStatus.TermStatus,
                        Session = f.YearlyAndTermlyStatus.Session == null
                            ? null
                            : new AcademicSessionDto
                            {
                                Session = f.YearlyAndTermlyStatus.Session.Session,
                            },
                    },
                AffectiveAndPsychomotor = f.AffectiveAndPsychomotor == null
                    ? null
                    : new AffectiveAndPsychomotorDto
                    {
                        Id = f.AffectiveAndPsychomotor.Id,
                        ItemsName = f.AffectiveAndPsychomotor.ItemsName,
                    },
            };
    }
}
