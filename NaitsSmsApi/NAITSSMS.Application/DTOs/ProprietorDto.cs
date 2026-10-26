using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class ProprietorDto
    {
        public byte Id { get; set; }
        [StringLength(100)]
        public string FullAddress { get; set; }
        [StringLength(7)]
        public string SchoolRegId { get; set; }

        [StringLength(50)]
        public string UserId { get; set; }

        public UserDto User { get; set; }
    }
}
