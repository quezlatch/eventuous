CREATE OR ALTER PROCEDURE __schema__.aquire_renew_lease
    @owner NVARCHAR(100),
    @expiration_timeout BIGINT,
    @shard_ids __schema__.ShardIdList READONLY
AS
BEGIN
    UPDATE __schema__.ShardLeases
    SET [Owner] = @owner,
        LeaseExpiresAt = DATEADD(SECOND, @expiration_timeout, SYSUTCDATETIME())
    WHERE ShardId IN (SELECT shard_id FROM @shard_ids)
    AND ([Owner] IS NULL OR LeaseExpiresAt < SYSUTCDATETIME())
END;