using System;

namespace NAITSSMS.Application.DTOs
{
    public partial class LogEntry
    {
        public DateTime Date { get; set; }
        public string ErrorMessage { get; set; }
        public string Source { get; set; }
        public string StackTrace { get; set; }
    }
}
