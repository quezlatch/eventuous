using Eventuous.Subscriptions.Checkpoints.Sharding;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.SqlServer.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

public class ShardingFixture() : StoreFixtureBase<MsSqlContainer>(LogLevel.Information) {
    readonly string _schemaName = GetSchemaName();
    public IClusterMembershipStore? ClusterMembershipStore { get; private set; }
    public ClusterMembershipOptions ClusterMembershipOptions { get; private set; } = new ClusterMembershipOptions {
        HeartbeatIntervalSeconds = 1,
        ExpirationTimeoutSeconds = 5,
        RefreshIntervalSeconds     = 2,
        MachineName              = Environment.MachineName
    };
    public string[]? Members { get; private set; }

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventuousSqlServer(Container.GetConnectionString(), _schemaName, true);
        services.AddEventStore<SqlServerStore>();
        services.AddSqlServerClusterMembershipStore(ClusterMembershipOptions);
    }

    protected override MsSqlContainer CreateContainer() => SqlContainer.Create();

    protected override void GetDependencies(IServiceProvider provider) {
        base.GetDependencies(provider);
        ClusterMembershipStore = provider.GetRequiredService<IClusterMembershipStore>();
        ClusterMembershipStore.MembershipChanged += (s, e) => {
            Members = e.Members;
        };
    }

    public ClusterMembershipStore CreateNewClusterMembershipStore(string machine) {
        var store = new ClusterMembershipStore(
            Provider.GetRequiredService<SqlServerStoreOptions>(),
            ClusterMembershipOptions with { MachineName = machine },
            NullLoggerFactory.Instance
        );
        return store;
    }
}
