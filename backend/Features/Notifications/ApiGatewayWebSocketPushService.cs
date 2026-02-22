using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace HiveOrders.Api.Features.Notifications;

public interface IApiGatewayWebSocketPushService
{
    Task PushToUserAsync(int tenantId, string userId, string type, string title, string? body, CancellationToken cancellationToken = default);
}

public class ApiGatewayWebSocketPushService : IApiGatewayWebSocketPushService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiGatewayWebSocketPushService> _logger;

    public ApiGatewayWebSocketPushService(IConfiguration configuration, ILogger<ApiGatewayWebSocketPushService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task PushToUserAsync(int tenantId, string userId, string type, string title, string? body, CancellationToken cancellationToken = default)
    {
        var tableName = _configuration["Realtime:ConnectionsTableName"];
        var endpoint = _configuration["Realtime:WebSocketEndpoint"];
        if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(endpoint))
            return;

        var userKey = $"{tenantId}#{userId}";
        using var dynamo = new AmazonDynamoDBClient();
        var query = new QueryRequest
        {
            TableName = tableName,
            IndexName = "gsi-user",
            KeyConditionExpression = "userKey = :uk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":uk", new AttributeValue(userKey) } },
        };

        QueryResponse response;
        try
        {
            response = await dynamo.QueryAsync(query, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query WebSocket connections for user {UserId}", userId);
            return;
        }

        var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { type, title, body });
        var endpointUri = new Uri(endpoint.StartsWith("http") ? endpoint : $"https://{endpoint}");

        foreach (var item in response.Items)
        {
            if (!item.TryGetValue("connectionId", out var connAttr))
                continue;
            var connectionId = connAttr.S;
            if (string.IsNullOrEmpty(connectionId))
                continue;

            try
            {
                var config = new AmazonApiGatewayManagementApiConfig { ServiceURL = endpointUri.ToString() };
                using var client = new AmazonApiGatewayManagementApiClient(config);
                await client.PostToConnectionAsync(
                    new PostToConnectionRequest { ConnectionId = connectionId, Data = new MemoryStream(payload) },
                    cancellationToken);
            }
            catch (GoneException)
            {
                // Connection closed, will be cleaned up by $disconnect
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push to connection {ConnectionId}", connectionId);
            }
        }
    }
}
