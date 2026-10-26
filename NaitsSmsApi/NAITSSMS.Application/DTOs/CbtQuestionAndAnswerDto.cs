using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
   public class CbtQuestionAndAnswerDto
    {
        public int Id { get; set; }
        public AssessmentType AssessmentType { get; set; }

        [StringLength(600)]
        public string Question { get; set; }
        [StringLength(2010)]
        public string AnswerOptions { get; set; }
        public AnswerOption Answer { get; set; }
        public int TermId { get; set; }
        [ForeignKey("TermId")]
        public YearlyAndTermlyStatusDto YearlyAndTermlyStatus { get; set; }
        public int ClassId { get; set; }
        [ForeignKey("ClassId")]
        public ClassDto Classs { get; set; }
        public int SubjectId { get; set; }
        [ForeignKey("SubjectId")]
        public SubjectDto Subject { get; set; }
    }
}
