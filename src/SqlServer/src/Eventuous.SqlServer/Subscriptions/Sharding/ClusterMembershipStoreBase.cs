using Eventuous.Subscriptions.Checkpoints.Sharding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

/// <summary>
/// Manages cluster membership by registering the current machine in SQL Server
/// and periodically reading the cluster membership list.
/// </summary>
public abstract class ClusterMembershipStoreBase : IHostedService, IClusterMembershipStore
{
    private readonly ILogger<ClusterMembershipStoreBase> _logger;
    private Timer? _renewalTimer;
    private Timer? _readTimer;
    private readonly string _machineName;
    private readonly TimeSpan _heartbeatInterval;
    private readonly TimeSpan _expirationTimeout;
    private readonly TimeSpan _renewalInterval;

    public IReadOnlyList<string> Members => _members.AsReadOnly();
    
    private List<string> _members = [];

    protected ClusterMembershipStoreBase(SqlServerStoreOptions options, string machineName, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ClusterMembershipStoreBase>();
        _machineName = machineName;
        _heartbeatInterval = TimeSpan.FromSeconds(options.ClusterMembership.HeartbeatIntervalSeconds);
        _expirationTimeout = TimeSpan.FromSeconds(options.ClusterMembership.ExpirationTimeoutSeconds);
        _renewalInterval = TimeSpan.FromSeconds(options.ClusterMembership.RenewIntervalSeconds);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting cluster membership store for machine: {MachineName}", _machineName);

        // Register this machine
        await RegisterMemberAsync(_machineName, ExpiresAt(), cancellationToken).NoContext();

        // Renew membership every 15 seconds
        _renewalTimer = new Timer(
            async _ => await RenewMembershipAsync(),
            null,
            _heartbeatInterval,
            _heartbeatInterval
        );

        // Read membership list every 5 seconds
        _readTimer = new Timer(
            async _ => await ReadMembersAsync(),
            null,
            _renewalInterval,
            _renewalInterval
        );

        // Initial read
        await ReadMembersAsync().NoContext();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping cluster membership store for machine: {MachineName}", _machineName);

        _renewalTimer?.Dispose();
        _readTimer?.Dispose();

        // Remove this machine from the cluster
        await UnregisterMemberAsync(_machineName, cancellationToken).NoContext();
    }

    private async Task RenewMembershipAsync()
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

    private async Task ReadMembersAsync()
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
    protected abstract Task UnregisterMemberAsync(string machineName,CancellationToken cancellationToken);
}