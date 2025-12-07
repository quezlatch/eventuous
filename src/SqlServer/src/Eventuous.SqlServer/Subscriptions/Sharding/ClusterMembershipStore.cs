using Eventuous.SqlServer.Extensions;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

/// <summary>
/// SQL Server implementation of cluster membership store using the ClusterMembership table.
/// </summary>
public class ClusterMembershipStore : ClusterMembershipStoreBase
{
    private readonly string connectionString;
    private readonly string schema;

    public ClusterMembershipStore(SqlServerStoreOptions options, ClusterMembershipOptions clusterOptions, ILoggerFactory loggerFactory) 
    : base(clusterOptions, loggerFactory)
    {
        connectionString = Ensure.NotEmptyString(options.ConnectionString);
        schema = Ensure.NotEmptyString(options.Schema);
    }

    protected override async Task<IEnumerable<string>> ReadActiveMembersAsync() {
        await using var connection = await ConnectionFactory.GetConnection(connectionString, CancellationToken.None).NoContext();
        using var cmd = connection.GetTextCommand(
            $"SELECT MachineName FROM {schema}.ClusterMembers WHERE ExpiresAt > @CurrentTime"
        ).Add("@CurrentTime", SqlDbType.DateTime2, DateTime.UtcNow);
        var members = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync().NoContext();
        while (await reader.ReadAsync().NoContext()) {
            members.Add(reader.GetString(0));
        }
        return members;
    }

    protected override async Task RegisterMemberAsync(string machineName, DateTime expiresAt, CancellationToken cancellationToken) {
        await using var connection = await ConnectionFactory.GetConnection(connectionString, cancellationToken).NoContext();
        using var cmd = connection.GetTextCommand(
            $@"INSERT INTO {schema}.ClusterMembers (MachineName, ExpiresAt) VALUES (@MachineName, @ExpiresAt)"
        )
        .Add("@MachineName", SqlDbType.NVarChar, machineName)
        .Add("@ExpiresAt", SqlDbType.DateTime2, expiresAt);
        await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
    }
    protected override async Task RenewMemberAsync(string machineName, DateTime expiresAt) {
        await using var connection = await ConnectionFactory.GetConnection(connectionString, CancellationToken.None).NoContext();
        using var cmd = connection.GetTextCommand(
            $@"UPDATE {schema}.ClusterMembers SET ExpiresAt = @ExpiresAt WHERE MachineName = @MachineName"
        )
        .Add("@MachineName", SqlDbType.NVarChar, machineName)
        .Add("@ExpiresAt", SqlDbType.DateTime2, expiresAt);
        await cmd.ExecuteNonQueryAsync().NoContext();
    }
    public override async Task UnregisterMemberAsync(string machineName, CancellationToken cancellationToken) {
        await using var connection = await ConnectionFactory.GetConnection(connectionString, cancellationToken).NoContext();
        using var cmd = connection.GetTextCommand(
            $@"DELETE FROM {schema}.ClusterMembers WHERE MachineName = @MachineName"
        )
        .Add("@MachineName", SqlDbType.NVarChar, machineName);
        await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
    }

}
