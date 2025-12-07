CREATE OR ALTER PROCEDURE __schema__.aquire_lease
    @shard_id INT,
    @owner NVARCHAR(100),
    @expires DATETIME2
AS
BEGIN
    UPDATE __schema__.ShardLeases
    SET [Owner] = @owner,
        LeaseExpiresAt = @expires
    WHERE ShardId = @shard_id
    AND ([Owner] IS NULL OR LeaseExpiresAt < SYSUTCDATETIME())

    IF @@ROWCOUNT = 1
    BEGIN
        SELECT [Version] FROM __schema__.ShardLeases WHERE ShardId = @shard_id;
    END
    ELSE
    BEGIN
        SELECT CAST(NULL AS VARBINARY(8)) AS [Version];
    END
END;