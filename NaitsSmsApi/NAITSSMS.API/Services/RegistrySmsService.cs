using System.Globalization;
using System.Net;
using System.Text.Json;
using NAITSSMS.API.Helpers;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Unitofwork;

namespace NAITSSMS.API.Services;

public sealed class IRegistrySmsService
{
    private const string JusibeUser = "7794de570dcdd94fee7f5cdf6c955cfe";
    private const string JusibePass = "93a8398d82619645dbec5cdac453f3d1";

    private readonly ITeachersAndOthersService _teachersAndOthersService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IRegistrySmsService> _logger;

    public IRegistrySmsService(
        ITeachersAndOthersService teachersAndOthersService,
        IUnitOfWork unitOfWork,
        IHttpClientFactory httpClientFactory,
        ILogger<IRegistrySmsService> logger)
    {
        _teachersAndOthersService = teachersAndOthersService;
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetSmsBalanceAsync(string userId, CancellationToken ct)
    {
        if (!InternetConnectivityHelper.CheckForInternetConnection())
        {
            return "Failed! Internet connection is required to check balance.";
        }

        var returnString = "Failed. Unable to complete the process.";
        var credentialEncoded = Uri.EscapeDataString(await _teachersAndOthersService.GetAndEncodeCredentials(userId, ct));
        var hostName = await ResolveNaitsHostNameAsync(ct);
        if (!string.IsNullOrEmpty(hostName))
        {
            var url = $"{hostName}/home/SmsBalance?credentialEncoded={credentialEncoded}";
            try
            {
                var balance = await GetRemoteStringAsync(url, ct);
                returnString = balance?.Replace("\"", string.Empty).Trim() ?? returnString;
                if (returnString == "000")
                {
                    returnString =
                        "Failed! SMS credentials are not registered on the NAITS online platform. "
                        + "Contact Administrator on 08035550938 to sync token/key, then retry.";
                }
                else if (returnString == "0000")
                {
                    returnString =
                        "Failed! No SMS units have been purchased for this school yet. "
                        + "Contact Administrator on 08035550938 to buy SMS units, then retry.";
                }
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                {
                    returnString = "Failed. " + ex.InnerException.Message;
                }
            }
        }
        else
        {
            returnString = "Failed! NAITS-ASMS online platform is temporarily unavailanle!!!";
        }

        return returnString;
    }

    public async Task<MessageSentFeedbackDto> SendMultipleSmsAsync(string userId, SmsInfoDto smsInfo, CancellationToken ct)
    {
        var returnResult = new MessageSentFeedbackDto
        {
            ExecutionStatus = "Process has been completed successfully.",
            TelNos = new List<TelNumberDto>(),
        };

        if (string.IsNullOrWhiteSpace(smsInfo.Message)
            || string.IsNullOrWhiteSpace(smsInfo.SenderName)
            || smsInfo.RecipientNo == null
            || smsInfo.RecipientNo.Count == 0)
        {
            returnResult.ExecutionStatus = "Failed! Message, sender name and at least one recipient are required.";
            return returnResult;
        }

        if (!InternetConnectivityHelper.CheckForInternetConnection())
        {
            returnResult.ExecutionStatus = "Failed! Internet connection is required to send SMS.";
            return returnResult;
        }

        var recipients = smsInfo.RecipientNo
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Select(number => number.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var noOfRecipients = recipients.Count;
        var noOfSmsPages = smsInfo.NoOfSms <= 0 ? 1 : smsInfo.NoOfSms;
        var balanceStatus = await GetSmsBalanceStatusAsync(userId, noOfRecipients, noOfSmsPages, ct);
        if (!balanceStatus.StartsWith("Success", StringComparison.OrdinalIgnoreCase))
        {
            returnResult.ExecutionStatus = balanceStatus;
            return returnResult;
        }

        var balanceParts = balanceStatus.Split(':');
        var naitsPlatformSmsBalance = float.Parse(balanceParts[1], CultureInfo.InvariantCulture);
        var pricePerSms = float.Parse(balanceParts[2], CultureInfo.InvariantCulture);

        var jusibeResult = await ConnectToJusibeAsync(new SmsInfoDto
        {
            Message = smsInfo.Message,
            SenderName = smsInfo.SenderName,
            RecipientNo = recipients,
            NoOfSms = noOfSmsPages,
        }, ct);

        var noOfSuccessfullySentSms = jusibeResult.TelNos.Count(item => item.TelNoStatus == TelNoStatus.OrdinaryNo);
        if (noOfSuccessfullySentSms >= 1f)
        {
            var newBalance = naitsPlatformSmsBalance - (pricePerSms * noOfSmsPages * noOfSuccessfullySentSms);
            var encodedParameter = WebUtility.UrlEncode(
                await _teachersAndOthersService.GetAndEncodeCredentialAndSmsBalanceUpdateParameters(
                    newBalance,
                    noOfSuccessfullySentSms,
                    noOfSmsPages,
                    ct));

            var hostName = await ResolveNaitsHostNameAsync(ct);
            if (string.IsNullOrEmpty(hostName))
            {
                returnResult.ExecutionStatus = "Failed! NAITS-ASMS online platform is temporarily unavailanle!!!";
                returnResult.TelNos = jusibeResult.TelNos;
                return returnResult;
            }

            var updateUrl = $"{hostName}/home/UpdateSmsBalance?encodedParameter={encodedParameter}";
            await GetRemoteStringAsync(updateUrl, ct);
        }

        returnResult.ExecutionStatus = jusibeResult.ExecutionStatus;
        returnResult.TelNos = jusibeResult.TelNos;
        returnResult.CostPerOrdinaryNo = pricePerSms;
        returnResult.CostPerMtnDndNo = pricePerSms;
        return returnResult;
    }

    public async Task<string> UpdateSmsCredentialsAsync(CancellationToken ct)
    {
        if (!InternetConnectivityHelper.CheckForInternetConnection())
        {
            return "Failed! Internet connection is required to update credential.";
        }

        var smsCredentials = await _teachersAndOthersService.GetCurrentSmsTokenAndKeyAndNewTokenAndKey(ct);
        if (string.IsNullOrWhiteSpace(smsCredentials))
        {
            return "You need to enable SMS first.";
        }

        var hostName = await ResolveNaitsHostNameAsync(ct);
        if (string.IsNullOrEmpty(hostName))
        {
            return "Failed! NAITS-ASMS online platform is temporarily unavailanle!!!";
        }

        try
        {
            // Uri.EscapeDataString keeps '+' in Base64 as %2B (required for ASP.NET query binding).
            var url = $"{hostName}/home/ChangeCredentials?smsCredential={Uri.EscapeDataString(smsCredentials)}";
            var response = await GetRemoteStringAsync(url, ct);
            if (response != null)
            {
                response = response.Replace("\"", string.Empty).Trim();
                if (string.Equals(response, "true", StringComparison.OrdinalIgnoreCase))
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                    return "Credentials updated Successfully";
                }

                _logger.LogWarning(
                    "ChangeCredentials rejected by NAITS host. Remote response: {Response}",
                    response.Length > 200 ? response[..200] : response);

                if (response == "000")
                {
                    return "Failed! These SMS credentials are not registered on the NAITS online platform (ourschoolonline.ng). "
                        + "Your local token/key do not match live online records. "
                        + "Use Submit System Information (or ask admin on 08035550938) to sync them, then retry.";
                }

                return "Failed! wrong credentials; Contact Administrator on 08035550938.";
            }

            return "Failed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update SMS credentials");
            return ex.InnerException != null ? $"Failed! {ex.InnerException.Message}" : $"Failed! {ex.Message}";
        }
    }

