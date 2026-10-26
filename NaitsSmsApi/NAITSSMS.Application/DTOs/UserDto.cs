using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Application.DTOs
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        [MaxLength(50)]
        public string UserName { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Password { get; set; } = string.Empty;


        [MaxLength(50)]
        public string Email { get; set; } = string.Empty;
        [MaxLength(50)]
        public string PhoneNumber { get; set; } = string.Empty;
        [MaxLength(30)]
        public string FirstName { get; set; } = string.Empty;
        [MaxLength(30)]
        public string MiddleName { get; set; } = string.Empty;
        [MaxLength(30)]
        public string SurName { get; set; } = string.Empty;
        [MaxLength(30)]
        public string LastName { get; set; } = string.Empty;
        public string FullName => string.Format("{0} {1} {2}", FirstName, MiddleName, SurName).Trim();
        public UserStatus UserStatus { get; set; }
        public Gender Gender { get; set; }
        public AccessStatus AccessStatus { get; set; }

    }
}