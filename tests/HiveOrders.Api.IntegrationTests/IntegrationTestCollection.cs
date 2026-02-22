using Xunit;

namespace HiveOrders.Api.IntegrationTests;

[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
}
