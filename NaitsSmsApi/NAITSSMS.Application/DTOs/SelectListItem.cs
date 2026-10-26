namespace NAITSSMS.Application.DTOs;

/// <summary>
/// Lightweight replacement for System.Web.Mvc.SelectListItem
/// used by dropdown helper methods throughout the application.
/// </summary>
public sealed class SelectListItem
{
    public string Text { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool Selected { get; set; }
    public bool Disabled { get; set; }
}
