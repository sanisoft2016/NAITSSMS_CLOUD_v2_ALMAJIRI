using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class ContinuousAssessmentTypeDto
    {
        public short Id { get; set; }
        public string SchoolId { get; set; }
        public SchoolInfoDto SchoolInfo { get; set; }
        public AssessmentType AssessmentType { get; set; }
    }
}
