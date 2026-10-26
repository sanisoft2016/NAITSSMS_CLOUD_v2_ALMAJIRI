using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NAITSSMS.Application.DTOs
{
   public class SetSchoolFeesDto
   {
        public string Id { get; set; }
        public string SchoolId { get; set; }
        public SchoolInfoDto SchoolInfo { get; set; }
        public string YearlyAndTermlyStatusId { get; set; }
       public YearlyAndTermlyStatusDto YearlyAndTermlyStatus { get; set; }
        [Display(Name = "Play Class ₦")]
        public double FreshPlayClass { get; set; }
        [Display(Name = "Pre Nursery ₦")]
        public double FreshPreNursery { get; set; }
        [Display(Name = "Nursery ₦")]
        public double FreshNursery { get; set; }
        [Display(Name = "Primary ₦")]
        public double FreshPrimary { get; set; }
        [Display(Name = "Jss ₦")]
        public double FreshJss { get; set; }
        [Display(Name = "Science ₦")]
        public double FreshScience { get; set; }
        [Display(Name = "Art ₦")]
        public double FreshArt { get; set; }
        [Display(Name = "Commercial ₦")]
        public double FreshCommercial { get; set; }

        [Display(Name = "Play Class ₦")]
        public double RetnPlayClass { get; set; }

        [Display(Name = "Pre Nursery ₦")]
        public double RetnPreNursery { get; set; }
        [Display(Name = "Nursery ₦")]
        public double RetnNursery { get; set; }
        [Display(Name = "Primary ₦")]
        public double RetnPrimary { get; set; }
        [Display(Name = "Jss ₦")]
        public double RetnJss { get; set; }
        [Display(Name = "Science ₦")]
        public double RetnScience { get; set; }
        [Display(Name = "Art ₦")]
        public double RetnArt { get; set; }
        [Display(Name = "Commercial ₦")]
        public double RetnCommercial { get; set; }
    }
}
