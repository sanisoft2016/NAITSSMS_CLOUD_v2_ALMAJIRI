using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Entities;

namespace NAITSSMS.Domain.Entities
{
    public class State
    {
        [Key]
        public byte Id { get; set; }
        //state
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
      
        public byte RegionId { get; set; }
        [ForeignKey("RegionId")]
        public Region? Region { get; set; }
        public ICollection<LocalGovernment> LocalGovernments { get; set; } = new List<LocalGovernment>();
    }

}