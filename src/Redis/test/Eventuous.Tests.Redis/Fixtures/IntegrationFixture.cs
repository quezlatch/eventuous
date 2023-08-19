using System.Diagnostics;
using System.Text.Json;
using Eventuous.Diagnostics;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using StackExchange.Redis;
using Eventuous.Redis;
using Testcontainers.Redis;

namespace Eventuous.Tests.Redis.Fixtures;

public sealed class IntegrationFixture : IAsyncLifetime {
    public IEventWriter     EventWriter    { get; set; }
    public IEventReader     EventReader    { get; set; }
    public IAggregateStore  AggregateStore { get; set; }
    public GetRedisDatabase GetDatabase    { get; set; }

    readonly ActivityListener _listener = DummyActivityListener.Create();
    RedisContainer            _redisContainer;

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public IntegrationFixture() {
        DefaultEventSerializer.SetDefaultSerializer(Serializer);

        return;
    }

    public async Task InitializeAsync() {
        _redisContainer = new RedisBuilder().WithImage("redis:7.0.12-alpine").Build();

        await _redisContainer.StartAsync();
        var connString = _redisContainer.GetConnectionString();
        await Module.LoadModule(GetDb);

        GetDatabase = GetDb;
        var store = new RedisStore(GetDb, new RedisStoreOptions(), Serializer);
        EventWriter    = store;
        EventReader    = store;
        AggregateStore = new AggregateStore(store, store);

        return;

        IDatabase GetDb() {
            var muxer = ConnectionMultiplexer.Connect(connString);

            return muxer.GetDatabase();
        }
    }

    public async Task DisposeAsync() {
        await _redisContainer.DisposeAsync();
        _listener.Dispose();
    }
}
