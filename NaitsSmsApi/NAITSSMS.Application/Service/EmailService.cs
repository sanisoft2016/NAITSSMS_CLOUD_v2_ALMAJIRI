using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using NAITSSMS.Application.IService;

namespace NAITSSMS.Application.Service
{
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _settings;
        private readonly EmailCredentials _emailCredentials;

        public EmailService(
            IOptions<SmtpSettings> settings,
            IOptions<EmailCredentials> emailCredentials)
        {
            _settings = settings?.Value ?? new SmtpSettings();
            _emailCredentials = emailCredentials?.Value ?? new EmailCredentials();
        }

        public EmailCredentials GetCredentials()
        {
            // From and UserName are both the mailbox. Password comes only from the
            // EmailCredentials__Password env var (K8s Secret) or user-secrets.
            if (string.IsNullOrWhiteSpace(_emailCredentials.UserName))
                _emailCredentials.UserName = _emailCredentials.From;
            if (string.IsNullOrWhiteSpace(_emailCredentials.From))
                _emailCredentials.From = _emailCredentials.UserName;
            return _emailCredentials;
        }

        public string SendEmail(MailMessage msg, EmailCredentials credentials, int portNumber)
        {
            string messageString;
            using (msg)
            {
                using var sc = new SmtpClient(credentials.HostName, portNumber)
                {
                    EnableSsl = credentials.EnableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Timeout = 30000,
                    Credentials = new NetworkCredential(credentials.UserName, credentials.Password),
                };
                try
                {
                    sc.Send(msg);
                    messageString = "success";
                }
                catch (Exception ex)
                {
                    // Preserve SMTP detail (e.g. spam filter) for callers that surface it.
                    messageString = "failed: " + (ex.InnerException?.Message ?? ex.Message);
                }
            }
            return messageString;
        }

        public string SendEmailWithPortFallback(
            string subject,
            string htmlBody,
            string? replyToEmail = null,
            string? replyToDisplayName = null)
        {
            const string failureMessage =
                "Unable to complete the process. Kindly mail info@naits.com.ng or call 08035550938.";

            var credentials = GetCredentials();
            if (credentials == null
                || string.IsNullOrWhiteSpace(credentials.From)
                || string.IsNullOrWhiteSpace(credentials.To)
                || credentials.Ports == null
                || credentials.Ports.Count == 0)
            {
                return "Failed! Error in the email parameters of the config file.";
            }

            var firstResult = SendOnce(credentials, subject, htmlBody, replyToEmail, replyToDisplayName, credentials.Ports[0]);
            if (IsSuccess(firstResult))
            {
                return "Your message has successfully been sent.";
            }

            if (credentials.Ports.Count > 1)
            {
                var retrySubject = string.IsNullOrWhiteSpace(subject)
                    ? $"NAITS-ASMS enquiry, Date and Time: {DateTime.Now}"
                    : subject;
                var secondResult = SendOnce(
                    credentials,
                    retrySubject,
                    htmlBody,
                    replyToEmail,
                    replyToDisplayName,
                    credentials.Ports[1]);
                if (IsSuccess(secondResult))
                {
                    return "Your message has successfully been sent.";
                }
            }

            return failureMessage;
        }

        private string SendOnce(
            EmailCredentials credentials,
            string subject,
            string htmlBody,
            string? replyToEmail,
            string? replyToDisplayName,
            string portText)
        {
            if (!int.TryParse(portText, out var port))
            {
                return "failed: Invalid SMTP port configuration.";
            }

            var mailMessage = new MailMessage(credentials.From, credentials.To)
            {
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
            };

            if (!string.IsNullOrWhiteSpace(replyToEmail))
            {
                try
                {
                    mailMessage.ReplyToList.Add(
                        string.IsNullOrWhiteSpace(replyToDisplayName)
                            ? new MailAddress(replyToEmail)
                            : new MailAddress(replyToEmail, replyToDisplayName));
                }
                catch
                {
                    // Ignore invalid reply-to; still send the enquiry.
                }
            }

            return SendEmail(mailMessage, credentials, port);
        }

        private static bool IsSuccess(string result)
            => result.StartsWith("success", StringComparison.OrdinalIgnoreCase);
    }

    public class SmtpSettings
    {
        public string Host { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool EnableSsl { get; set; }
    }

    public class EmailCredentials
    {
        public string HostName { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool EnableSsl { get; set; }
        public List<string> Ports { get; set; } = [];
    }
}
