namespace Eventuous.Subscriptions.Checkpoints.Sharding;

/// <summary>
/// Gets the current cluster membership. Currently just uses sql server, but could be extended kubernetes or other stores.
/// </summary>
public interface IClusterMembershipStore {
    Task<IEnumerable<string>> GetMembersAsync();
}