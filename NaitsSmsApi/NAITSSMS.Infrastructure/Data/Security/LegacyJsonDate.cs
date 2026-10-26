using System;
using Newtonsoft.Json;

namespace NAITSSMS.Infrastructure.Data.Security;

/// <summary>
/// Legacy ProgramYInfo / credential dates are stored as Microsoft JSON dates
/// (e.g. "\/Date(1797375600000)\/"). Newtonsoft matches the old SecurityService;
/// System.Text.Json cannot parse that format.
/// </summary>
public static class LegacyJsonDate
{
    /// <summary>Matches legacy JavaScriptSerializer.Serialize(DateTime) → "\/Date(ms)\/".</summary>
    public static string ToJson(DateTime date) =>
        JsonConvert.SerializeObject(date, new JsonSerializerSettings
        {
            DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
        });

    public static DateTime FromJson(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
        {
            throw new ArgumentException("Date string is required.", nameof(dateString));
        }

        return JsonConvert.DeserializeObject<DateTime>(dateString);
    }
}
