using Eventuous.SqlServer.Extensions;
using Eventuous.SqlServer.Projections;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions.Sharding;

public class SqlServerAllStreamShardingSubscription(
        SqlServerAllStreamShardingSubscriptionOptions options,
        ICheckpointStore                      checkpointStore,
        ConsumePipe                           consumePipe,
        ILoggerFactory?                       loggerFactory     = null,
        IEventSerializer?                     eventSerializer   = null,
        IMetadataSerializer?                  metaSerializer    = null,
        SqlServerConnectionOptions?           connectionOptions = null
    )
    : SqlServerSubscriptionBase<SqlServerAllStreamShardingSubscriptionOptions>(
        options,
        checkpointStore,
        consumePipe,
        SubscriptionKind.All,
        loggerFactory,
        eventSerializer,
        metaSerializer,
        connectionOptions
    ) {
    protected override SqlCommand PrepareCommand(SqlConnection connection, long start)
        => connection.GetStoredProcCommand(Schema.ReadStreamSub)
            .Add("@@checkpoint_id", SqlDbType.NVarChar, "")
            .Add("@owner", SqlDbType.NVarChar, "")
            .Add("@count", SqlDbType.Int, Options.MaxPageSize);
}

public record SqlServerAllStreamShardingSubscriptionOptions : SqlServerSubscriptionBaseOptions;