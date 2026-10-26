using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class SubjectStudentAssignmentDto
    {
        public int Id { get; set; }
        [StringLength(40)]
        public string StudentId { get; set; }
        public StudentDetailsDto StudentDetail { get; set; }
        public int SubjectId { get; set; }
        public SubjectDto Subject { get; set; }
    }
}
