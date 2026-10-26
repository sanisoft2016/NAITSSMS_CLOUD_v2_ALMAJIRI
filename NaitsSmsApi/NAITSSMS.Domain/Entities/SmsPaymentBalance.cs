using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NAITSSMS.Domain.Entities
{
    public class SmsPaymentBalance
    {
        [Key, StringLength(40)]
        public required string SchoolSmsId { get; set; }
        [ForeignKey("SchoolSmsId")]
        public SchoolTyInfo? SchoolSmsInfo { get; set; }
        public float PricePerSms { get; set; }
        public float Amount { get; set; }
    }
}