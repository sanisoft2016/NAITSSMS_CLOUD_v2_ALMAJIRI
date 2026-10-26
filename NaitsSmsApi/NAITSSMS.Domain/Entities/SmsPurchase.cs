using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
namespace NAITSSMS.Domain.Entities
{
    public class SmsPurchase
    {
        [Key, StringLength(40)]
        public required string Id { get; set; }
        [StringLength(40)]
        public string SchoolSmsId { get; set; } = string.Empty;
        [ForeignKey("SchoolSmsId")]
        public SchoolTyInfo? SchoolSmsInfo { get; set; }
        [Display(Name = "Payment Date")]

        public DateTime PaymentDate { get; set; }
        [Required, StringLength(40)]
        public string AgentUserId { get; set; } = string.Empty;
        [ForeignKey("AgentUserId")]
        public Agent? AgentUser { get; set; }

        [Display(Name = "Bal b4 Purchase")]

        public float BalB4Purchase  { get; set; }
        [Display(Name = "Amount Purchase")]
        public float AmountPurchase { get; set; }

        public TimeStatus TimeStatus { get; set; }
    }
}