
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Domain.Entities
{
    public class ContinuousAssessmentType
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        public string SchoolId { get; set; } = string.Empty;
        [ForeignKey("SchoolId")]
        public SchoolTyInfo? SchoolInfo { get; set; }
        public AssessmentType AssessmentType { get; set; }
    }
}
