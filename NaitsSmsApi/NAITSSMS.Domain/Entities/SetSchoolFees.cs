using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using NAITSSMS.Domain.Base;

namespace NAITSSMS.Domain.Entities
{
   public class SetSchoolFeesTr : AuditableEntity
   {
        [Key, MaxLength(40)]
        public override string Id { get => base.Id; set => base.Id = value; }

        [StringLength(40)]
        public string SchoolId { get; set; } = string.Empty;
        [ForeignKey("SchoolId")]
        public SchoolTyInfo? SchoolInfo { get; set; }
        [MaxLength(40)] 
        public string YearlyAndTermlyStatusId { get; set; } = string.Empty;
        [ForeignKey("YearlyAndTermlyStatusId")]
        public TxYearlyAndTermlyStatus? YearlyAndTermlyStatus { get; set; }
        public double FreshPlayClass { get; set; }
        public double FreshPreNursery { get; set; }
        public double FreshNursery { get; set; }
        public double FreshPrimary { get; set; }
        public double FreshJss { get; set; }
        public double FreshScience { get; set; }
        public double FreshArt { get; set; }
        public double FreshCommercial { get; set; }

        public double RetnPlayClass { get; set; }
        public double RetnPreNursery { get; set; }
        public double RetnNursery { get; set; }
        public double RetnPrimary { get; set; }
        public double RetnJss { get; set; }
        public double RetnScience { get; set; }
        public double RetnArt { get; set; }
        public double RetnCommercial { get; set; }
    }
}
