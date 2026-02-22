namespace HiveOrders.Api.Shared.Infrastructure;

public sealed record ExceptionReport
{
    public required string Environment { get; init; }
    public required string ExceptionType { get; init; }
    public required string Message { get; init; }
    public required string StackTrace { get; init; }
    public string? InnerExceptionReport { get; init; }
    public required string RequestMethod { get; init; }
    public required string RequestPath { get; init; }
    public required string RequestQueryString { get; init; }
    public IReadOnlyDictionary<string, string> RequestHeaders { get; init; } = new Dictionary<string, string>();
    public string? RequestBodyPreview { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}
