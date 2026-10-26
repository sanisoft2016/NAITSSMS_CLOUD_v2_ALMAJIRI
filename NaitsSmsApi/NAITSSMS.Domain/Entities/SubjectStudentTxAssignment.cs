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
    public class SubjectStudentTxAssignment
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }

        [StringLength(40)]
        public string StudentId { get; set; } = string.Empty;
        [ForeignKey("StudentId")]
        public TxStudentDetail? StudentDetail { get; set; }
        [StringLength(40)] 
        public string SubjectId { get; set; } = string.Empty;
        [ForeignKey("SubjectId")]
        public RxSubject? Subject { get; set; }
    }
}
