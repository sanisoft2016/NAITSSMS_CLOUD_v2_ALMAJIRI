
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Domain.Entities
{
    public class CbtQuestionAndAnswer
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        public AssessmentType AssessmentType { get; set; }
        [StringLength(600)]
        public string Question{ get; set; } = string.Empty;
        [StringLength(2010)]
        public string  AnswerOptions { get; set; } =   string.Empty;
        public AnswerOption Answer { get; set; }

        public byte TermId { get; set; }
        [ForeignKey("TermId")]
        public Term? Term { get; set; }

        public string ClassId { get; set; } = string.Empty;
        [ForeignKey("ClassId")]
        public RxClasss? Classs { get; set; }
        public string SubjectId { get; set; } = string.Empty;
        [ForeignKey("SubjectId")]
        public RxSubject? Subject { get; set; }
    }
}
