using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Application.DTOs
{
    public class SmsPurchaseDto
    {
        [Key]
        public int Id { get; set; }
        public string SchoolSmsId { get; set; }
        public SchoolSmsInfoDto SchoolSmsInfo { get; set; }
        [Display(Name = "Payment Date")]

        public DateTime PaymentDate { get; set; }
        [Required]
        public short AgentUserId { get; set; }
        [ForeignKey("AgentUserId")]
        public ApplicationUser AgentUser { get; set; }
        [Display(Name = "Bal b4 Purchase")]

        public float BalB4Purchase { get; set; }
        [Display(Name = "Amount Purchase")]
        public float AmountPurchase { get; set; }

        public TimeStatus TimeStatus { get; set; }
    }
}
