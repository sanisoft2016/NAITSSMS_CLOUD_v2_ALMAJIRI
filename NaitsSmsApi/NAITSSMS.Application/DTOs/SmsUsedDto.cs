using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class SmsUsedDto
    {
        public int Id { get; set; }
        public string SchoolSmsId { get; set; }
        public SchoolInfoDto SchoolSmsInfo { get; set; }
        public DateTime UsedDate { get; set; }
        public float PricePerSms { get; set; }
        public byte NoOfPages { get; set; }
        public short NoOfRecipient { get; set; }
        public float BalB4SendindSms { get; set; }
    }
}
