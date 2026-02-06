using Eventuous.SqlServer.Subscriptions;
using Eventuous.SqlServer.Subscriptions.Sharding;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.MsSql;

namespace Eventuous.Tests.SqlServer.Subscriptions.Sharding;

[ClassDataSource<ShardingFixture>]
public class SubscribeToAll(ShardingFixture fixture)
    : SubscribeToAllBase<MsSqlContainer, SqlServerAllStreamShardingSubscription, SqlServerAllStreamShardingSubscriptionOptions, SqlServerCheckpointStore>(
        fixture
    ) {
    [Test]
    public async Task SqlServer_ShouldConsumeProducedEvents(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEvents(cancellationToken);
    }

    [Test]
    public async Task SqlServer_ShouldConsumeProducedEventsWhenRestarting(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEventsWhenRestarting(cancellationToken);
    }

    [Test]
    public async Task SqlServer_ShouldUseExistingCheckpoint(CancellationToken cancellationToken) {
        await ShouldUseExistingCheckpoint(cancellationToken);
    }
}