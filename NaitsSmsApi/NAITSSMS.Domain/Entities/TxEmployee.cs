using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
    public class TxEmployee
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        [StringLength(100)]
        public string FullAddress { get; set; } = string.Empty;
        //public virtual TeacherStatus Status { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime? DateOfBirth { get; set; }
        [StringLength(50)]
        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public  ApplicationUser? User { get; set; }
        [StringLength(50)]
        public string SignatureImageString { get; set; } = string.Empty;
        public TimeStatus TimeStatus { get; set; }
        //public AccessStatus AccessStatus { get; set; }
        public EmployeeStatus EmployeeStatus { set; get; }
        [StringLength(40)]
        public string SchoolId { get; set; } = string.Empty; //Teacher
        [ForeignKey("SchoolId")]
        public SchoolTyInfo? SchoolInfo { get; set; }
        public EmployeeIncomeStructureStatus EmployeeIncomeStructureStatus { set; get; }
        public TxEmployeeIncomeStructure? EmployeeIncomeStructure { get; set; }
        [StringLength(20)]
        public string EmployeeIdNo { get; set; } = string.Empty;
        [StringLength(30)]
        public string Decipline { set; get; } = string.Empty;
        public EmployeeQualificationStatus EmployeeQualificationStatus { set; get; }
    }
}
