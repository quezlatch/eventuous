using Eventuous.SqlServer;
using Eventuous.SqlServer.Subscriptions.Sharding;
using Shouldly;

namespace Eventuous.Tests.SqlServer.Subscriptions.Sharding;

[ClassDataSource<ShardingFixture>]
public class RenewLease(ShardingFixture fixture) {
    
    [Test]
    public async Task AllShardsAreLeasedToThisMachine() {
        await Task.Delay(5000);
        await using var connection = await ConnectionFactory.GetConnection(fixture.Container.GetConnectionString(), CancellationToken.None);
        using var cmd = connection.CreateCommand();
        cmd.CommandType = System.Data.CommandType.Text;
        cmd.CommandText =
            $"SELECT * FROM {fixture.SchemaName}.ShardLeases";
        var reader = await cmd.ExecuteReaderAsync();
        var machineName = Environment.MachineName;
        var now = DateTime.UtcNow;
        while (await reader.ReadAsync()) {
            var id = reader.GetInt32(0);
            var owner = reader.IsDBNull(1) ? null : reader.GetString(1);
            DateTime? expiration = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
            owner.ShouldBe(machineName);
            expiration.ShouldNotBeNull();
            expiration.Value.ShouldBeGreaterThan(now);
        }
    }

}