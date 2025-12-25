using Eventuous.Subscriptions.Checkpoints.Sharding;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.SqlServer.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

public class ShardingFixture() : StoreFixtureBase<MsSqlContainer>(LogLevel.Information) {
    public readonly string Owner = "TestMachine";
    public readonly string SchemaName = GetSchemaName();
    public IClusterMembershipStore? ClusterMembershipStore { get; private set; }
    public ClusterMembershipOptions ClusterMembershipOptions { get; private set; } = new ClusterMembershipOptions {
        HeartbeatIntervalSeconds = 1,
        ExpirationTimeoutSeconds = 5,
        RefreshIntervalSeconds   = 2
    };
    public string[]? Members { get; private set; }
    public SqlServerLeaseManager? SqlServerLeaseManager { get; private set; }

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventuousSqlServer(Container.GetConnectionString(), SchemaName, true, owner: Owner);
        services.AddEventStore<SqlServerStore>();
        services.AddSqlServerClusterMembershipStore(ClusterMembershipOptions);
        services.AddSqlServerShardLeaseManager(1, 2);
    }

    protected override MsSqlContainer CreateContainer() => SqlContainer.Create();

    protected override void GetDependencies(IServiceProvider provider) {
        base.GetDependencies(provider);
        ClusterMembershipStore = provider.GetRequiredService<IClusterMembershipStore>();
        ClusterMembershipStore.MembershipChanged += StoreMembers;
        SqlServerLeaseManager = provider.GetRequiredService<SqlServerLeaseManager>();
    }

    public ClusterMembershipStore CreateNewClusterMembershipStore(string owner, int expirationTimeoutSeconds = 5) {
        var store = new ClusterMembershipStore(
            Provider.GetRequiredService<SqlServerStoreOptions>() with { Owner = owner },
            ClusterMembershipOptions with { ExpirationTimeoutSeconds = expirationTimeoutSeconds },
            NullLoggerFactory.Instance
        );
        return store;
    }

    private void StoreMembers(object? sender, MembershipChangedEventArgs e) {
        Members = e.Members;
    }
}
