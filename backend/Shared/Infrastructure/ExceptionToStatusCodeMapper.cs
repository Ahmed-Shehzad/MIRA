using System.Net;

namespace HiveOrders.Api.Shared.Infrastructure;

public sealed class ExceptionToStatusCodeMapper : IExceptionToStatusCodeMapper
{
    public HttpStatusCode Map(Exception exception)
    {
        return exception switch
        {
            ArgumentNullException => HttpStatusCode.BadRequest,
            ArgumentException => HttpStatusCode.BadRequest,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            KeyNotFoundException => HttpStatusCode.NotFound,
            InvalidOperationException => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError
        };
    }
}
