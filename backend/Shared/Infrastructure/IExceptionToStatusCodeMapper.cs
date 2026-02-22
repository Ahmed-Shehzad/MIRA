using System.Net;

namespace HiveOrders.Api.Shared.Infrastructure;

public interface IExceptionToStatusCodeMapper
{
    HttpStatusCode Map(Exception exception);
}
