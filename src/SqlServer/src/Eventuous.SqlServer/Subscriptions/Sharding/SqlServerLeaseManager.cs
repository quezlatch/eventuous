using Eventuous.SqlServer.Extensions;
using Eventuous.Subscriptions.Checkpoints.Sharding;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

public record ShardLease(int ShardId, string Owner);

public class SqlServerLeaseManager {
    readonly IClusterMembershipStore store;
    readonly string connectionString;
    readonly string schemaName;
    readonly int numOfShards;
    readonly ILoggerFactory? loggerFactory;
    private readonly string aquireLease;
    private List<string> _members;
    private SqlParameter? _shardIdsParameter;

    public SqlServerLeaseManager(IClusterMembershipStore store, string connectionString, string schemaName, int numOfShards = 20, ILoggerFactory? loggerFactory = null)
    {
        var schema = new Schema(schemaName);
        this.store = store;
        this.store.MembershipChanged += RebuildShardIdParameter;
        this.connectionString = connectionString;
        this.schemaName = schemaName;
        this.numOfShards = numOfShards;
        this.loggerFactory = loggerFactory;
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

    // public async Task<ShardLease?> TryAcquireLeaseAsync(
    // int shardId, 
    // string podId,
    // TimeSpan leaseDuration,
    // CancellationToken token)
    // {
    //     await using var connection = await ConnectionFactory.GetConnection(connectionString, token).NoContext();

    //     using var cmd = connection.GetStoredProcCommand(aquireLease)
    //         .Add("@shard_id", SqlDbType.Int, shardId)
    //         .Add("@owner", SqlDbType.NVarChar, podId)
    //         .Add("@expires", SqlDbType.DateTime2, DateTime.UtcNow + leaseDuration);

    //     var version = await cmd.ExecuteScalarAsync(token);

    //     return version != DBNull.Value && version != null
    //         ? new ShardLease(shardId, podId,  (byte[])version)
    //         : null;
    // }

    public async Task RenewLeaseAsync() {
        if (_shardIdsParameter == null) return;
    }
}

/*

public class EventRecord { public long Sequence { get; set; } }

public interface IEventReader
{
    Task<IReadOnlyList<EventRecord>> ReadEventsAsync(int shardId, long fromSequence, CancellationToken ct);
}

public class ShardProcessor
{
    private readonly int _shardId;
    private readonly string _podId;
    private readonly IShardLeaseStore _leaseStore;
    private readonly ICheckpointStore _checkpointStore;
    private readonly IEventReader _reader;
    private readonly TimeSpan _leaseTtl;
    private long _currentVersion;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public ShardProcessor(int shardId, string podId, IShardLeaseStore leaseStore, ICheckpointStore checkpointStore, IEventReader reader, TimeSpan leaseTtl)
    {
        _shardId = shardId;
        _podId = podId;
        _leaseStore = leaseStore;
        _checkpointStore = checkpointStore;
        _reader = reader;
        _leaseTtl = leaseTtl;
    }

    public async Task StartAsync(CancellationToken appToken)
    {
        // Ensure we currently own the lease and load version
        var lease = await _leaseStore.GetLeaseAsync(_shardId, appToken)
            ?? throw new InvalidOperationException("Lease missing for shard " + _shardId);

        _currentVersion = lease.Version;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(appToken);
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts == null) return;
        _cts.Cancel();
        if (_loop != null) await _loop;
        // Try release gracefully (best-effort)
        await _leaseStore.TryReleaseAsync(_shardId, _podId, _currentVersion);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        long checkpoint = await _checkpointStore.GetCheckpointAsync(_shardId, ct);

        while (!ct.IsCancellationRequested)
        {
            // Attempt a local renew BEFORE doing heavy work (to detect lease loss quickly)
            var renewed = await _leaseStore.TryRenewAsync(_shardId, _podId, _currentVersion, _leaseTtl, ct);
            if (!renewed)
            {
                // Lost lease — stop
                return;
            }

            // On success, version incremented in DB, so load the new version
            var lease = await _leaseStore.GetLeaseAsync(_shardId, ct);
            if (lease == null || lease.OwnerPod != _podId) return;
            _currentVersion = lease.Version;

            // Read next batch of events
            var events = await _reader.ReadEventsAsync(_shardId, checkpoint + 1, ct);
            if (events.Count == 0)
            {
                await Task.Delay(200, ct);
                continue;
            }

            foreach (var evt in events)
            {
                await HandleEventAsync(evt, ct);
                checkpoint = evt.Sequence;
                await _checkpointStore.SetCheckpointAsync(_shardId, checkpoint, ct);
            }
        }
    }

    private Task HandleEventAsync(EventRecord evt, CancellationToken ct)
    {
        // TODO: call your domain handler, projection writer, etc.
        return Task.CompletedTask;
    }
}


using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

public class ShardManagerBackgroundService : BackgroundService
{
    private readonly IShardLeaseStore _leaseStore;
    private readonly ICheckpointStore _checkpointStore;
    private readonly IPodProvider _podProvider;
    private readonly IEventReader _reader;
    private readonly string _podId;
    private readonly int _shardCount;
    private readonly TimeSpan _leaseTtl = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _renewInterval = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _rebalanceInterval = TimeSpan.FromSeconds(5);

    // running processors keyed by shardId
    private readonly ConcurrentDictionary<int, (ShardProcessor proc, CancellationTokenSource cts)> _running = new();

    public ShardManagerBackgroundService(
        IShardLeaseStore leaseStore,
        ICheckpointStore checkpointStore,
        IPodProvider podProvider,
        IEventReader reader,
        string podId,
        int shardCount)
    {
        _leaseStore = leaseStore;
        _checkpointStore = checkpointStore;
        _podProvider = podProvider;
        _reader = reader;
        _podId = podId;
        _shardCount = shardCount;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // loops: rebalance + renew
        var renewTask = Task.Run(() => RenewLoop(stoppingToken), stoppingToken);
        var rebalanceTask = Task.Run(() => RebalanceLoop(stoppingToken), stoppingToken);

        await Task.WhenAny(renewTask, rebalanceTask);

        // on cancellation, stop all running processors
        await StopAllAsync();
    }

    private async Task RebalanceLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RebalanceOnce(ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Rebalance error: {ex}");
            }

            await Task.Delay(_rebalanceInterval, ct);
        }
    }

    private async Task RenewLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                foreach (var kv in _running.ToArray())
                {
                    var shardId = kv.Key;
                    var (proc, cts) = kv.Value;
                    // call GetLease to know current version
                    var lease = await _leaseStore.GetLeaseAsync(shardId, ct);
                    if (lease == null || lease.OwnerPod != _podId)
                    {
                        // lost ownership — stop processor
                        await StopProcessorAsync(shardId);
                        continue;
                    }

                    var renewOk = await _leaseStore.TryRenewAsync(shardId, _podId, lease.Version, _leaseTtl, ct);
                    if (!renewOk)
                    {
                        await StopProcessorAsync(shardId);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Renew loop error: {ex}");
            }

            await Task.Delay(_renewInterval, ct);
        }
    }

    private async Task RebalanceOnce(CancellationToken ct)
    {
        var pods = (await _podProvider.GetAlivePodIdsAsync(ct)).ToList();
        if (!pods.Any()) return;

        for (int shardId = 0; shardId < _shardCount; shardId++)
        {
            var owner = RendezvousHasher.GetOwner(shardId, pods);
            if (owner != _podId)
            {
                // if we are running this shard, stop it
                if (_running.ContainsKey(shardId))
                    await StopProcessorAsync(shardId);
                continue;
            }

            // we should own the shard
            if (_running.ContainsKey(shardId)) continue; // already running

            // try to acquire lease (best-effort)
            var acquired = await _leaseStore.TryAcquireAsync(shardId, _podId, _leaseTtl, ct);
            if (!acquired) continue;

            // start the processor
            var tokenSrc = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var processor = new ShardProcessor(shardId, _podId, _leaseStore, _checkpointStore, _reader, _leaseTtl);
            await processor.StartAsync(tokenSrc.Token);

            _running[shardId] = (processor, tokenSrc);
        }
    }

    private async Task StopProcessorAsync(int shardId)
    {
        if (!_running.TryRemove(shardId, out var pair)) return;
        var (proc, cts) = pair;
        try
        {
            cts.Cancel();
            await proc.StopAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping shard {shardId}: {ex}");
        }
        finally
        {
            cts.Dispose();
        }
    }

    private async Task StopAllAsync()
    {
        var keys = _running.Keys.ToArray();
        foreach (var k in keys) await StopProcessorAsync(k);
    }
}


*/