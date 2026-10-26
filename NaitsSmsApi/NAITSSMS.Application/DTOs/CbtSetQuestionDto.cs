using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class CbtSetQuestionDto
    {
        public int Id { get; set; }
        public int QuestionAndAnswerId { get; set; }
        public CbtQuestionAndAnswerDto QuestionAndAnswer { get; set; }
    }
}
