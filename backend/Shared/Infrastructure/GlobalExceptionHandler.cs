using System.Net;
using System.Text;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HiveOrders.Api.Shared.Infrastructure;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IExceptionReportService _reportService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IExceptionReportService reportService,
        IHostEnvironment environment,
        ILogger<GlobalExceptionHandler> logger)
    {
        _reportService = reportService;
        _environment = environment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var report = await BuildExceptionReportAsync(httpContext, exception, cancellationToken);
        _ = _reportService.SendReportAsync(report, cancellationToken);

        var problemDetails = CreateProblemDetails(exception);
        httpContext.Response.StatusCode = problemDetails.Status ?? (int)HttpStatusCode.InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private ProblemDetails CreateProblemDetails(Exception exception)
    {
        var statusCode = exception switch
        {
            ArgumentNullException => HttpStatusCode.BadRequest,
            ArgumentException => HttpStatusCode.BadRequest,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            KeyNotFoundException => HttpStatusCode.NotFound,
            InvalidOperationException => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{(int)statusCode}",
            Title = exception.GetType().Name,
            Status = (int)statusCode,
            Detail = _environment.IsDevelopment()
                ? exception.Message
                : "An error occurred processing your request.",
            Instance = null
        };

        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
            if (exception.InnerException != null)
                problemDetails.Extensions["innerException"] = exception.InnerException.ToString();
        }

        return problemDetails;
    }

    private async Task<ExceptionReport> BuildExceptionReportAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var request = httpContext.Request;
        var headers = new Dictionary<string, string>();
        foreach (var (key, value) in request.Headers)
            headers[key] = value.ToString();

        string? bodyPreview = null;
        if (request.ContentLength.HasValue && request.ContentLength > 0 && request.ContentLength < 100_000)
        {
            try
            {
                request.EnableBuffering();
                request.Body.Position = 0;
                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync(cancellationToken);
                request.Body.Position = 0;
                bodyPreview = ExceptionReportService.TruncateBody(body);
            }
            catch
            {
                bodyPreview = "[Could not read request body]";
            }
        }

        var innerReport = exception.InnerException != null
            ? FormatExceptionRecursive(exception.InnerException)
            : null;

        return new ExceptionReport
        {
            Environment = _environment.EnvironmentName,
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            StackTrace = exception.StackTrace ?? string.Empty,
            InnerExceptionReport = innerReport,
            RequestMethod = request.Method,
            RequestPath = request.Path,
            RequestQueryString = request.QueryString.HasValue ? request.QueryString.ToString() : string.Empty,
            RequestHeaders = headers,
            RequestBodyPreview = bodyPreview,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private static string FormatExceptionRecursive(Exception ex)
    {
        var sb = new StringBuilder();
        var current = ex;
        var depth = 0;
        while (current != null && depth < 5)
        {
            if (depth > 0) sb.AppendLine();
            sb.AppendLine($"[Inner {depth}] {current.GetType().FullName}: {current.Message}");
            sb.AppendLine(current.StackTrace ?? "");
            current = current.InnerException;
            depth++;
        }
        return sb.ToString();
    }
}
