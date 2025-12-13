using Eventuous.Subscriptions.Checkpoints.Sharding;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

/// <summary>
/// Manages cluster membership by registering the current machine in SQL Server
/// and periodically reading the cluster membership list.
/// </summary>
public abstract class ClusterMembershipStoreBase : IClusterMembershipStore
{
    private readonly ILogger<ClusterMembershipStoreBase> _logger;
    protected readonly string _machineName;
    protected readonly int _expirationTimeout;

    public string Owner => _machineName;

    private List<string> _members = [];

    public event EventHandler<MembershipChangedEventArgs> MembershipChanged;

    protected ClusterMembershipStoreBase(ClusterMembershipOptions options, ILoggerFactory loggerFactory)
    {
        _expirationTimeout = options.ExpirationTimeoutSeconds;
        _logger = loggerFactory.CreateLogger<ClusterMembershipStoreBase>();
        _machineName = options.MachineName;
    }

    public async Task ReadMembersAsync()
    {
        try
        {
            var members = await ReadActiveMembersAsync().NoContext();
            var orderedMembers = members.Order().ToArray();
            if (!orderedMembers.SequenceEqual(_members))
            {
                _members = [.. orderedMembers];
                _logger.LogInformation("Cluster membership changed: {Members}", orderedMembers);
                MembershipChanged?.Invoke(this, new MembershipChangedEventArgs(_members));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading cluster members");
        }
    }

    /// <summary>
    /// Register the current machine in the cluster membership table.
    /// </summary>
    public abstract Task RegisterMemberAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Renew the membership expiration for the given machine.
    /// </summary>
    public abstract Task RenewMembershipAsync();

    /// <summary>
    /// Read all active (non-expired) cluster members.
    /// </summary>
    protected abstract Task<IEnumerable<string>> ReadActiveMembersAsync();

    /// <summary>
    /// Remove the machine from the cluster membership table.
    /// </summary>
    public abstract Task UnregisterMemberAsync(string machineName,CancellationToken cancellationToken);
}
