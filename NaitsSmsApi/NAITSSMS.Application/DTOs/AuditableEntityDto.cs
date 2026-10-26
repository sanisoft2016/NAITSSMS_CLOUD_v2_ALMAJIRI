using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
   public class AuditableEntityDto
    {
        [StringLength(40)]
        public string Id { get; set; }
        public DateTime CreatedDate { get; set; }

        [MaxLength(70)]
        [ScaffoldColumn(false)]
        public string CreatedBy { get; set; }

        [ScaffoldColumn(false)]
        public DateTime UpdatedDate { get; set; }

        [MaxLength(70)]
        [ScaffoldColumn(false)]
        public string UpdatedBy { get; set; }
    }
}
