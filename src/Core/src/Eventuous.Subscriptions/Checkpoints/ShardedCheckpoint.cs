using System.Runtime.InteropServices;

namespace Eventuous.Subscriptions.Checkpoints;

[PublicAPI]
[StructLayout(LayoutKind.Auto)]
public record struct ShardedCheckpoint(string Id, long ShardId, ulong? Position) {
    public static ShardedCheckpoint Empty(string id, long shardId) => new(id, shardId, null);

    public readonly bool IsEmpty => Position == null;
}
