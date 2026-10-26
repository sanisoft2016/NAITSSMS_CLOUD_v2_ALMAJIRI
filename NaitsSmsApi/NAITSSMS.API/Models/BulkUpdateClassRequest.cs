using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NAITSSMS.API.Models;

public sealed class BulkUpdateClassRequest
{
    [Required]
    [MinLength(1)]
    public List<string> StudentIds { get; set; } = new();

    [Required]
    public string NewClassArmId { get; set; } = string.Empty;
}