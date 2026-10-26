using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
   public class AllStudentsAndTeachesDto
    {
        public List<StudentDetailsDto> StudentDetails { get; set; }
        public List<EmployeeDto> Employees { get; set; }
    }
}
