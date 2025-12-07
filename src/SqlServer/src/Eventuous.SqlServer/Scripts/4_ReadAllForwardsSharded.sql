CREATE OR ALTER PROCEDURE __schema__.read_all_forwards_sharded
    @checkpoint_id NVARCHAR(128),
    @owner NVARCHAR(100),
    @count INT = 1000
AS
BEGIN
    DECLARE @MinGlobalPosition BIGINT;

    SELECT @MinGlobalPosition = MIN(ISNULL(Position, 0))
    FROM __schema__.ShardedCheckpoints
    WHERE Id = @checkpoint_id;

    SELECT TOP (@count)
        m.MessageId,
        m.MessageType,
        m.StreamPosition,
        m.GlobalPosition,
        m.JsonData,
        m.JsonMetadata,
        m.Created,
        m.ShardId,
        s.StreamName
    FROM __schema__.Messages m
    JOIN __schema__.ShardLeases sl ON
        sl.ShardId = m.ShardId AND sl.[Owner] = @owner AND sl.LeaseExpiresAt > SYSUTCDATETIME()
    JOIN __schema__.Streams s ON m.StreamId = s.StreamId
    LEFT JOIN __schema__.ShardedCheckpoints sc ON
        sc.Id = @checkpoint_id AND sc.ShardId = sl.ShardId
    WHERE m.GlobalPosition >= @MinGlobalPosition
      AND m.GlobalPosition >= ISNULL(sc.Position, 0)
    ORDER BY m.GlobalPosition
    OPTION (RECOMPILE); -- optional: helps optimizer use actual @MinGlobalPosition and other params
END;