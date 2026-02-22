namespace HiveOrders.Api.Shared.Infrastructure;

/// <summary>Service for sending exception reports (e.g. via email) to development team.</summary>
public interface IExceptionReportService
{
    /// <summary>Sends an exception report asynchronously. Does not throw; failures are logged.</summary>
    Task SendReportAsync(ExceptionReport report, CancellationToken cancellationToken = default);
}
