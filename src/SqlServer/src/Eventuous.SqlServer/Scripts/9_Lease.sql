CREATE OR ALTER PROCEDURE __schema__.aquire_lease
    @ShardId INT,
    @OwnerPod NVARCHAR(100),
    @Expires DATETIME2
AS
BEGIN
    UPDATE __schema__.ShardLeases
    SET OwnerPod = @OwnerPod,
        LeaseExpiresAt = @Expires
    WHERE ShardId = @ShardId
    AND (OwnerPod IS NULL OR LeaseExpiresAt < SYSUTCDATETIME())

    IF @@ROWCOUNT = 1
    BEGIN
        SELECT [Version] FROM __schema__.ShardLeases WHERE ShardId = @ShardId;
    END
    ELSE
    BEGIN
        SELECT CAST(NULL AS VARBINARY(8)) AS [Version];
    END
END;