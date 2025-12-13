CREATE OR ALTER PROCEDURE __schema__.aquire_renew_lease
    @owner NVARCHAR(100),
    @expirationTimeout BIGINT,
    @shard_ids __schema__.ShardIdList READONLY
AS
BEGIN
    UPDATE __schema__.ShardLeases
    SET [Owner] = @owner,
        LeaseExpiresAt = DATEADD(SECOND, @expirationTimeout, SYSUTCDATETIME())
    WHERE ShardId IN (SELECT ShardId FROM @shard_ids)
    AND ([Owner] IS NULL OR LeaseExpiresAt < SYSUTCDATETIME())
END;