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
    [Retry(3)]
    public async Task MembersLeasesAreRenewed() {
        var alternativeClusterMembershipStore = fixture.CreateNewClusterMembershipStore("another-machine");
        try {
            await alternativeClusterMembershipStore.RegisterMemberAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(fixture.ClusterMembershipOptions!.HeartbeatIntervalSeconds));
            fixture.Members!.ShouldBe(new[]{Environment.MachineName, "another-machine"}.Order());
        } finally {
            await alternativeClusterMembershipStore.UnregisterMemberAsync("another-machine", CancellationToken.None);
        }
    }


    [Test]
    [DependsOn(nameof(MembersLeasesAreRenewed))]
    public async Task MembersAreExpired() {
        var alternativeClusterMembershipStore = fixture.CreateNewClusterMembershipStore("another-machine");
        try {
            await alternativeClusterMembershipStore.RegisterMemberAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(fixture.ClusterMembershipOptions!.ExpirationTimeoutSeconds + 2));
            fixture.Members!.ShouldBe([Environment.MachineName]);
        } finally {
            await alternativeClusterMembershipStore.UnregisterMemberAsync("another-machine", CancellationToken.None);
        }
    }
}