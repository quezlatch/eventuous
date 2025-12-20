using Eventuous.SqlServer.Extensions;
using Eventuous.Subscriptions.Checkpoints.Sharding;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

public class SqlServerLeaseManager {
    readonly IClusterMembershipStore store;
    readonly string connectionString;
    readonly string schemaName;
    readonly int numOfShards;
    readonly ILogger<SqlServerLeaseManager>? logger;
    private readonly string aquireLease;
    private DataTable? _shardIdsTableVariable;

    public SqlServerLeaseManager(IClusterMembershipStore store, string connectionString, string schemaName, int numOfShards = 20, ILogger<SqlServerLeaseManager>? logger = null)
    {
        var schema = new Schema(schemaName);
        this.store = store;
        this.store.MembershipChanged += RebuildShardIdParameter;
        this.connectionString = connectionString;
        this.schemaName = schemaName;
        this.numOfShards = numOfShards;
        this.logger = logger;
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
            .Where(id => hasher.GetOwner(id) == owner)
            .ToArray();
        logger?.LogInformation("Node {Owner} owns {Shards}", owner, shardIds);
        var dataTable = new DataTable();
        dataTable.Columns.Add("ShardId", typeof(int));
        foreach (var id in shardIds) {
            dataTable.Rows.Add(id);
        }
        _shardIdsTableVariable = dataTable;
    }

    public async Task RenewLeaseAsync() {
        if (_shardIdsTableVariable == null) return;

        await using var connection = await ConnectionFactory.GetConnection(connectionString, CancellationToken.None).NoContext();
        using var cmd = connection.GetStoredProcCommand(aquireLease)
            .Add("@owner", SqlDbType.NVarChar, store.Owner)
            .Add("@expiration_timeout", SqlDbType.BigInt, 30) // 30 seconds
            .Add("@shard_ids", SqlDbType.Structured, _shardIdsTableVariable);
        await cmd.ExecuteNonQueryAsync().NoContext();
        logger?.LogInformation("Renewed leases for shards");
    }
}
