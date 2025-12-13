namespace Eventuous.Subscriptions.Checkpoints.Sharding;

/// <summary>
/// Gets the current cluster membership. Currently just uses sql server, but could be extended kubernetes or other stores.
/// </summary>
public interface IClusterMembershipStore {
    event EventHandler<MembershipChangedEventArgs> MembershipChanged;
    string Owner {get;}
}

public class MembershipChangedEventArgs : EventArgs {
    public string[] Members { get; }

    public MembershipChangedEventArgs(IEnumerable<string> members) {
        Members = [.. members];
    }
}