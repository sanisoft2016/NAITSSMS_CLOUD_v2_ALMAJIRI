using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class ApplicationUserDto
    {
        public virtual string Email { get; set; }

        /// <summary>
        /// True if the email is confirmed, default is false
        /// 
        /// </summary>
        public virtual bool EmailConfirmed { get; set; }

        /// <summary>
        /// The salted/hashed form of the user password
        /// 
        /// </summary>
        public virtual string PasswordHash { get; set; }

        /// <summary>
        /// A random value that should change whenever a users credentials have changed (password changed, login removed)
        /// 
        /// </summary>
        public virtual string SecurityStamp { get; set; }

        /// <summary>
        /// PhoneNumber for the user
        /// 
        /// </summary>
        public virtual string PhoneNumber { get; set; }

        /// <summary>
        /// True if the phone number is confirmed, default is false
        /// 
        /// </summary>
        public virtual bool PhoneNumberConfirmed { get; set; }

        /// <summary>
        /// Is two factor enabled for the user
        /// 
        /// </summary>
        public virtual bool TwoFactorEnabled { get; set; }

        /// <summary>
        /// DateTime in UTC when lockout ends, any time in the past is considered not locked out.
        /// 
        /// </summary>
        public virtual DateTime? LockoutEndDateUtc { get; set; }

        /// <summary>
        /// Is lockout enabled for this user
        /// 
        /// </summary>
        public virtual bool LockoutEnabled { get; set; }

        /// <summary>
        /// Used to record failures for the purposes of lockout
        /// 
        /// </summary>
        public virtual int AccessFailedCount { get; set; }

        /// <summary>
        /// User ID (Primary Key)
        /// 
        /// </summary>
        public virtual string Id { get; set; }

        /// <summary>
        /// User name
        /// 
        /// </summary>
        public virtual string UserName { get; set; }

        [StringLength(40)]
        public string FirstName { get; set; }
        [StringLength(40)]
        public string MiddleName { get; set; }
        [StringLength(40)]
        public string SurName { get; set; }
        public DateTime CreatedDate { get; set; }
        public string FullName { get { return string.Format("{0} {1} {2}", FirstName, MiddleName, SurName); } }
        public Gender Gender { get; set; }
        public AccessStatus AccessStatus { get; set; }
    }
}
