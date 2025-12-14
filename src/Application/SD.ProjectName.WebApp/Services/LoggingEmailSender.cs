using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;

namespace SD.ProjectName.WebApp.Services
{
    public class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;

        public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            _logger.LogInformation("Sending email to {Email} with subject {Subject}. Body: {Body}", email, subject, htmlMessage);
            return Task.CompletedTask;
        }
    }
}