    public async Task<string> SendMultipleSmsExpressAsync(string userId, SmsInfoDto smsInfo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(smsInfo.Message)
            || string.IsNullOrWhiteSpace(smsInfo.SenderName)
            || smsInfo.RecipientNo == null
            || smsInfo.RecipientNo.Count == 0)
        {
            return "Failed! Message, sender name and at least one recipient are required.";
        }

        if (!InternetConnectivityHelper.CheckForInternetConnection())
        {
            return "Failed! Internet Connectivity is required.";
        }

        var recipients = smsInfo.RecipientNo
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Select(number => number.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var noOfRecipients = recipients.Count;
        var noOfSmsPages = smsInfo.NoOfSms <= 0 ? 1 : smsInfo.NoOfSms;
        var balanceStatus = await GetSmsBalanceStatusAsync(userId, noOfRecipients, noOfSmsPages, ct);
        if (!balanceStatus.StartsWith("Success", StringComparison.OrdinalIgnoreCase))
        {
            return balanceStatus;
        }

        var hostName = await ResolveNaitsHostNameAsync(ct);
        if (string.IsNullOrEmpty(hostName))
        {
            return "Failed! NAITS-ASMS online platform is temporarily unavailanle!!!";
        }

        var encodedParameter = WebUtility.UrlEncode(
            await _teachersAndOthersService.GetAndEncodeCredentialsAndNoOfSmsTimesPages(
                noOfRecipients,
                noOfSmsPages,
                ct));

        var updateUrl = $"{hostName}/home/UpdateSmsBalanceExpress?parameterEncoded={encodedParameter}";
        await GetRemoteStringAsync(updateUrl, ct);

        var executionStatus = string.Empty;
        var counter = 0;
        foreach (var eachNo in recipients)
        {
            counter++;
            var response = await SendJusibeSmsAsync(eachNo, smsInfo.SenderName, smsInfo.Message, ct);
            if (response.Status.StartsWith("SMS", StringComparison.OrdinalIgnoreCase))
            {
                executionStatus = response.Status;
                break;
            }

            if (response.Status is "Internet problem" or "Unknown error")
            {
                executionStatus = response.Status == "Internet problem"
                    ? "Either Internet problem or other Unknown error."
                    : "An Unknown error Occured.";
                break;
            }
        }

        if (counter == noOfRecipients && string.IsNullOrEmpty(executionStatus))
        {
            executionStatus = "Good! messages were sent successfully.";
        }

        return string.IsNullOrWhiteSpace(executionStatus)
            ? "Failed! Unable to complete the process."
            : executionStatus;
    }

