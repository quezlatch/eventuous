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
    private readonly ILogger<ClusterMembershipStore> logger;

    public ClusterMembershipStore(SqlServerStoreOptions options, ClusterMembershipOptions clusterOptions, ILoggerFactory loggerFactory) 
    : base(options, clusterOptions, loggerFactory)
    {
        connectionString = Ensure.NotEmptyString(options.ConnectionString);
        schema = Ensure.NotEmptyString(options.Schema);
        logger = loggerFactory.CreateLogger<ClusterMembershipStore>();
    }

    protected override async Task<IEnumerable<string>> ReadActiveMembersAsync() {
        await using var connection = await ConnectionFactory.GetConnection(connectionString, CancellationToken.None).NoContext();
        using var cmd = connection.GetTextCommand(
            $"SELECT MachineName FROM {schema}.ClusterMembers WHERE ExpiresAt > SYSUTCDATETIME()"
        );
        var members = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync().NoContext();
        while (await reader.ReadAsync().NoContext()) {
            members.Add(reader.GetString(0));
        }
        return members;
    }

    public override async Task RegisterMemberAsync(CancellationToken cancellationToken) {
        logger.LogInformation("Registering cluster member: {MachineName}", _machineName);
        await using var connection = await ConnectionFactory.GetConnection(connectionString, cancellationToken).NoContext();
        using var cmd = connection.GetTextCommand(
            $@"INSERT INTO {schema}.ClusterMembers (MachineName, ExpiresAt) VALUES (@MachineName, DATEADD(SECOND, @ExpirationTimeout, SYSUTCDATETIME()))"
        )
        .Add("@MachineName", SqlDbType.NVarChar, _machineName)
        .Add("@ExpirationTimeout", SqlDbType.Int, _expirationTimeout);
        await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
    }
    public override async Task RenewMembershipAsync() {
        logger.LogInformation("Renewing cluster member: {MachineName}", _machineName);
        await using var connection = await ConnectionFactory.GetConnection(connectionString, CancellationToken.None).NoContext();
        using var cmd = connection.GetTextCommand(
            $@"UPDATE {schema}.ClusterMembers SET ExpiresAt = DATEADD(SECOND, @ExpirationTimeout, SYSUTCDATETIME()) WHERE MachineName = @MachineName"
        )
        .Add("@MachineName", SqlDbType.NVarChar, _machineName)
        .Add("@ExpirationTimeout", SqlDbType.Int, _expirationTimeout);
        await cmd.ExecuteNonQueryAsync().NoContext();
    }
    public override async Task UnregisterMemberAsync(CancellationToken cancellationToken) {
        logger.LogInformation("Unregistering cluster member: {MachineName}", _machineName);
        await using var connection = await ConnectionFactory.GetConnection(connectionString, cancellationToken).NoContext();
        using var cmd = connection.GetTextCommand(
            $@"DELETE FROM {schema}.ClusterMembers WHERE MachineName = @MachineName"
        )
        .Add("@MachineName", SqlDbType.NVarChar, _machineName);
        await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
    }

}
