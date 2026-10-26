using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
   public class LicensTying
   {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        [StringLength(40)]
        public string SchoolSmsId { get; set; } = string.Empty; //SchoolId

        [ForeignKey("SchoolSmsId")]
        public SchoolTyInfo? SchoolSmsInfo { get; set; }
        public int SubscriptionId { get; set; }

        [Display(Name = "Payment Date")]

        public DateTime PaymentDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        [Required]
        public string AgentUserId { get; set; } = string.Empty;
        [ForeignKey("AgentUserId")]
        public Agent? AgentUser { get; set; }
        [Display(Name = "License Category")]

        public LicenseCategories LicenseCategory { get; set; }
        public float Charges { get; set; }

        public TimeStatus TimeStatus { get; set; }

        public RenewalConsumptionStatus RenewalConsumptionStatus { get; set; }
    }
}
