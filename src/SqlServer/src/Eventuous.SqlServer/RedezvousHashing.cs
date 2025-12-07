namespace Eventuous.SqlServer;

/// <summary>
/// Implements the Redezvous Hashing algorithm for distributing strings across a set of nodes.
/// Redezvous hashing provides consistent hashing with minimal key redistribution when nodes are added or removed.
/// </summary>
public class RedezvousHashing
{
    private readonly List<string> _nodes;

    public RedezvousHashing(IEnumerable<string> nodes)
    {
        _nodes = nodes.ToList();
        if (_nodes.Count == 0)
        {
            throw new ArgumentException("At least one node must be provided", nameof(nodes));
        }
    }

    /// <summary>
    /// Finds the best node for a given key using the redezvous hashing algorithm.
    /// </summary>
    /// <param name="key">The key to hash</param>
    /// <returns>The node with the highest hash value for the key</returns>
    public string GetNode(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var bestNode = _nodes[0];
        var bestHash = Hash(key, bestNode);

        for (var i = 1; i < _nodes.Count; i++)
        {
            var hash = Hash(key, _nodes[i]);
            if (hash > bestHash)
            {
                bestHash = hash;
                bestNode = _nodes[i];
            }
        }

        return bestNode;
    }

    /// <summary>
    /// Adds a node to the hash ring.
    /// </summary>
    public void AddNode(string node)
    {
        if (string.IsNullOrEmpty(node))
        {
            throw new ArgumentException("Node cannot be null or empty", nameof(node));
        }

        if (!_nodes.Contains(node))
        {
            _nodes.Add(node);
        }
    }

    /// <summary>
    /// Removes a node from the hash ring.
    /// </summary>
    public void RemoveNode(string node)
    {
        _nodes.Remove(node);
        if (_nodes.Count == 0)
        {
            throw new InvalidOperationException("Cannot remove the last node");
        }
    }

    /// <summary>
    /// Gets all nodes in the hash ring.
    /// </summary>
    public IReadOnlyList<string> Nodes => _nodes.AsReadOnly();

    /// <summary>
    /// Computes the hash value for a key-node pair.
    /// Uses a combination of the key and node name to produce a consistent hash.
    /// </summary>
    private static ulong Hash(string key, string node) {
        var combined = $"{key}:{node}";
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(combined));
        return BitConverter.ToUInt64(bytes, 0);
    }
}