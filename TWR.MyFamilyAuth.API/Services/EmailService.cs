using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using TWR.MyFamilyAuth.API.Models;

namespace TWR.MyFamilyAuth.API.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger   = logger;
    }

    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetToken)
    {
        var subject = "MyFamilyAuth — Password Reset";
        var body    = $"<p>Hi {WebUtility.HtmlEncode(toName)},</p>" +
                      $"<p>Your password reset code is: <strong>{resetToken}</strong></p>" +
                      $"<p>This code expires in 15 minutes.</p>" +
                      $"<p>If you did not request this, ignore this email.</p>";
        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendWelcomeAsync(string toEmail, string toName)
    {
        var subject = "Welcome to TWR MyApps";
        var body    = $"<p>Hi {WebUtility.HtmlEncode(toName)},</p>" +
                      $"<p>Your account has been created. Sign in at the MyFamilyAuth portal.</p>";
        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendTwoFactorCodeAsync(string toEmail, string toName, string otpCode, string appName)
    {
        var subject = $"{appName} — Your Sign-In Code";
        var body    = $"<p>Hi {System.Net.WebUtility.HtmlEncode(toName)},</p>" +
                      $"<p>Your sign-in verification code for <strong>{System.Net.WebUtility.HtmlEncode(appName)}</strong> is:</p>" +
                      $"<p style=\"font-size:2rem;letter-spacing:0.3rem;font-weight:bold;color:#1d4ed8\">{otpCode}</p>" +
                      $"<p>This code expires in <strong>10 minutes</strong>.</p>" +
                      $"<p>If you did not attempt to sign in, you can safely ignore this email.</p>";
        await SendAsync(toEmail, toName, subject, body);
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        if (string.IsNullOrEmpty(_settings.SmtpPassword))
        {
            _logger.LogWarning("Email not sent — SMTP password not configured. To: {Email}, Subject: {Subject}", toEmail, subject);
            return;
        }
        try
        {
            using var client = new SmtpClient(_settings.SmtpServer, _settings.SmtpPort)
            {
                EnableSsl             = true,
                UseDefaultCredentials = false,
                Credentials           = new NetworkCredential(_settings.SmtpUser, _settings.SmtpPassword)
            };
            using var msg = new MailMessage();
            msg.From    = new MailAddress(_settings.FromAddress, _settings.FromName);
            msg.To.Add(new MailAddress(toEmail, toName));
            msg.Subject    = subject;
            msg.Body       = htmlBody;
            msg.IsBodyHtml = true;
            await client.SendMailAsync(msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
        }
    }
}
