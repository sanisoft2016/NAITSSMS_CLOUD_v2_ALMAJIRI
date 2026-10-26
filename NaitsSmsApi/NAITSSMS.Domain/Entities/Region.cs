using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Base;

namespace NAITSSMS.Domain.Entities
{
    public class Region 
    {
        [Key]
        public required byte Id { get; set; }
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
        public ICollection<State> States { get; set; } = new List<State>();
    }
}

