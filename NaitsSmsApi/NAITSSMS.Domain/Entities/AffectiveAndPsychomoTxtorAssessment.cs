using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAITSSMS.Domain.Base;

namespace NAITSSMS.Domain.Entities
{
    public class AffectiveAndPsychomoTxtorAssessment
    {
        [Key, StringLength(40)]
        public required string Id { get; set; }
        public byte ItemId { get; set; } //AffectiveAndPsychomotor Id
        [ForeignKey("ItemId")]
        public AffectiveXzAndPsychomotor? AffectiveAndPsychomotor { set; get; }
        public byte Score { get; set; }
        public string YearlyAndTermlyStatusId { get; set; } = string.Empty;
        [ForeignKey("YearlyAndTermlyStatusId")]
        public TxYearlyAndTermlyStatus? YearlyAndTermlyStatus { get; set; }
        [StringLength(40)]
        public string StudentId { get; set; } = string.Empty;
        [ForeignKey("StudentId")]
        public TxStudentDetail? Student { get; set; }
        public string ClassArmId { get; set; } = string.Empty;
        [ForeignKey("ClassArmId")]
        public AxClassArm? ClassArm { get; set; }
        public string FormMasterId { get; set; } = string.Empty;
        [ForeignKey("FormMasterId")]
        public TxEmployee? FormMaster { get; set; }
        //public int SchoolInfoId { get; set; }
        //[ForeignKey("SchoolInfoId")]
        //public SchoolTyInfo SchoolInfo { get; set; }
    }
}
