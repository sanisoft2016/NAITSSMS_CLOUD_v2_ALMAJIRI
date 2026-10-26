using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
   public class SchoolTyInfo
    {
        public short SsNo { get; set; }
        [StringLength(70)]
        public string SchoolName { get; set; } = string.Empty;

        [StringLength(150)]
        public string SchoolAddress { get; set; } = string.Empty;
        [StringLength(50)]
        public string Telephone { get; set; } = string.Empty;
        [StringLength(100)]
        public string PoAddress { get; set; } = string.Empty;
        [DataType(DataType.EmailAddress)]
        [StringLength(50)]
        public string EmailAddress { get; set; } = string.Empty;
        [StringLength(70)]
        public string Website { get; set; } = string.Empty;
        [StringLength(40)]
        public string SchoolLogoString { get; set; } = string.Empty;
        [StringLength(70)]
        public string Motto { get; set; } = string.Empty;
        [Key, StringLength(40)]
        public string SchoolRegistrationOfficerId { set; get; } = string.Empty;
        public SchoolAdminAccountantStatus SchoolAdminAccountantStatus { get; set; }//For Super Admin Consumption only
        [StringLength(40)]
        public string? AccountingStartTermId { set; get; }
        public SchoolRegistrationOfficer SchoolRegistrationOfficer { get; set; }
        public SchoolLevelCategoriesLabel SchoolLevelCategoriesLabel { get; set; }
        public ICollection<TxEmployee> Teacher { get; set; } = new List<TxEmployee>();
        public ICollection<RxSubject> Subject { get; set; }  = new List<RxSubject>();
        public AccessStatus ElectiveAccessStatus { get; set; }
        [StringLength(162)]
        public string SystemImplementationType { get; set; } = string.Empty;
        public SubscriptionStatus MyStatus { get; set; }
        [StringLength(150)]
        public string SmsApiKey { get; set; } = string.Empty;

        // Remove the 'Index(IsUnique = true)' part from the attribute, as 'Index' is not a valid attribute in standard .NET and 'IsUnique' is not a type.
        // If you need to create a unique index, this should be done via Fluent API in your DbContext configuration, not via data annotations.

        [StringLength(70)]
        public string SmsApiToken { get; set; } = string.Empty;
        public SmsPaymentBalance? SmsBalance { get; set; }
        public SchoolTyInfo()
       {
           SmsApiKey = Guid.NewGuid().ToString();
            SmsApiToken = Guid.NewGuid().ToString();
        }

        [StringLength(170)]
        public string TotalStudents { get; set; } = string.Empty;
        [StringLength(162)]
        public string FeatureImplementationType { get; set; } = string.Empty;

        [StringLength(162)]
        public string NetworkImplementationType { get; set; } = string.Empty;
    }
}
