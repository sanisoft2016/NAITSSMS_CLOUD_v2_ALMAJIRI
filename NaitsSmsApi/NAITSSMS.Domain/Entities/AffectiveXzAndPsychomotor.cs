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
   public class AffectiveXzAndPsychomotor
   {
 
        public required byte Id { get; set; }

        [StringLength(25)]
        public string ItemsName { get; set; } = string.Empty;
        //public int SchoolInfoId { get; set; }
        //[ForeignKey("SchoolInfoId")]
        //public SchoolTyInfo SchoolInfo { get; set; }
    }
}
