using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class CbtQuestionAnswerAndSetQuestionDto
    {
        public List<CbtSetQuestionDto> SetQuestion { get; set; }
        public List<CbtQuestionAndAnswerDto> QuestionAndAnswer { get; set; }
    }
}
