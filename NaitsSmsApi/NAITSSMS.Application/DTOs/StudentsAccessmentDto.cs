using System;

namespace NAITSSMS.Application.DTOs
{
    public class StudentsAccessmentDto
    {
        public string Id { get; set; } = string.Empty;
        public DateTime LastUpdateDate { get; set; }
        public string? YearlyAndTermlyStatusId { get; set; }
        public YearlyAndTermlyStatusDto? YearlyAndTermlyStatus { get; set; }
        public string? EmployeeId { get; set; }
        public EmployeeDto? Employee { get; set; }
        public string? SubjectId { get; set; }
        public SubjectDto? Subject { get; set; }
        public string? StudentId { get; set; }
        public StudentDetailsDto? Student { get; set; }
        public string? ClassArmId { get; set; }
        public ClassArmDto? ClassArm { get; set; }
        public float Ca1 { get; set; }
        public float Ca2 { get; set; }
        public float Ca3 { get; set; }
        public float Exams { get; set; }
    }
}
