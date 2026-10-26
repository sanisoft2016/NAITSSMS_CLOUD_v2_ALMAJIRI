using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class SchoolInfoDto
    {
        [Display(Name = "Registration Id")]
        public short SsNo { get; set; }
        [Required]
        [StringLength(70)]
        [Display(Name = "School Name")]
        public string SchoolName { get; set; }
       
        [StringLength(150)]
        [Display(Name = "School Address")]
        public string SchoolAddress { get; set; }
        [StringLength(50)]
        public string Telephone { get; set; }
        [StringLength(100)]
        [Display(Name = "Post Office Address")]
        public string PoAddress { get; set; }
        [StringLength(50)]
        [DataType(DataType.EmailAddress)]
        public string EmailAddress { get; set; }
        [StringLength(70)]
        public string Website { get; set; }
        [Display(Name = "Upload School Logo")]
        [StringLength(40)]
        public string SchoolLogoString { get; set; }
        [StringLength(70)]
        public string Motto { get; set; }
        public string SchoolRegistrationOfficerId { set; get; }
        public SchoolAdminAccountantStatus SchoolAdminAccountantStatus { get; set; }//For Super Admin Consumption only
        public int? AccountingStartTermId { set; get; }
        public SchoolRegistrationOfficerDto SchoolRegistrationOfficer { get; set; }
        public IEnumerable<EmployeeDto> Teacher { get; set; }
        public IEnumerable<SubjectDto> Subject { get; set; }
        [StringLength(162)]
        public string SystemImplementationType { get; set; }
        /// <summary>
        /// Decoded flag from SystemImplementationType (":0" Per Term, ":1" Per Student Per Term).
        /// Populated for Super Admin school-list UI; not persisted.
        /// </summary>
        public byte? SystemImplementationTypeCode { get; set; }
        [StringLength(150)]
        public string SmsApiKey { get; set; }
        [StringLength(70)]
        public string SmsApiToken { get; set; }
        public SubscriptionStatus MyStatus { get; set; }

        [StringLength(162)]
        public string FeatureImplementationType { get; set; }
    }
}
