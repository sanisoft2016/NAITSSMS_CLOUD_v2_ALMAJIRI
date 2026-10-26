using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace NAITSSMS.Domain.Entities
{
    public class PaintCategory
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        [MaxLength(30)]
        public string CatName { get; set; } = string.Empty;
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;
        public float CoinValue { get; set; }

    }
}
