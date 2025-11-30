// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable ConvertClosureToMethodGroup

namespace Eventuous.SqlServer.Subscriptions.Sharding;

public record ClusterMembershipOptions {
    public int HeartbeatIntervalSeconds { get; init; } = 15;
    public int ExpirationTimeoutSeconds { get; init; } = 45;
    public int RefreshIntervalSeconds { get; init; } = 10;
    public string MachineName { get; init; } = Environment.MachineName;
}
