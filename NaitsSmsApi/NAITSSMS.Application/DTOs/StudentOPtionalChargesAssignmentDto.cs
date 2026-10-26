using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class StudentOPtionalChargesAssignmentDto
    {
        public int Id { set; get; }
        [StringLength(40)]
        public string StudentId { get; set; }
        [ForeignKey("StudentId")]
        public StudentDetailsDto StudentDetail { get; set; }
        public int OptionalFeesItemId { get; set; }
        [ForeignKey("OptionalFeesItemId")]
        public OptionalChargeItemsDto OptionalFeesItem { get; set; }
    }
}
