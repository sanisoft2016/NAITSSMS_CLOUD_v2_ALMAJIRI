using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class InternalRegistrationKeyEncodingAndExpiryDateDto
    {
        public string ExpiryDate { get; set; }
        public string InternalRegistrationKeyEncoding { get; set; }
    }
    public class LoginReturnStatus
    {
        public string StatusCode { get; set; }
        public string ReturnMessage { get; set; }
    }
    public class InterfaceCardInfo
    {
        public CardStatus Status { get; set; }
        public string CardNumber { get; set; }
    }
    public class LicenseSubmisionDto
    {
        [Required]
        public string LicenceId { get; set; }
        [Required]
        public string RegistrationKey { get; set; }
    }
}
