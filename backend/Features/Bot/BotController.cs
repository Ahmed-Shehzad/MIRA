using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;

namespace HiveOrders.Api.Features.Bot;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/bot")]
[AllowAnonymous]
public class BotController : ControllerBase
{
    private readonly IBotFrameworkHttpAdapter _adapter;
    private readonly IBot _bot;

    public BotController(IBotFrameworkHttpAdapter adapter, IBot bot)
    {
        _adapter = adapter;
        _bot = bot;
    }

    [HttpPost]
    public async Task PostAsync(CancellationToken cancellationToken)
    {
        await _adapter.ProcessAsync(Request, Response, _bot, cancellationToken);
    }
}
