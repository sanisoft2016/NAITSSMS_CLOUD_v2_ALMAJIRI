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
   public class SchoolRegistrationOfficerDto
    {
        public string Id { get; set; }
        [StringLength(100)]
        public string FullAddress { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime? DateOfBirth { get; set; }
        [StringLength(50)]
        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public UserDto User { get; set; }
        [StringLength(50)]
        public string SignatureImageString { get; set; }
        public TimeStatus TimeStatus { get; set; }
        public AccessStatus AccessStatus { get; set; }
        //public byte ProprietorId { set; get; }
        //[ForeignKey("ProprietorId")]
        //public ProprieTytor Proprietor { set; get; }
        public SchoolInfoDto SchoolInfo { get; set; }
        [Required]
        public short AgentUserId { get; set; }
        [ForeignKey("AgentUserId")]
        public AgentDto AgentUser { get; set; }
    }
}
