using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NAITSSMS.Application.DTOs
{
    /// <summary>Legacy SadminController.LicensingIndex ViewBag payload.</summary>
    public class LicensingIndexPageDto
    {
        public string Id { get; set; } = string.Empty;
        public string SchoolName { get; set; } = string.Empty;
        public string SchoolAddress { get; set; } = string.Empty;
        public string SchoolTelephone { get; set; } = string.Empty;
        public float SmsBalance { get; set; }
        public string? ExpiringDate { get; set; }
        public byte? FeatureImplementationType { get; set; }
        public string? FeatureImplementationTypeWarning { get; set; }
        public string TotalStudents { get; set; } = "0";
    }

    public class CreateSubscriptionKeyRequestDto
    {
        public int Id { get; set; }
        public string SchoolSmsId { get; set; } = string.Empty;
        public int SubscriptionId { get; set; }
        public DateTime ExpiryDate { get; set; }
        public float Charges { get; set; }
        public string? AgentUserId { get; set; }
    }

    public class CreateSubscriptionKeyResultDto
    {
        public bool Success { get; set; }
        /// <summary>HTML shown in #regKey on success (legacy Json string).</summary>
        public string? HtmlMessage { get; set; }
        public string? Message { get; set; }
    }

    public class UpdateNumberOfStudentsRequestDto
    {
        public string SchoolId { get; set; } = string.Empty;
        public short NoOfStudents { get; set; }
    }

    public class UpdateNumberOfStudentsResultDto
    {
        public bool Success { get; set; }
        /// <summary>Legacy returns Json("Has successfully been updated") or Json(true) for failure.</summary>
        public string? Message { get; set; }
        public bool FailedSentinel { get; set; }
    }

    public class LastSubscriptionInfoDto
    {
        public bool HasSubscription { get; set; }
        public string? SubscriptionId { get; set; }
        public string? RegistrationKey { get; set; }
        public string? ExpiryDate { get; set; }
    }

    public class SubscriptionHistoryItemDto
    {
        public int SubscriptionId { get; set; }
        public DateTime PaymentDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public float Charges { get; set; }
        public string PaymentDateLong { get; set; } = string.Empty;
        public string ExpiryDateLong { get; set; } = string.Empty;
    }
}
