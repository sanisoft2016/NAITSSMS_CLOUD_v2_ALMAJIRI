using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class SmsPaymentBalanceDto
    {
        public string SchoolSmsId { get; set; }
        public SchoolInfoDto SchoolSmsInfo { get; set; }
        public float PricePerSms { get; set; }
        public float Amount { get; set; }
    }
}
