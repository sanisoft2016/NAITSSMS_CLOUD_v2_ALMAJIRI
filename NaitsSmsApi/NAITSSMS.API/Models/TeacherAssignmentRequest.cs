using System.ComponentModel.DataAnnotations;

namespace NAITSSMS.API.Models
{
    public sealed class SubjectTeacherArmAssignmentRequest
    {
        [Required]
        public string TeacherId { get; set; } = string.Empty;

        [Required]
        public string SubjectId { get; set; } = string.Empty;

        [Required]
        public string ArmId { get; set; } = string.Empty;
    }

    public sealed class FormMasterArmAssignmentRequest
    {
        [Required]
        public string TeacherId { get; set; } = string.Empty;

        [Required]
        public string ArmId { get; set; } = string.Empty;
    }
}