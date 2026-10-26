using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
   public class ClassArmDto
    {
       public string Id { get; set; } = string.Empty;
       public byte SNo { get; set; }
       public string ClassId { get; set; } = string.Empty;
        [ForeignKey("ClassId")]
       public  ClassDto? Classs { get; set; }
        [Display(Name = "Arm's Name")]
        public ArmName ArmName { get; set; }
        public string FormMaster { get; set; } = string.Empty;
        public IEnumerable<StudentDetailsDto>? StudentDetails { get; set; }

        //public IEnumerable<TeacherSubjectArmAssignmentDto> TeacherSubjectArmAssignment { get; set; }

        //public static explicit operator int (ClassArmDto v)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
