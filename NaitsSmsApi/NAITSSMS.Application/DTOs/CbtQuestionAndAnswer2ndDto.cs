using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class CbtQuestionAndAnswer2ndDto
    {
        public int Id { get; set; }

        public int QuestionReferenceId { get; set; }
        public AssessmentType AssessmentType { get; set; }

        public string Question { get; set; }
        public string optionNoA { get; set; }
        public string optionNoB { get; set; }
        public string optionNoC { get; set; }
        public string optionNoD { get; set; }

        public AnswerOption Answer { get; set; }
        public int TermId { get; set; }
        public int ClassId { get; set; }
        public int SubjectId { get; set; }
    }
}
