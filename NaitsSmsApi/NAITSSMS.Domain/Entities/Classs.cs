using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
    public class RxClasss
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        public byte SNo { get; set; }
        [Required]
        public CurrentClass ClassName { get; set; }

        [StringLength(40)]
        public string ClassLabel { get; set; } = string.Empty;
        public ICollection<AxClassArm> ArmName { get; set; } = new List<AxClassArm>();
        //public virtual ICollection<RxSubject> Subject { get; set; }
        [StringLength(40)]
        public string SchoolInfoId { get; set; } = string.Empty;
        [ForeignKey("SchoolInfoId")]
        public SchoolTyInfo? SchoolInfo { get; set; }


    }
}
