using Shouldly;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

[ClassDataSource<ShardingFixture>]
public class ClusterMembershipStoreTests(ShardingFixture fixture) {

    [Test]
    public async Task GetMembers() {
        var members = await fixture.ClusterMembershipStore!.GetMembersAsync();
        members.ShouldContain(Environment.MachineName);
    }

    [Test]
    [DependsOn(nameof(GetMembers))]
    public async Task HeartbeatWorks() {
        await Task.Delay(TimeSpan.FromSeconds(fixture.ClusterMembershipOptions!.ExpirationTimeoutSeconds + 2));
        var membersAfterDelay = await fixture.ClusterMembershipStore!.GetMembersAsync();
        membersAfterDelay.ShouldContain(Environment.MachineName);
    }
}