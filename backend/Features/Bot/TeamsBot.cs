using HiveOrders.Api.Features.OrderRounds;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace HiveOrders.Api.Features.Bot;

public class TeamsBot : ActivityHandler
{
    private readonly IOrderRoundHandler _orderRoundHandler;
    private readonly IBotUserResolver _userResolver;
    private readonly IBotLinkService _linkService;

    public TeamsBot(IOrderRoundHandler orderRoundHandler, IBotUserResolver userResolver, IBotLinkService linkService)
    {
        _orderRoundHandler = orderRoundHandler;
        _userResolver = userResolver;
        _linkService = linkService;
    }

    protected override async Task OnMessageActivityAsync(
        ITurnContext<IMessageActivity> turnContext,
        CancellationToken cancellationToken)
    {
        var text = (turnContext.Activity.Text ?? "").Trim().ToLowerInvariant();
        var externalId = turnContext.Activity.From?.AadObjectId ?? turnContext.Activity.From?.Id ?? "";
        var userId = await _userResolver.ResolveUserIdAsync(externalId, cancellationToken);

        if (userId == null)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text(
                "Your Teams account is not linked. Sign in to the web app and link your account in Settings to use the bot."),
                cancellationToken);
            return;
        }

        if (text.StartsWith("link "))
        {
            var code = text["link ".Length..].Trim();
            var linked = await _userResolver.ResolveUserIdAsync(externalId, cancellationToken) != null;
            if (linked)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Your account is already linked."), cancellationToken);
                return;
            }

            var success = await _linkService.ConsumeLinkCodeAsync(code, externalId, cancellationToken);
            await turnContext.SendActivityAsync(MessageFactory.Text(
                success ? "Account linked successfully! You can now use 'list rounds'." : "Invalid or expired code. Get a new code from the web app."),
                cancellationToken);
            return;
        }

        if (text.StartsWith("list rounds") || text == "list" || text == "rounds")
        {
            var rounds = await _orderRoundHandler.GetMyOrderRoundsAsync(userId, cancellationToken);
            var reply = rounds.Count == 0
                ? "You have no order rounds."
                : string.Join("\n", rounds.Select(r => $"- {r.RestaurantName} (deadline: {r.Deadline:g})"));
            await turnContext.SendActivityAsync(MessageFactory.Text(reply), cancellationToken);
            return;
        }

        if (text.StartsWith("help") || text == "?")
        {
            await turnContext.SendActivityAsync(MessageFactory.Text(
                "Commands:\n" +
                "- **list rounds** - Show your order rounds\n" +
                "- **help** - Show this message"), cancellationToken);
            return;
        }

        await turnContext.SendActivityAsync(
            MessageFactory.Text("Hi! Say 'list rounds' to see your orders, or 'help' for commands."),
            cancellationToken);
    }

    protected override async Task OnMembersAddedAsync(
        IList<ChannelAccount> membersAdded,
        ITurnContext<IConversationUpdateActivity> turnContext,
        CancellationToken cancellationToken)
    {
        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient?.Id)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("Welcome! Say 'help' for available commands."),
                    cancellationToken);
            }
        }
    }
}
