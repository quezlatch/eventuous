using System.Security.Cryptography;
using System.Text;

namespace Eventuous.SqlServer;

/// <summary>
/// Implements the Redezvous Hashing algorithm for distributing strings across a set of nodes.
/// Redezvous hashing provides consistent hashing with minimal key redistribution when nodes are added or removed.
/// </summary>
public class RendezvousHashing
{
    private readonly List<string> _podIds;

    public RendezvousHashing(IEnumerable<string> podIds)
    {
        _podIds = podIds.ToList();
        if (_podIds.Count == 0)
        {
            throw new ArgumentException("At least one node must be provided", nameof(podIds));
        }
    }

    public string GetOwner(int shardId)
    {
        var best = _podIds[0];
        ulong bestScore = 0;
        foreach (var p in _podIds)
        {
            var score = Hash64(p + "|" + shardId);
            if (score > bestScore || best == null)
            {
                bestScore = score;
                best = p;
            }
        }
        return best;
    }

        // 64-bit hash using SHA256 and taking first 8 bytes
    private static ulong Hash64(string s) {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return BitConverter.ToUInt64(bytes, 0);
    }
}