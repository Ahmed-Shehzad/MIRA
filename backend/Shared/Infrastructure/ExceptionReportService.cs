using System.Text;
using System.Text.Json;

namespace HiveOrders.Api.Shared.Infrastructure;

public class ExceptionReportService : IExceptionReportService
{
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExceptionReportService> _logger;

    private const string AuthorizationHeader = "Authorization";
    private const string CookieHeader = "Cookie";
    private const int MaxBodyPreviewLength = 2000;

    public ExceptionReportService(
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<ExceptionReportService> logger)
    {
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendReportAsync(ExceptionReport report, CancellationToken cancellationToken = default)
    {
        var recipient = _configuration["ErrorReporting:DevEmailAddress"];
        if (string.IsNullOrWhiteSpace(recipient))
        {
            _logger.LogDebug("ErrorReporting:DevEmailAddress not configured; skipping exception email");
            return;
        }

        try
        {
            var subject = $"[{report.Environment}] {report.ExceptionType}: {report.Message}";
            if (subject.Length > 200)
                subject = subject[..197] + "...";

            var body = BuildEmailBody(report);
            await _emailService.SendEmailAsync(recipient!, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send exception report email");
        }
    }

    private static string BuildEmailBody(ExceptionReport report)
    {
        var sb = new StringBuilder();
        sb.Append("<pre style='font-family:monospace;font-size:12px;white-space:pre-wrap;'>");
        sb.AppendLine("=== EXCEPTION REPORT ===");
        sb.AppendLine($"Environment: {report.Environment}");
        sb.AppendLine($"OccurredAt: {report.OccurredAt:O}");
        sb.AppendLine();
        sb.AppendLine("--- Exception ---");
        sb.AppendLine($"Type: {report.ExceptionType}");
        sb.AppendLine($"Message: {report.Message}");
        sb.AppendLine();
        sb.AppendLine("--- Stack Trace ---");
        sb.AppendLine(report.StackTrace);
        if (!string.IsNullOrEmpty(report.InnerExceptionReport))
        {
            sb.AppendLine();
            sb.AppendLine("--- Inner Exception ---");
            sb.AppendLine(report.InnerExceptionReport);
        }
        sb.AppendLine();
        sb.AppendLine("--- Request ---");
        sb.AppendLine($"Method: {report.RequestMethod}");
        sb.AppendLine($"Path: {report.RequestPath}");
        sb.AppendLine($"QueryString: {report.RequestQueryString}");
        sb.AppendLine("Headers:");
        foreach (var (k, v) in report.RequestHeaders)
            sb.AppendLine($"  {k}: {SanitizeHeaderValue(k, v)}");
        if (!string.IsNullOrEmpty(report.RequestBodyPreview))
        {
            sb.AppendLine();
            sb.AppendLine("Body (preview):");
            sb.AppendLine(EscapeHtml(report.RequestBodyPreview));
        }
        sb.AppendLine();
        sb.AppendLine("--- Serialized Report (JSON) ---");
        sb.AppendLine(EscapeHtml(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })));
        sb.Append("</pre>");
        return sb.ToString();
    }

    private static string SanitizeHeaderValue(string headerName, string value)
    {
        if (string.Equals(headerName, AuthorizationHeader, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(headerName, CookieHeader, StringComparison.OrdinalIgnoreCase))
            return "[REDACTED]";
        return EscapeHtml(value.Length > 500 ? value[..500] + "..." : value);
    }

    private static string EscapeHtml(string s)
    {
        return s
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    public static string TruncateBody(string? body, int maxLength = MaxBodyPreviewLength)
    {
        if (string.IsNullOrEmpty(body)) return string.Empty;
        if (body.Length <= maxLength) return body;
        return body[..maxLength] + $"\n... [truncated, total {body.Length} chars]";
    }
}
