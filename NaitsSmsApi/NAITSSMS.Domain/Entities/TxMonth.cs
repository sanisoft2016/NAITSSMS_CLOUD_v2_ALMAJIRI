using NAITSSMS.Domain.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;

namespace NAITSSMS.Domain.Entities
{
    public class TxMonth
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        [StringLength(40)]
        public string YearId { set; get; } = string.Empty;
        [ForeignKey("YearId")]
        public TxYear? Year { get; set; }
        public byte Month { get; set; }
    }

   
}
