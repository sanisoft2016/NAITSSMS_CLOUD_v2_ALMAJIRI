using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    //public class TeacherIstDto
    //{
    //    public string Id { get; set; }

    //    [Required]
    //    [Display(Name = "First Name")]
    //    public string FirstName { get; set; }

    //    [Display(Name = "Middle Name")]
    //    public string MiddleName { get; set; }

    //    [Required]
    //    [Display(Name = "Sur Name")]
    //    public string SurName { get; set; }

    //    [Required]
    //    [Display(Name = "User Name")]
    //    public string UserName { get; set; }

    //    [Required]
    //    [DataType(DataType.EmailAddress)]
    //    public string Email { get; set; }

    //    [Required]
    //    public virtual Gender Gender { get; set; }

    //    [Required]
    //    [StringLength(30, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
    //    [DataType(DataType.Password)]
    //    [Display(Name = "Password")]
    //    public string Password { get; set; }

    //    [Required]
    //    [DataType(DataType.Password)]
    //    [Display(Name = "Confirm password")]
    //    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    //    public string ConfirmPassword { get; set; }

    //    public DateTime? CreatedDate { get; set; }
    //    [Display(Name = "Phone No.")]
    //    public string PhoneNumber { get; set; }

    //    [Display(Name = "Date of Birth")]
    //    public DateTime? DateOfBirth { get; set; }
    //    public string UserId { get; set; }
    //    public string Addresses { get; set; }
    //    public virtual TeacherStatus Status { get; set; }
    //}
    public class EmployeeDto
    {
        public string Id { set; get; }
        [StringLength(100)]
        public string FullAddress { get; set; }
        //public virtual TeacherStatus Status { get; set; }
        public DateTime? LastLogin { get; set; }
        [Display(Name = "Date Of Birth")]
        public DateTime? DateOfBirth { get; set; }
        [StringLength(50)]
        public string UserId { get; set; }
        [StringLength(50)]
        [Display(Name = "Signature Image Upload")]
        public string SignatureImageString { get; set; }
        public UserDto User { get; set; }
        public TimeStatus TimeStatus { get; set; }
        //public AccessStatus AccessStatus { get; set; }
        public string EmployeeStatus { set; get; }
        public string SchoolId { get; set; }
        public SchoolInfoDto SchoolInfo { get; set; }
        public EmployeeIncomeStructureStatus EmployeeIncomeStructureStatus { set; get; }
        public EmployeeIncomeStructureDto EmployeeIncomeTyStructure { get; set; }
        [StringLength(20)]
        public string EmployeeIdNo { get; set; }
        [StringLength(30)]
        public string Decipline { set; get; }
        public EmployeeQualificationStatus EmployeeQualificationStatus { set; get; }
    }

    public class EmployeesXtractionDto
    {
        public int Id { set; get; }
        public string FullName { set; get; }
        public EmployeeStatus EmployeeStatus { set; get; }
        public EmployeeIncomeStructureStatus EmployeeIncomeStructureStatus { set; get; }

    }
}
