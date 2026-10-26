using NAITSSMS.Domain.Enum;
using System;

namespace NAITSSMS.Domain.Entities
{
    public class SmsLog
    {
        public string Id { get; set; } = string.Empty;
        public string RecipientNumber { get; set; } = string.Empty;
        public string MessageText { get; set; } = string.Empty;
        public string? SchoolInfoId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? SentDate { get; set; }
        public SmsStatus Status { get; set; }
        public string? ErrorMessage { get; set; }

        // Navigation property
        public virtual SchoolTyInfo? SchoolInfo { get; set; }
    }
}