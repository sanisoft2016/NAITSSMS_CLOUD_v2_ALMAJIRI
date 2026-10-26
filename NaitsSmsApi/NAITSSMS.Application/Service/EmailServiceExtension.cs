using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;

namespace NAITSSMS.Application.Service;

/// <summary>
/// Extends <see cref="EmailService"/> with SMS activation and public contact enquiry helpers.
/// </summary>
public sealed class EmailServiceExtension : IEmailServiceExtension
{
    private readonly IEmailService _emailService;
    private readonly ITeachersAndOthersService _teachersAndOthersService;

    public EmailServiceExtension(
        IEmailService emailService,
        ITeachersAndOthersService teachersAndOthersService)
    {
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _teachersAndOthersService = teachersAndOthersService ?? throw new ArgumentNullException(nameof(teachersAndOthersService));
    }

    public async Task<object> PostSystemInformation(string userIdOrSchoolId, CancellationToken ct)
    {
        const string failureMessage =
            "Un-able to complete the process. Kindly mail - info@naits.com.ng or call 08035550938.";

        var credentials = _emailService.GetCredentials();
        if (credentials == null || credentials.Ports == null || credentials.Ports.Count == 0)
        {
            return new { message = "Failed! Error in the email parameters of the config file." };
        }

        var keyAndToken = await _teachersAndOthersService.GetSmsTokenAndKeyAndSystemIdAndUserNameAndSchoolName(ct);
        if (string.IsNullOrWhiteSpace(keyAndToken))
        {
            return new { message = "School registration profile was not found for the current user." };
        }

        var item = keyAndToken.Split(':').ToList();
        if (item.Count < 7)
        {
            return new { message = failureMessage };
        }

        var htmlBody = BuildSmsActivationEmailBody(item);
        var subject = DateTime.Now + " as Date and Time, SMS Activation Security Info";
        var messageString = _emailService.SendEmailWithPortFallback(subject, htmlBody);

        if (messageString.StartsWith("Your message", StringComparison.OrdinalIgnoreCase)
            || messageString.StartsWith("Good!", StringComparison.OrdinalIgnoreCase))
        {
            return new { message = "Good! The message was successfully sent" };
        }

        if (messageString.StartsWith("Failed!", StringComparison.OrdinalIgnoreCase))
        {
            return new { message = messageString };
        }

        return new { message = failureMessage };
    }

    public Task<ContactEnquiryResult> SendContactEnquiry(ContactUsDto model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var subjectBase = string.IsNullOrWhiteSpace(model.Subject)
            ? "NAITS-ASMS website enquiry"
            : model.Subject.Trim();
        var subject = $"{subjectBase}, Date and Time: {DateTime.Now}";
        var htmlBody = BuildContactEnquiryBody(model);

        var message = _emailService.SendEmailWithPortFallback(
            subject,
            htmlBody,
            model.Email,
            model.FullName);

        var success = message.StartsWith("Your message", StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(new ContactEnquiryResult
        {
            Status = success ? "Success" : "Failure",
            Message = message,
        });
    }

    private static string BuildContactEnquiryBody(ContactUsDto model)
    {
        var school = string.IsNullOrWhiteSpace(model.School) ? "N/A" : model.School.Trim();
        return
            "Sender's name: " + System.Net.WebUtility.HtmlEncode(model.FullName) + ", <br>" +
            "Email Address: " + System.Net.WebUtility.HtmlEncode(model.Email) + ", <br>" +
            "School: " + System.Net.WebUtility.HtmlEncode(school) + ", <br>" +
            "Message: " + System.Net.WebUtility.HtmlEncode(model.Message).Replace("\n", "<br>");
    }

    private static string BuildSmsActivationEmailBody(IReadOnlyList<string> item)
    {
        return "Token: " + item[0] + ", <br>" +
               "Key: " + item[1] + ", <br>" +
               "System Id: " + item[2] + "<br>" +
               "userName: " + item[3] + "<br>" +
               "schoolName: " + item[4] +
               "<br>" + "Telephone: " + item[5] + "<br>" +
               "School Address: " + item[6];
    }
}
