namespace NAITSSMS.Application.DTOs
{
    /// <summary>
    /// Student / parent portal sibling option (shared Contact MobileNumber).
    /// <see cref="Id"/> is the student UserId (TxStudentDetail.Id alias).
    /// </summary>
    public class StudentPortalSiblingDto
    {
        public string Id { get; set; } = string.Empty;
        public string IdNo { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ClassLabel { get; set; } = string.Empty;
        public string ArmLabel { get; set; } = string.Empty;
        public bool IsCurrentLogin { get; set; }
    }
}
