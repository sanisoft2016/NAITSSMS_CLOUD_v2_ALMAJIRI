using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
   public class SubordinateSalaryReportDto
    {
        public int MainSalaryReportId { get; set; }
        public MainSalaryReportDto MainSalaryReport { get; set; }
        public int CashNo { get; set; }
        public double CashAmount { get; set; }
        public int TellerNo { get; set; }
        public double TellerAmount { get; set; }
        public int TransferNo { get; set; }
        public double TransferAmount { get; set; }

    }
    public class MainSalaryReportDto
    {
        public int Id { get; set; }
        public string Month { get; set; }
        public int NoOfEmpl { get; set; }
        public double BasicSalary { get; set; }
        public double Allowance { get; set; }
        public double Bonus { get; set; }
        public double Deduction { get; set; }
        public SubordinateSalaryReportDto SubordinateSalaryReport { get; set; }
    }
}
