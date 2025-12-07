using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

public class ClusterMembershipService : IHostedService
{
    private readonly ILogger<ClusterMembershipService> _logger;
    private Timer? _renewalTimer;
    private Timer? _readTimer;
    private readonly string _machineName;
    private readonly TimeSpan _heartbeatInterval;
    private readonly TimeSpan _refreshInterval;
    private readonly ClusterMembershipStoreBase _store;

    public ClusterMembershipService(ClusterMembershipStoreBase store, ClusterMembershipOptions options, ILogger<ClusterMembershipService> logger)
    {
        _machineName = options.MachineName;
        _heartbeatInterval = TimeSpan.FromSeconds(options.HeartbeatIntervalSeconds);
        _refreshInterval = TimeSpan.FromSeconds(options.RefreshIntervalSeconds);
        _store = store;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting cluster membership store for machine: {MachineName}", _machineName);

        // Register this machine
        await _store.RegisterMemberAsync(_machineName, cancellationToken).NoContext();

        // Renew membership every heartbeat seconds
        _renewalTimer = new Timer(
            async _ => await _store.RenewMembershipAsync(),
            null,
            _heartbeatInterval,
            _heartbeatInterval
        );

        // Read membership list every refresh seconds
        _readTimer = new Timer(
            async _ => await _store.ReadMembersAsync(),
            null,
            _refreshInterval,
            _refreshInterval
        );

        // Initial read
        await _store.ReadMembersAsync().NoContext();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping cluster membership store for machine: {MachineName}", _machineName);

        _renewalTimer?.Dispose();
        _readTimer?.Dispose();

        // Remove this machine from the cluster
        await _store.UnregisterMemberAsync(_machineName, cancellationToken).NoContext();
    }
}