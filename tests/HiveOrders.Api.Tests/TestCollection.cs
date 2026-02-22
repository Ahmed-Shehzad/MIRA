using Xunit;

namespace HiveOrders.Api.Tests;

[CollectionDefinition("Postgres")]
public class TestCollection : ICollectionFixture<PostgreSqlFixture>
{
}
