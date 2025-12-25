using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

public class SqlServerLeaseService(int heartbeatIntervalSeconds, SqlServerLeaseManager leaseManager, ILogger<SqlServerLeaseService> logger) : IHostedService {
    private Timer? _renewalTimer;
    private readonly int _heartbeatInterval = heartbeatIntervalSeconds * 1000;

    public Task StartAsync(CancellationToken cancellationToken) {
        logger.LogInformation("Starting SQL Server lease manager");
        _renewalTimer = new Timer(
            async _ => await leaseManager.RenewLeaseAsync(),
            null,
            1000, // give cluster memebership time to initialize
            _heartbeatInterval
        );
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        logger.LogInformation("Stopping SQL Server lease manager");
        _renewalTimer?.Dispose();
        return Task.CompletedTask;
    }
}