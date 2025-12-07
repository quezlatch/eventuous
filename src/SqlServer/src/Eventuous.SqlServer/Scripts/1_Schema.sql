IF (SCHEMA_ID(N'__schema__') IS NULL)
    BEGIN
        EXEC ('CREATE SCHEMA [__schema__] AUTHORIZATION [dbo]')
    END

IF OBJECT_ID('__schema__.ShardLeases', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.ShardLeases (
            ShardId INT PRIMARY KEY,
            [Owner] NVARCHAR(100) NULL,
            LeaseExpiresAt DATETIME2 NULL,
            [Version] ROWVERSION
        );
    END

IF OBJECT_ID('__schema__.Streams', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.Streams
        (
            StreamId   INT IDENTITY (1,1) NOT NULL,
            StreamName NVARCHAR(850)      NOT NULL,
            [Version]  INT DEFAULT (-1)   NOT NULL,
            CONSTRAINT PK_Streams PRIMARY KEY CLUSTERED (StreamId) WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON),
            CONSTRAINT UQ_StreamName UNIQUE NONCLUSTERED (StreamName),
            CONSTRAINT CK_VersionGteNegativeOne CHECK ([Version] >= -1),
        );
    END

IF OBJECT_ID('__schema__.Messages', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.Messages
        (
            MessageId      UNIQUEIDENTIFIER      NOT NULL,
            MessageType    NVARCHAR(128)         NOT NULL,
            StreamId       INT                   NOT NULL,
            StreamPosition INT                   NOT NULL,
            GlobalPosition BIGINT IDENTITY (0,1) NOT NULL,
            JsonData       NVARCHAR(MAX)         NOT NULL,
            JsonMetadata   NVARCHAR(MAX)         NOT NULL,
            Created        DATETIME2(7)          NOT NULL,
            -- use the multiplicative (Knuth) hash expression for ShardId
            -- is here instead of Streams to give the executor a helping hand when optimizing queries
            ShardId AS (
            CAST(
                ( ((CAST(StreamId AS BIGINT) * 2654435761) % 4294967296 + 4294967296) % 4294967296 )
                % __num_of_shards__
            AS INT)
            ) PERSISTED,
            CONSTRAINT PK_Events PRIMARY KEY CLUSTERED (GlobalPosition) WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON),
            CONSTRAINT FK_MessageStreamId FOREIGN KEY (StreamId) REFERENCES __schema__.Streams (StreamId),
            CONSTRAINT UQ_StreamIdAndStreamPosition UNIQUE NONCLUSTERED (StreamId, StreamPosition),
            CONSTRAINT UQ_StreamIdAndMessageId UNIQUE NONCLUSTERED (StreamId, MessageId),
            CONSTRAINT CK_StreamPositionGteZero CHECK (StreamPosition >= 0),
            CONSTRAINT CK_JsonDataIsJson CHECK (ISJSON(JsonData) = 1),
            CONSTRAINT CK_JsonMetadataIsJson CHECK (ISJSON(JsonMetadata) = 1),
            CONSTRAINT FK_MessageSShardId FOREIGN KEY (ShardId) REFERENCES __schema__.ShardLeases (ShardId),
            INDEX IDX_MessageSShardId NONCLUSTERED (ShardId, GlobalPosition),
            INDEX IDX_EventsStream (StreamId)
        );
    END

IF OBJECT_ID('__schema__.Checkpoints', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.Checkpoints
        (
            Id       NVARCHAR(128) NOT NULL,
            Position BIGINT            NULL,
            CONSTRAINT PK_Checkpoints PRIMARY KEY CLUSTERED (Id),
        );
    END

IF OBJECT_ID('__schema__.ShardedCheckpoints', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.ShardedCheckpoints
        (
            Id       NVARCHAR(128) NOT NULL,
            ShardId  INT           NOT NULL,
            Position BIGINT            NULL,
            CONSTRAINT PK_ShardedCheckpoints PRIMARY KEY CLUSTERED (Id, ShardId),
            CONSTRAINT FK_ShardedCheckpoints_ShardId FOREIGN KEY (ShardId) REFERENCES __schema__.ShardLeases (ShardId)
        );
    END

IF TYPE_ID('__schema__.StreamMessage') IS NULL
    BEGIN
        CREATE type __schema__.StreamMessage AS TABLE
        (
            message_id    UNIQUEIDENTIFIER NOT NULL,
            message_type  NVARCHAR(128)    NOT NULL,
            json_data     NVARCHAR(MAX)    NOT NULL,
            json_metadata NVARCHAR(MAX)    NOT NULL
        )
    END

IF TYPE_ID('__schema__.ShardIdList') IS NULL
    BEGIN
        CREATE TYPE __schema__.ShardIdList AS TABLE
        (
            ShardId INT NOT NULL
        );
    END

-- cluster membership store table
IF OBJECT_ID('__schema__.ClusterMembers', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.ClusterMembers
        (
            MachineName NVARCHAR(200) PRIMARY KEY,
            ExpiresAt DATETIME2 NOT NULL
        );
    END

-- Initialize ShardLeases with shard IDs 0 to __num_of_shards__. This does not mean
-- that all these shards will be used, but it sets up the leases for potential shards.
-- The actual number of shards used will depend on the application configuration.
-- Probably just 4 or 8. generally
;WITH Shards AS (
    SELECT TOP (__num_of_shards__) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS ShardId
    FROM sys.all_objects
)

INSERT INTO __schema__.ShardLeases (ShardId, [Owner], LeaseExpiresAt)
SELECT ShardId, NULL, NULL FROM Shards
WHERE NOT EXISTS (SELECT 1 FROM __schema__.ShardLeases SL WHERE SL.ShardId = Shards.ShardId);