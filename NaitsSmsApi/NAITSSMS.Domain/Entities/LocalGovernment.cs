using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using NAITSSMS.Domain.Base;

namespace NAITSSMS.Domain.Entities
{

    public class LocalGovernment
    {
        [Key]
        public short Id { get; set; }
        public byte StateId { get; set; }
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
        [ForeignKey("StateId")]
        public State? State { get; set; }
    }
}
