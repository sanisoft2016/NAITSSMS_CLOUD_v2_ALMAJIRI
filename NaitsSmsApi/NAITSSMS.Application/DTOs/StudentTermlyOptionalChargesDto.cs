using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
   public class StudentTermlyOptionalChargesDto
    {
        [StringLength(40)]
        public string Id { get; set; }

        public StudentTermlyOptionalChargesDto()
        {
            Id = Guid.NewGuid().ToString();
        }
        [StringLength(40)]
        public string StudentId { get; set; }
        public StudentDetailsDto StudentDetail { get; set; }
        public int ClassArmId { get; set; }
        public ClassArmDto ClassArm { get; set; }
        public int OptionalFeesItemId { get; set; }
        public OptionalChargeItemsDto OptionalFeesItem { get; set; }
        public DateTime AsignedDate { get; set; }

        public ICollection<StudentTermlyOptionalPaymentDto> StudentTermlyOptionalPaymentList { get; set; }
    }
}
