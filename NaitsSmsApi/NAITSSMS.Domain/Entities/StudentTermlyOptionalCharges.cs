using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Entities;

namespace NAITSSMS.Domain.Entities
{
    public class StudentTermlyOptionalCharges
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }

        public StudentTermlyOptionalCharges()
        {
            Id = Guid.NewGuid().ToString();
            StudentId = string.Empty;
            StudentDetail = null!;
            ClassArm = null!;
            OptionalFeesItem = null!;
            StudentTermlyOptionalPaymentList = new List<StudentTermlyOptionalPayment>();
        }
        [StringLength(40)]
        public string StudentId { get; set; } 
        [ForeignKey("StudentId")]
        public TxStudentDetail StudentDetail { get; set; }
        [StringLength(40)] 
        public string ClassArmId { get; set; } = string.Empty;
        [ForeignKey("ClassArmId")]
        public AxClassArm? ClassArm { get; set; }
        [StringLength(40)]
        public string OptionalFeesItemId { get; set; } = string.Empty;
        [ForeignKey("OptionalFeesItemId")]
        public OptionalXyChargeItems OptionalFeesItem { get; set; }
        public DateTime AsignedDate { get; set; }
        public ICollection<StudentTermlyOptionalPayment> StudentTermlyOptionalPaymentList { get; set; } = new List<StudentTermlyOptionalPayment>();
    }
}
