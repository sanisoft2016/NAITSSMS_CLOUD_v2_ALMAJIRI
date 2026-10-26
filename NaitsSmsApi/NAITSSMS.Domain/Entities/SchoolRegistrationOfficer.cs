using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
    public class SchoolRegistrationOfficer
    {
        [StringLength(40), Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        [StringLength(100)]
        public string FullAddress { get; set; } = string.Empty;
        public DateTime? LastLogin { get; set; }
        public DateTime? DateOfBirth { get; set; }
        [StringLength(40)]
        public string SignatureImageString { get; set; } = string.Empty;
        public TimeStatus TimeStatus { get; set; }
        public AccessStatus AccessStatus { get; set; }
        //public byte ProprietorId { set; get; }
        //[ForeignKey("ProprietorId")]
        //public ProprieTytor Proprietor { set; get; }
        public SchoolTyInfo? SchoolInfo { get; set; }
        [Required]
        public string AgentUserId { get; set; } = string.Empty;
        [ForeignKey("AgentUserId")]
        public Agent? AgentUser { get; set; }
    }
}
