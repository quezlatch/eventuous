// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.SqlServer;
using Eventuous.SqlServer.Projections;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.SqlServer.Subscriptions.Sharding;
using Eventuous.Subscriptions.Checkpoints.Sharding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable UnusedMethodReturnValue.Global
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions {
    /// <param name="services">Service collection</param>
    extension(IServiceCollection services) {
        /// <summary>
        /// Adds SQL Server event store and the necessary schema to the DI container.
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        /// <param name="schema">Schema name</param>
        /// <param name="initializeDatabase">Set to true if you want the schema to be created on startup</param>
        /// <returns></returns>
        public IServiceCollection AddEventuousSqlServer(
                string connectionString,
                string schema             = Schema.DefaultSchema,
                bool   initializeDatabase = false
            ) {
            var options = new SqlServerStoreOptions {
                Schema             = Ensure.NotEmptyString(schema),
                ConnectionString   = Ensure.NotEmptyString(connectionString),
                InitializeDatabase = initializeDatabase
            };
            services.AddSingleton(options);
            services.AddSingleton<SqlServerStore>();
            services.AddHostedService<SchemaInitializer>();
            services.TryAddSingleton(new SqlServerConnectionOptions(connectionString, schema));

            return services;
        }

        /// <summary>
        /// Adds SQL Server event store and the necessary schema to the DI container using the configuration.
        /// </summary>
        /// <param name="config">Configuration section for SQL Server options</param>
        /// <returns></returns>
        public IServiceCollection AddEventuousSqlServer(IConfiguration config) {
            services.Configure<SqlServerStoreOptions>(config);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<SqlServerStoreOptions>>().Value);
            services.AddSingleton<SqlServerStore>();
            services.AddHostedService<SchemaInitializer>();

            services.TryAddSingleton(
                sp => {
                    var storeOptions = sp.GetRequiredService<IOptions<SqlServerStoreOptions>>().Value;

                    return new SqlServerConnectionOptions(Ensure.NotEmptyString(storeOptions.ConnectionString), storeOptions.Schema);
                }
            );

            return services;
        }

        /// <summary>
        /// Registers the SQL Server-based checkpoint store using the details provided when registering
        /// SQL Server connection factory.
        /// </summary>
        /// <returns></returns>
        public IServiceCollection AddSqlServerCheckpointStore()
            => services.AddCheckpointStore<SqlServerCheckpointStore>(
                sp => {
                    var loggerFactory          = sp.GetService<ILoggerFactory>();
                    var connectionOptions      = sp.GetService<SqlServerConnectionOptions>();
                    var checkpointStoreOptions = sp.GetService<SqlServerCheckpointStoreOptions>();

                    var schema = connectionOptions?.Schema is not null and not Schema.DefaultSchema
                     && checkpointStoreOptions?.Schema is null or Schema.DefaultSchema
                            ? connectionOptions.Schema
                            : checkpointStoreOptions?.Schema ?? Schema.DefaultSchema;
                    var connectionString = checkpointStoreOptions?.ConnectionString ?? connectionOptions?.ConnectionString;

                    return new(Ensure.NotNull(connectionString), schema, loggerFactory);
                }
            );

        public IServiceCollection AddSqlServerClusterMembershipStore(ClusterMembershipOptions? options = null) {
            var clusterOptions = options ?? new ClusterMembershipOptions();
            services.AddSingleton<ClusterMembershipStoreBase>(sp => new ClusterMembershipStore(
                                sp.GetRequiredService<SqlServerStoreOptions>(),
                                clusterOptions,
                                sp.GetRequiredService<ILoggerFactory>()
                            ));
            services.AddHostedService(sp =>
                new ClusterMembershipService(
                    sp.GetRequiredService<ClusterMembershipStoreBase>(),
                    clusterOptions,
                    sp.GetRequiredService<ILogger<ClusterMembershipService>>()
                ));
            services.AddSingleton<IClusterMembershipStore>(sp => sp.GetRequiredService<ClusterMembershipStoreBase>());
            return services;
        }

        public IServiceCollection AddSqlServerShardLeaseManager(int heartbeatInterval = 5000) {
            services.AddSingleton(sp => {
                var storeOptions = sp.GetRequiredService<SqlServerStoreOptions>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var clusterMembership = sp.GetRequiredService<IClusterMembershipStore>();
                return new SqlServerLeaseManager(clusterMembership, storeOptions.ConnectionString!, storeOptions.Schema, storeOptions.NumOfShards);
            });
            services.AddHostedService(sp =>
                new SqlServerLeaseService(
                    heartbeatInterval,
                    sp.GetRequiredService<SqlServerLeaseManager>(),
                    sp.GetRequiredService<ILogger<SqlServerLeaseService>>()
                ));

            return services;
        }
    }
}
