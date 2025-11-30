using Shouldly;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

[ClassDataSource<ShardingFixture>]
public class ClusterMembershipStoreTests(ShardingFixture fixture) {

    [Test]
    public async Task GetMembers() {
        var members = await fixture.ClusterMembershipStore!.GetMembersAsync();
        members.ShouldContain(Environment.MachineName);
    }
}