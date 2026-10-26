using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Application.DTOs
{
    public sealed class SuperAdminDashboardDto
    {
        public int TotalSchools { get; set; }
        public int ActiveSchools { get; set; }
        public int DisabledSchools { get; set; }
        public int SchoolsWithAccountant { get; set; }
        public int SchoolsWithoutAccountant { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int ExpiringWithin30Days { get; set; }
        public int ExpiredSubscriptions { get; set; }
        public int NoSubscription { get; set; }
        public int TotalAgents { get; set; }
        public IReadOnlyList<SuperAdminSchoolOverviewDto> SchoolsNeedingAttention { get; set; } =
            Array.Empty<SuperAdminSchoolOverviewDto>();
        public IReadOnlyList<SuperAdminSchoolOverviewDto> RecentSchools { get; set; } =
            Array.Empty<SuperAdminSchoolOverviewDto>();
    }

    public sealed class SuperAdminSchoolOverviewDto
    {
        public string SchoolId { get; set; } = string.Empty;
        public short RegistrationNumber { get; set; }
        public string SchoolName { get; set; } = string.Empty;
        public string RegistrarUserId { get; set; } = string.Empty;
        public string RegistrarName { get; set; } = string.Empty;
        public string RegistrarUserName { get; set; } = string.Empty;
        public AccessStatus AccessStatus { get; set; }
        public SchoolAdminAccountantStatus AccountantStatus { get; set; }
        public DateTime? SubscriptionExpiryDate { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string SubscriptionHealth { get; set; } = "No subscription";
        public int AttentionPriority { get; set; }
    }
}
