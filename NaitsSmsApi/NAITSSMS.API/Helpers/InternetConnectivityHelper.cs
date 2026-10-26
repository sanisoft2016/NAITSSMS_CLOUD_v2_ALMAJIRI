using System.Net.NetworkInformation;

namespace NAITSSMS.API.Helpers;

/// <summary>
/// Legacy RegistryController.CheckForInternetConnection for .NET 9.
/// </summary>
public static class InternetConnectivityHelper
{
    public static bool CheckForInternetConnection()
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send("google.com", 1000, new byte[32], new PingOptions());
            if (reply is { Status: IPStatus.Success })
            {
                return true;
            }

            return TryHttpFallback();
        }
        catch
        {
            return TryHttpFallback();
        }
    }

    private static bool TryHttpFallback()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = client.GetAsync(
                "http://www.google.com",
                HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            return response != null;
        }
        catch
        {
            return false;
        }
    }
}
