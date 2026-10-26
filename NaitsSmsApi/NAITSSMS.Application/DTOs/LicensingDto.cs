using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using Newtonsoft.Json;

namespace NAITSSMS.Application.DTOs
{
   public class LicensingDto
    {
        public int Id { get; set; }
        public string SchoolSmsId { get; set; }//SchoolId
        public SchoolSmsInfoDto SchoolSmsInfo { get; set; }
        public int SubscriptionId { get; set; }//SchoolId

        [Display(Name = "Payment Date")]
        public DateTime PaymentDate { get; set; }
        public DateTime ExpiryDate { get; set; }

        [Required]
        public short AgentUserId { get; set; }
        [ForeignKey("AgentUserId")]
        public ApplicationUser AgentUser { get; set; }
        [Display(Name = "License Category")]

        public LicenseCategories LicenseCategory { get; set; }
        public float Charges { get; set; }

        public TimeStatus TimeStatus { get; set; }
    }
}
