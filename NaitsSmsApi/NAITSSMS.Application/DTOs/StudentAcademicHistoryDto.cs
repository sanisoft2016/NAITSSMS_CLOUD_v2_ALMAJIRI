using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class StudentAcademicProgressHistoryDto
    {
        public string Id { get; set; }
        public string StudentId { get; set; }
        public StudentDetailsDto StudentDetails { get; set; }
        public int YearlyAndTermlyStatusId { get; set; }
        public YearlyAndTermlyStatusDto YearlyAndTermlyStatus { get; set; }
        public int ClassId { get; set; }
        public ClassDto Classs { get; set; }
        public ClassCategoryStatus ClassCategory { get; set; }
        public List<StudentTermlyFeesPaymentHistoryDto> StudentTermlyFeePayment { get; set; }

    }
}
