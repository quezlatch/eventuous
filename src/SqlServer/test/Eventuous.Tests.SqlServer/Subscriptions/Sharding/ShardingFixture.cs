using Eventuous.Subscriptions.Checkpoints.Sharding;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.SqlServer.Fixtures;
using Microsoft.Extensions.DependencyInjection;
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

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventuousSqlServer(Container.GetConnectionString(), _schemaName, true);
        services.AddEventStore<SqlServerStore>();
        services.AddSqlServerClusterMembershipStore(ClusterMembershipOptions);
    }

    protected override MsSqlContainer CreateContainer() => SqlContainer.Create();

    protected override void GetDependencies(IServiceProvider provider) {
        base.GetDependencies(provider);
        ClusterMembershipStore = provider.GetRequiredService<IClusterMembershipStore>();
    }
}
