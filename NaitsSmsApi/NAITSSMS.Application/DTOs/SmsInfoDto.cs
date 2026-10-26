using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class SchoolSmsInfoto
    {
        public string SchoolId { get; set; }
        public string SchoolName { get; set; }
        public string SmsAutToken { get; set; }
        public string SmsAutKey { get; set; }

    }

    public class SmsUsersDto
    {
        public string Id { get; set; }
        [Required]
        [Display(Name = "User Name")]
        [StringLength(40)]
        public string UserName { get; set; }
        [Display(Name = "Email Address")]
        //[Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }

        [Required]
        [StringLength(40, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
        [Display(Name = "School Name")]
        [StringLength(70)]
        public string SchoolName { get; set; }
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; }
        [StringLength(50)]
        [Display(Name = "Phone No.")]
        public string PhoneNumber { get; set; }
        public AccessStatus AccessStatus { get; set; }

        [StringLength(40)]
        public string SmsApiKey { get; set; }
        [StringLength(40)]
        public string SmsApiToken { get; set; }
    }
    public class SentSmsHistoryDto
    {
        public short Id { get; set; }
        public string SchoolId { get; set; }
        public string Message { get; set; }
        public byte NoOfMtnDndLine { get; set; }
        public byte NoOfOrdidaryLine { get; set; }
        public byte NoOfIvalidLine { get; set; }
        public byte MessageDate { get; set; }
    }
    public class StudentsGuardianInfoUpdateDto
    {
        public string StudentId { get; set; }
        public string GuardianName { get; set; }
        public string TelNo { get; set; }
        public NetworkType NetworkType { get; set; }
    }

    public class SmsInfoDto
    {
        public string SenderName { get; set; }
        public List<string> RecipientNo { get; set; }
        public string   Message { get; set; }
        public int NoOfSms { get; set; }
    }
    public class MessageSentFeedbackDto
    {
        public float CostPerOrdinaryNo { get; set; }
        public float CostPerMtnDndNo { get; set; }
        public string ExecutionStatus { get; set; }
        public List<TelNumberDto> TelNos { get; set; }
    }
    public class TelNumberDto
    {
        public string TelNo { get; set; }
        public TelNoStatus TelNoStatus { get; set; }
    }
    public class TelNoDto
    {
        public string TelNo { get; set; }
        public NetworkType NetworkType { get; set; }
       public TelNoStatus TelNoStatus { get; set; }
    }
    public class StudentInfoForSmsDto
    {
        public string Id { set; get; }
        public CurrentClass Classs { set; get; }
        public string ClassLabel { set; get; }
        public string Arm { set; get; }
        
        [Display(Name = "Admission No.")]
        public string AdmsnNo { set; get; }
        [Display(Name = "Full Name")]
        public string FullName { set; get; }
        [Display(Name = "Guardian Name")]
        public string GuardianName { set; get; }
        [Display(Name = "GSM Number")]
        public string TelNo { get; set; }
       // public NetworkType NetworkType { get; set; }
    }
}
