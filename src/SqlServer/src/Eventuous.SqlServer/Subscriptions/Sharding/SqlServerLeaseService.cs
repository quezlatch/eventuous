using Microsoft.Extensions.Hosting;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

public class SqlServerLeaseService(SqlServerLeaseManager leaseManager) : IHostedService {
    private Timer? _renewalTimer;
    private readonly int _heartbeatInterval = 5000;

    public Task StartAsync(CancellationToken cancellationToken) {
                _renewalTimer = new Timer(
            async _ => await leaseManager.RenewLeaseAsync(),
            null,
            1000, // give cluster memebership time to initialize
            _heartbeatInterval
        );
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        _renewalTimer?.Dispose();
        return Task.CompletedTask;
    }
}