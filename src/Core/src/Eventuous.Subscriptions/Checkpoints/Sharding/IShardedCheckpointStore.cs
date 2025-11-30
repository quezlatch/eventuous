namespace Eventuous.Subscriptions.Checkpoints.Sharding;

[PublicAPI]
public interface IShardedCheckpointStore {
    ValueTask<ShardedCheckpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken);

    ValueTask<ShardedCheckpoint> StoreCheckpoint(ShardedCheckpoint checkpoint, bool force, CancellationToken cancellationToken);
}