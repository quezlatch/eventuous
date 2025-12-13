using Shouldly;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

[ClassDataSource<ShardingFixture>]
public class ClusterMembershipStoreTests(ShardingFixture fixture) {
    [Test]
    public async Task GetMembers() {
        fixture.Members!.ShouldContain(Environment.MachineName);
    }

    [Test]
    [DependsOn(nameof(GetMembers))]
    public async Task HeartbeatWorks() {
        var alternativeClusterMembershipStore = fixture.CreateNewClusterMembershipStore("second");
        await alternativeClusterMembershipStore.RegisterMemberAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(fixture.ClusterMembershipOptions!.HeartbeatIntervalSeconds + 2));
        fixture.Members!.ShouldBe([Environment.MachineName, "second"]);
    }
}