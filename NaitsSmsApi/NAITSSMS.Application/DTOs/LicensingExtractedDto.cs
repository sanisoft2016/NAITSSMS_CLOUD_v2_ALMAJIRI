using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class LicensingExtractedDto
    {
        public string SchoolSmsId { get; set; }//SchoolId
        public int SubscriptionId { get; set; }
        public DateTime PaymentDate { get; set; }
        public DateTime ExpiryDate { get; set; }
    }
}
