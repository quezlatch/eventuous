using Eventuous.Subscriptions.Checkpoints.Sharding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

/// <summary>
/// Manages cluster membership by registering the current machine in SQL Server
/// and periodically reading the cluster membership list.
/// </summary>
public abstract class ClusterMembershipStoreBase : IClusterMembershipStore
{
    private readonly ILogger<ClusterMembershipStoreBase> _logger;
    private readonly string _machineName;
    private readonly TimeSpan _expirationTimeout;

    public IReadOnlyList<string> Members => _members.AsReadOnly();
    
    private List<string> _members = [];

    protected ClusterMembershipStoreBase(ClusterMembershipOptions options, ILoggerFactory loggerFactory)
    {
        _expirationTimeout = TimeSpan.FromSeconds(options.ExpirationTimeoutSeconds);
        _logger = loggerFactory.CreateLogger<ClusterMembershipStoreBase>();
        _machineName = options.MachineName;
    }

    public async Task RenewMembershipAsync()
    {
        try {
            await RenewMemberAsync(_machineName, ExpiresAt()).NoContext();
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing cluster membership");
        }
    }

    public Task<IEnumerable<string>> GetMembersAsync() => Task.FromResult(_members.AsEnumerable());

    private DateTime ExpiresAt() => DateTime.UtcNow.Add(_expirationTimeout);

    public async Task ReadMembersAsync()
    {
        try
        {
            var members = await ReadActiveMembersAsync().NoContext();
            _members = [.. members];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading cluster members");
        }
    }

    public Task RegisterMemberAsync(string machineName, CancellationToken cancellationToken)
        => RegisterMemberAsync(machineName, ExpiresAt(), cancellationToken);

    /// <summary>
    /// Register the current machine in the cluster membership table.
    /// </summary>
    protected abstract Task RegisterMemberAsync(string machineName, DateTime expiresAt, CancellationToken cancellationToken);

    /// <summary>
    /// Renew the membership expiration for the given machine.
    /// </summary>
    protected abstract Task RenewMemberAsync(string machineName, DateTime expiresAt);

    /// <summary>
    /// Read all active (non-expired) cluster members.
    /// </summary>
    protected abstract Task<IEnumerable<string>> ReadActiveMembersAsync();

    /// <summary>
    /// Remove the machine from the cluster membership table.
    /// </summary>
    public abstract Task UnregisterMemberAsync(string machineName,CancellationToken cancellationToken);
}
