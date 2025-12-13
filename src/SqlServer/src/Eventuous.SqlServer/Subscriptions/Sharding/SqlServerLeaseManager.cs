using Eventuous.SqlServer.Extensions;
using Eventuous.Subscriptions.Checkpoints.Sharding;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

public class SqlServerLeaseManager {
    readonly IClusterMembershipStore store;
    readonly string connectionString;
    readonly string schemaName;
    readonly int numOfShards;
    private readonly string aquireLease;
    private SqlParameter? _shardIdsParameter;

    public SqlServerLeaseManager(IClusterMembershipStore store, string connectionString, string schemaName, int numOfShards = 20)
    {
        var schema = new Schema(schemaName);
        this.store = store;
        this.store.MembershipChanged += RebuildShardIdParameter;
        this.connectionString = connectionString;
        this.schemaName = schemaName;
        this.numOfShards = numOfShards;
        this.aquireLease = schema.AquireLease;
    }

    /// <summary>
    /// Rebuilds the shard ID parameter used in SQL commands when cluster membership changes.
    /// Which should hopefully not happen that often.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void RebuildShardIdParameter(object? sender, MembershipChangedEventArgs e) {
        var hasher = new RendezvousHashing(e.Members);
        var owner = store.Owner;
        var shardIds = Enumerable.Range(0, numOfShards)
            .Where(id => hasher.GetOwner(id) == owner);
        var dataTable = new DataTable();
        dataTable.Columns.Add("ShardId", typeof(int));
        foreach (var id in shardIds) {
            dataTable.Rows.Add(id);
        }
        _shardIdsParameter = new SqlParameter("@shardIds", SqlDbType.Structured) {
            TypeName = $"{schemaName}.ShardIdList",
            Value = dataTable
        };
    }

    public async Task RenewLeaseAsync() {
        if (_shardIdsParameter == null) return;

        await using var connection = await ConnectionFactory.GetConnection(connectionString, CancellationToken.None).NoContext();
        using var cmd = connection.GetStoredProcCommand(aquireLease)
            .Add("@owner", SqlDbType.NVarChar, store.Owner)
            .Add("@expirationTimeout", SqlDbType.BigInt, 30) // 30 seconds
            .Add("@shardIds", SqlDbType.Structured, _shardIdsParameter);
    }
}
