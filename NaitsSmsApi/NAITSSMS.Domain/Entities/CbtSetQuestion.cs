using NAITSSMS.Domain.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Domain.Entities
{
    public class CbtSetQuestion
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        public string QuestionAndAnswerId { get; set; } = string.Empty;
        [ForeignKey("QuestionAndAnswerId")]
        public CbtQuestionAndAnswer? QuestionAndAnswer { get; set; }
    }
}