    private async Task<string> GetSmsBalanceStatusAsync(string userId, float noOfRecipients, float noOfSmsPages, CancellationToken ct)
    {
        var balanceResponse = await GetSmsBalanceAsync(userId, ct);
        if (balanceResponse == "000")
        {
            return "Invalid Credential";
        }

        if (!balanceResponse.Contains(':', StringComparison.Ordinal))
        {
            return balanceResponse;
        }

        var parts = balanceResponse.Split(':');
        if (parts.Length < 2)
        {
            return balanceResponse;
        }

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var naitsPlatformSmsBalance)
            || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pricePerSms))
        {
            return balanceResponse;
        }

        var totalCostOfSms = noOfSmsPages * noOfRecipients * pricePerSms;
        var jusibeCredits = await GetJusibeCreditsAsync(ct);
        if (totalCostOfSms > naitsPlatformSmsBalance)
        {
            return $"Insufficient balance, The total charge for your SMS is = {totalCostOfSms}";
        }

        if ((noOfSmsPages * noOfRecipients) > jusibeCredits)
        {
            return "Insufficient SMS Platform Balance Contact NAITS admin on 08035550938.";
        }

        return $"Success:{naitsPlatformSmsBalance.ToString(CultureInfo.InvariantCulture)}:{pricePerSms.ToString(CultureInfo.InvariantCulture)}";
    }

    private async Task<MessageSentFeedbackDto> ConnectToJusibeAsync(SmsInfoDto smsInfo, CancellationToken ct)
    {
        var feedbackTelNos = new List<TelNumberDto>();
        var returnResult = new MessageSentFeedbackDto
        {
            ExecutionStatus = "Process has been completed successfully.",
            TelNos = feedbackTelNos,
        };

        foreach (var eachNo in smsInfo.RecipientNo)
        {
            var response = await SendJusibeSmsAsync(eachNo.Trim(), smsInfo.SenderName, smsInfo.Message, ct);
            if (!string.IsNullOrEmpty(response.MessageId))
            {
                var deliveryStatus = await GetJusibeDeliveryStatusAsync(response.MessageId, ct);
                feedbackTelNos.Add(new TelNumberDto
                {
                    TelNo = eachNo,
                    TelNoStatus = deliveryStatus == "Rejected" ? TelNoStatus.DndLine : TelNoStatus.OrdinaryNo,
                });
                continue;
            }

            switch (response.Status)
            {
                case "Invalid number":
                    feedbackTelNos.Add(new TelNumberDto { TelNo = eachNo, TelNoStatus = TelNoStatus.InvalidNo });
                    break;
                case "Insufficient credit":
                    returnResult.ExecutionStatus = "Contact NAITS-ASMS Admin to activate more SMS credit.";
                    return returnResult;
                case "Internet problem":
                    returnResult.ExecutionStatus = "Either Internet problem or other Unknown error.";
                    return returnResult;
                default:
                    if (response.Status.StartsWith("SMS", StringComparison.OrdinalIgnoreCase))
                    {
                        returnResult.ExecutionStatus = response.Status;
                    }
                    else
                    {
                        returnResult.ExecutionStatus = string.IsNullOrWhiteSpace(response.Status)
                            ? "Unknown error."
                            : response.Status;
                    }

                    return returnResult;
            }
        }

        returnResult.TelNos = feedbackTelNos;
        return returnResult;
    }

    private async Task<JusibeSendResponse> SendJusibeSmsAsync(string to, string from, string message, CancellationToken ct)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["user"] = JusibeUser,
            ["pass"] = JusibePass,
            ["to"] = to,
            ["from"] = from,
            ["message"] = message,
            ["type"] = "json",
        });

        using var client = _httpClientFactory.CreateClient();
        using var response = await client.PostAsync("https://jusibe.com/smsapi/send_sms", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return ParseJusibeSendResponse(body);
    }

    private async Task<string> GetJusibeDeliveryStatusAsync(string messageId, CancellationToken ct)
    {
        var url =
            $"https://jusibe.com/smsapi/delivery_status?user={WebUtility.UrlEncode(JusibeUser)}&pass={WebUtility.UrlEncode(JusibePass)}&message_id={WebUtility.UrlEncode(messageId)}";
        using var client = _httpClientFactory.CreateClient();
        var body = await client.GetStringAsync(url, ct);
        using var document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("status", out var status))
        {
            return status.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private async Task<float> GetJusibeCreditsAsync(CancellationToken ct)
    {
        var url =
            $"https://jusibe.com/smsapi/balance?user={WebUtility.UrlEncode(JusibeUser)}&pass={WebUtility.UrlEncode(JusibePass)}";
        using var client = _httpClientFactory.CreateClient();
        var body = await client.GetStringAsync(url, ct);
        using var document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("sms_credits", out var credits)
            && credits.TryGetDouble(out var value))
        {
            return (float)value;
        }

        return 0f;
    }

    private static JusibeSendResponse ParseJusibeSendResponse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return new JusibeSendResponse
            {
                MessageId = root.TryGetProperty("message_id", out var messageId) ? messageId.GetString() : null,
                Status = root.TryGetProperty("status", out var status) ? status.GetString() ?? string.Empty : string.Empty,
            };
        }
        catch (JsonException)
        {
            return new JusibeSendResponse { Status = body };
        }
    }

    private static async Task<string?> ResolveNaitsHostNameAsync(CancellationToken ct)
    {
        foreach (var hostName in new[]
                 {
                     "http://www.ourschoolonline.ng",
                     "https://www.ourschoolonline.ng",
                     "http://ourschoolonline.ng",
                     "https://ourschoolonline.ng",
                 })
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                using var response = await httpClient.GetAsync(hostName, ct);
                if (response.IsSuccessStatusCode)
                {
                    return hostName;
                }
            }
            catch
            {
                // try next host
            }
        }

        return null;
    }

    private async Task<string> GetRemoteStringAsync(string url, CancellationToken ct)
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        return await client.GetStringAsync(url, ct);
    }

    private sealed class JusibeSendResponse
    {
        public string? MessageId { get; init; }
        public string Status { get; init; } = string.Empty;
    }
}
