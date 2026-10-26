using System.ComponentModel.DataAnnotations;

namespace CustRewardMgtSys.Application.DTOs
{
    public class TwoFactorCodeDto
    {
        [Required]
        [StringLength(12, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;
    }

    public class TwoFactorLoginDto
    {
        [Required]
        public string TwoFactorToken { get; set; } = string.Empty;

        [Required]
        [StringLength(12, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;
    }

    public class TwoFactorDisableDto
    {
        [Required]
        [StringLength(12, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;
    }
}
