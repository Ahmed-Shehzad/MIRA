using System.Net;
using System.Net.Mail;

namespace HiveOrders.Api.Shared.Infrastructure;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Email:SmtpHost"] ?? "localhost";
        var port = int.Parse(_configuration["Email:SmtpPort"] ?? "1025");
        var from = _configuration["Email:FromAddress"] ?? "noreply@hive.local";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = false,
            Credentials = new NetworkCredential()
        };

        using var message = new MailMessage(from, to, subject, body) { IsBodyHtml = true };
        await client.SendMailAsync(message, cancellationToken);
    }
}
