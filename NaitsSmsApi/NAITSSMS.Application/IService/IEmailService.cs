using NAITSSMS.Application.DTOs;

namespace NAITSSMS.Application.IService;

public interface IEmailService
{
    Service.EmailCredentials GetCredentials();
    string SendEmail(System.Net.Mail.MailMessage msg, Service.EmailCredentials credentials, int portNumber);

    /// <summary>
    /// Sends using the first configured port, then retries with the second port on failure.
    /// Returns a user-facing status message.
    /// </summary>
    string SendEmailWithPortFallback(
        string subject,
        string htmlBody,
        string? replyToEmail = null,
        string? replyToDisplayName = null);
}
