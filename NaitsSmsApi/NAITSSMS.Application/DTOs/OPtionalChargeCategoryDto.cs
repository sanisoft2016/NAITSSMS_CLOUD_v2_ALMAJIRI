using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class OPtionalChargeCategoryDto
    {
        public short Id { get; set; }

        [StringLength(40)]
        public string SchoolId { get; set; }

        [ForeignKey("SchoolId")]
        public SchoolInfoDto School { get; set; }

        [StringLength(50)]
        public string CategoryName { get; set; }

        public ICollection<OptionalChargeItemsDto> OptionalFeesItemList { get; set; }
    }
}
