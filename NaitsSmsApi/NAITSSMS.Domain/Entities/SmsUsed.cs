using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace NAITSSMS.Domain.Entities
{
    public class SmsUsed
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        [StringLength(40)]
        public string SchoolSmsId { get; set; } = string.Empty; //SchoolId
        [ForeignKey("SchoolSmsId")]
        public SchoolTyInfo? SchoolSmsInfo { get; set; }
        public DateTime UsedDate { get; set; }
        public float PricePerSms { get; set; }
        public byte NoOfPages { get; set; }
        public short NoOfRecipient { get; set; }
        public float BalB4SendindSms { get; set; }
    }
}